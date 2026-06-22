using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class Monster_Movement : MonoBehaviour
{
    public enum MonsterState
    {
        Patrol,
        Investigate,
        Chase,
        ReturnToPatrol
    }

    /// <summary>What the monster does immediately after a teleport.</summary>
    public enum PostTeleportBehavior
    {
        Investigate,
        Chase,
        ReturnToPatrol
    }

    [Header("References")]
    [SerializeField] private Transform    player;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator     animator;

    [Header("Patrol")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float patrolPointReachedDistance = 2f;
    [SerializeField] private float patrolWaitTime             = 1.5f;

    [Header("Detection Ranges — state-specific")]
    [Tooltip("How far the monster detects the player while Patrolling or returning to patrol")]
    [SerializeField] private float patrolDetectionRange      = 10f;
    [Tooltip("How far the monster detects the player while Investigating")]
    [SerializeField] private float investigateDetectionRange = 14f;
    [Tooltip("How far the monster detects the player while actively Chasing")]
    [SerializeField] private float chaseRange                = 18f;
    [Tooltip("Chase is abandoned when the player exceeds this distance")]
    [SerializeField] private float losePlayerRange           = 25f;

    [Header("Line of Sight")]
    [SerializeField] private bool      requireLineOfSight = false;
    [SerializeField] private LayerMask lineOfSightMask    = ~0;

    [Header("Movement Speeds")]
    [SerializeField] private float patrolSpeed      = 2.5f;
    [SerializeField] private float investigateSpeed = 3.5f;
    [SerializeField] private float chaseSpeed       = 5f;

    [Header("Investigation")]
    [SerializeField] private float investigateReachedDistance = 3f;
    [SerializeField] private float investigateWaitTime        = 4f;

    [Header("Attack")]
    [SerializeField] private float attackStartRange          = 4f;
    [SerializeField] private float attackDamageRange         = 4.5f;
    [SerializeField] private float stopBeforeAttackDistance  = 3.5f;
    [SerializeField] private float attackCooldown            = 2f;
    [SerializeField] private float attackHitDelay            = 0.7f;
    [SerializeField] private float afterAttackDelay          = 0.4f;

    [Header("Animator Parameters")]
    [SerializeField] private string attackTriggerName = "Attack";
    [SerializeField] private bool   useCrawlBool      = true;
    [SerializeField] private string crawlBoolName     = "Idle to crawl";

    // ── Runtime state ────────────────────────────────────────────────────────

    private MonsterState state = MonsterState.Patrol;

    private int   currentPatrolIndex;
    private float waitTimer;

    private Vector3 investigateTarget;
    private bool    hasInvestigateTarget;

    private bool  playerInSafeArea;
    private bool  isAttacking;
    private float nextAttackTime;

    private PlayerDeath playerDeath;

    // ── Public accessors ─────────────────────────────────────────────────────

    public MonsterState CurrentState => state;
    public Transform    Player       => player;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (agent == null)    agent    = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        if (player != null) playerDeath = player.GetComponent<PlayerDeath>();

        if (agent == null)
        {
            Debug.LogError("Monster has no NavMeshAgent assigned.");
            enabled = false;
            return;
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogError("Monster NavMeshAgent is not on a NavMesh.");
            enabled = false;
            return;
        }

        agent.stoppingDistance = stopBeforeAttackDistance;

        SetState(MonsterState.Patrol);
        GoToNextPatrolPoint();
    }

    private void Update()
    {
        if (player == null || agent == null) return;
        if (!agent.isOnNavMesh) return;

        if (playerDeath != null && playerDeath.IsDead) { StopMonster(); return; }
        if (isAttacking) return;

        UpdateAnimatorMovement();

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (!playerInSafeArea && distanceToPlayer <= attackStartRange && Time.time >= nextAttackTime)
        {
            StartCoroutine(AttackPlayer());
            return;
        }

        bool canDetect = !playerInSafeArea && CanDetectPlayer();

        if (canDetect)
            SetState(MonsterState.Chase);
        else if (state == MonsterState.Chase && playerInSafeArea)
            SetState(MonsterState.ReturnToPatrol);
        else if (state == MonsterState.Chase && !CanKeepChasingPlayer())
            SetInvestigateTarget(player.position);

        switch (state)
        {
            case MonsterState.Patrol:         UpdatePatrol();         break;
            case MonsterState.Investigate:    UpdateInvestigate();    break;
            case MonsterState.Chase:          UpdateChase();          break;
            case MonsterState.ReturnToPatrol: UpdateReturnToPatrol(); break;
        }
    }

    // ── State updates ────────────────────────────────────────────────────────

    private void UpdatePatrol()
    {
        agent.isStopped        = false;
        agent.speed            = patrolSpeed;
        agent.stoppingDistance = 0.5f;

        if (patrolPoints == null || patrolPoints.Length == 0) return;

        if (!agent.pathPending && agent.remainingDistance <= patrolPointReachedDistance)
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= patrolWaitTime) { waitTimer = 0f; GoToNextPatrolPoint(); }
        }
    }

    private void UpdateInvestigate()
    {
        agent.isStopped        = false;
        agent.speed            = investigateSpeed;
        agent.stoppingDistance = 1f;

        if (!hasInvestigateTarget) { SetState(MonsterState.ReturnToPatrol); return; }

        if (!agent.pathPending && agent.remainingDistance <= investigateReachedDistance)
        {
            waitTimer += Time.deltaTime;
            if (waitTimer >= investigateWaitTime)
            {
                waitTimer            = 0f;
                hasInvestigateTarget = false;
                SetState(MonsterState.ReturnToPatrol);
            }
        }
    }

    private void UpdateChase()
    {
        if (playerInSafeArea) { SetState(MonsterState.ReturnToPatrol); return; }

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist <= attackStartRange) { StopMonster(); FacePlayer(); return; }

        agent.isStopped        = false;
        agent.speed            = chaseSpeed;
        agent.stoppingDistance = stopBeforeAttackDistance;
        agent.SetDestination(player.position);
    }

    private void UpdateReturnToPatrol()
    {
        agent.isStopped        = false;
        agent.speed            = patrolSpeed;
        agent.stoppingDistance = 0.5f;

        if (patrolPoints == null || patrolPoints.Length == 0) return;

        Transform pt = patrolPoints[currentPatrolIndex];
        if (pt != null) agent.SetDestination(pt.position);

        if (!agent.pathPending && agent.remainingDistance <= patrolPointReachedDistance)
        {
            SetState(MonsterState.Patrol);
            GoToNextPatrolPoint();
        }
    }

    // ── Attack ───────────────────────────────────────────────────────────────

    private IEnumerator AttackPlayer()
    {
        isAttacking    = true;
        nextAttackTime = Time.time + attackCooldown;

        StopMonster();
        FacePlayer();

        if (animator != null)
        {
            animator.ResetTrigger(attackTriggerName);
            animator.SetTrigger(attackTriggerName);
        }

        Debug.Log("[Monster] Attack started.");

        yield return new WaitForSeconds(attackHitDelay);

        if (!playerInSafeArea && player != null)
        {
            float dist = Vector3.Distance(transform.position, player.position);

            if (dist <= attackDamageRange)
            {
                if (playerDeath == null) playerDeath = player.GetComponent<PlayerDeath>();
                if (playerDeath != null) playerDeath.KillPlayer();
                else Debug.LogWarning("[Monster] PlayerDeath script missing on player.");
            }
            else
            {
                Debug.Log("[Monster] Attack missed — player moved away.");
            }
        }

        yield return new WaitForSeconds(afterAttackDelay);

        isAttacking = false;

        if (playerDeath != null && playerDeath.IsDead) { StopMonster(); yield break; }

        if (playerInSafeArea) SetState(MonsterState.ReturnToPatrol);
        else { agent.isStopped = false; SetState(MonsterState.Chase); }
    }

    // ── Public API — EnemyDifficultyController ────────────────────────────────

    public void SetPatrolSpeed(float v)              => patrolSpeed              = v;
    public void SetInvestigateSpeed(float v)         => investigateSpeed         = v;
    public void SetChaseSpeed(float v)               => chaseSpeed               = v;
    public void SetPatrolDetectionRange(float v)     => patrolDetectionRange     = v;
    public void SetInvestigateDetectionRange(float v)=> investigateDetectionRange = v;
    public void SetChaseRange(float v)               => chaseRange               = v;
    public void SetLosePlayerRange(float v)          => losePlayerRange          = v;

    // ── Public API — EnemyTeleportManager ────────────────────────────────────

    /// <summary>
    /// Warps the monster to the nearest valid NavMesh point and applies post-teleport behaviour.
    /// Returns true when the warp succeeded.
    /// </summary>
    public bool TeleportTo(Vector3 worldPosition, PostTeleportBehavior behavior,
                           Vector3 investigatePoint = default)
    {
        if (agent == null || !agent.isOnNavMesh)
        {
            Debug.LogWarning("[Monster] TeleportTo: agent not on NavMesh.");
            return false;
        }

        if (!NavMesh.SamplePosition(worldPosition, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            Debug.LogWarning($"[Monster] TeleportTo: no NavMesh near {worldPosition}.");
            return false;
        }

        agent.Warp(hit.position);
        agent.isStopped = false;
        isAttacking     = false;

        switch (behavior)
        {
            case PostTeleportBehavior.Investigate:
                Vector3 target = (investigatePoint != default) ? investigatePoint
                                 : (player != null ? player.position : hit.position);
                SetInvestigateTarget(target);
                break;

            case PostTeleportBehavior.Chase:
                if (!playerInSafeArea) SetState(MonsterState.Chase);
                else                   SetState(MonsterState.ReturnToPatrol);
                break;

            case PostTeleportBehavior.ReturnToPatrol:
                SetState(MonsterState.ReturnToPatrol);
                break;
        }

        Debug.Log($"[Monster] Teleported to {hit.position:F1}  behaviour: {behavior}");
        return true;
    }

    // ── Public API — SafeZone / WaitOut zones ────────────────────────────────

    public void SetPlayerSafe(bool isSafe)
    {
        playerInSafeArea = isSafe;

        if (playerInSafeArea)
        {
            hasInvestigateTarget = false;
            isAttacking          = false;
            if (agent != null && agent.isOnNavMesh) agent.isStopped = false;
            SetState(MonsterState.ReturnToPatrol);
        }
    }

    public void SetInvestigateTarget(Vector3 target)
    {
        if (playerInSafeArea) return;

        investigateTarget    = target;
        hasInvestigateTarget = true;
        SetState(MonsterState.Investigate);

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped        = false;
            agent.speed            = investigateSpeed;
            agent.stoppingDistance = 1f;
            agent.SetDestination(investigateTarget);
        }
    }

    /// <summary>
    /// Immediately cancels chasing / investigating and sends the monster back to patrol.
    /// Called by EnemyWaitOutSafeZone after the player has waited long enough.
    /// </summary>
    public void ForceReturnToPatrol()
    {
        hasInvestigateTarget = false;
        isAttacking          = false;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.ResetPath();
        }

        SetState(MonsterState.ReturnToPatrol);
        Debug.Log("[Monster] Forced to return to patrol.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void StopMonster()
    {
        if (agent == null || !agent.isOnNavMesh) return;
        agent.isStopped = true;
        agent.velocity  = Vector3.zero;
        agent.ResetPath();
    }

    private void FacePlayer()
    {
        if (player == null) return;
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    private void GoToNextPatrolPoint()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;

        currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;

        Transform t = patrolPoints[currentPatrolIndex];
        if (t != null && agent != null && agent.isOnNavMesh)
            agent.SetDestination(t.position);
    }

    // ── Detection ────────────────────────────────────────────────────────────

    /// <summary>Returns the correct detection range for the current state.</summary>
    private float GetCurrentDetectionRange()
    {
        switch (state)
        {
            case MonsterState.Patrol:         return patrolDetectionRange;
            case MonsterState.Investigate:    return investigateDetectionRange;
            case MonsterState.Chase:          return chaseRange;
            case MonsterState.ReturnToPatrol: return patrolDetectionRange;
            default:                          return chaseRange;
        }
    }

    private bool CanDetectPlayer()
    {
        if (playerInSafeArea) return false;

        float range    = GetCurrentDetectionRange();
        float distance = Vector3.Distance(transform.position, player.position);

        if (distance > range) return false;
        if (!requireLineOfSight) return true;

        Vector3 origin    = transform.position + Vector3.up * 1.5f;
        Vector3 targetPos = player.position    + Vector3.up * 1.2f;
        Vector3 direction = targetPos - origin;

        if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit,
                            range, lineOfSightMask, QueryTriggerInteraction.Ignore))
            return hit.transform == player || hit.transform.IsChildOf(player);

        return false;
    }

    private bool CanKeepChasingPlayer()
    {
        if (playerInSafeArea) return false;
        return Vector3.Distance(transform.position, player.position) <= losePlayerRange;
    }

    private void SetState(MonsterState newState)
    {
        if (state == newState) return;
        state     = newState;
        waitTimer = 0f;
    }

    private void UpdateAnimatorMovement()
    {
        if (animator == null || !useCrawlBool) return;
        bool isMoving = agent != null && agent.velocity.magnitude > 0.1f && !agent.isStopped;
        animator.SetBool(crawlBoolName, isMoving);
    }
}
