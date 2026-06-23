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
    [Tooltip("How long the monster investigates the last known position after losing the player. " +
             "Set to 0 to reuse investigateWaitTime.")]
    [SerializeField] private float lostPlayerInvestigateTime  = 8f;

    [Header("Escape Zone")]
    [Tooltip("Effective lose-player range when the player is inside a MonsterEscapeZone. " +
             "Should be much smaller than the normal losePlayerRange.")]
    [SerializeField] private float escapeZoneLosePlayerRange    = 40f;
    [Tooltip("If the monster is closer than this distance it ignores the escape zone " +
             "and keeps chasing normally.")]
    [SerializeField] private float escapeZoneMinDangerDistance  = 15f;

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
    private bool  playerProtected;        // set by AreaSafeZone — no attack, no return to cabin patrol
    private bool  playerInEscapeZone;
    private float activeEscapeZoneLoseRange;
    private float activeEscapeZoneMinDanger;

    private bool  isLostPlayerInvestigation;

    // Area investigation (castle / house / barn roaming)
    private bool     isAreaInvestigating;
    private Vector3  investigationAreaCenter;
    private float    investigationAreaRadius;
    private float    investigationEndTime;
    private float    nextRandomInvestigationMoveTime;
    private float    randomInvestigationMoveInterval;
    private float    investigationNavMeshSampleRadius;
    private Collider investigationForbiddenCollider;

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

        if (!playerInSafeArea && !playerProtected && distanceToPlayer <= attackStartRange && Time.time >= nextAttackTime)
        {
            StartCoroutine(AttackPlayer());
            return;
        }

        bool canDetect = !playerInSafeArea && !playerProtected && CanDetectPlayer();

        // Escape zone: suppress detection if the player is beyond the reduced lose range
        // and outside the "still dangerous" distance. This prevents chaseRange from
        // overriding the escape zone and keeps the monster from re-entering Chase.
        if (canDetect && playerInEscapeZone)
        {
            float dist = Vector3.Distance(transform.position, player.position);
            if (dist > activeEscapeZoneMinDanger && dist > activeEscapeZoneLoseRange)
            {
                Debug.Log($"[Monster] Escape zone active, reduced lose range = {activeEscapeZoneLoseRange:F0} — suppressing detection.");
                canDetect = false;
            }
        }

        if (canDetect)
            SetState(MonsterState.Chase);
        else if (state == MonsterState.Chase && (playerInSafeArea || playerProtected))
            SetState(MonsterState.Investigate);  // area zone: stay nearby, don't go straight to patrol
        else if (state == MonsterState.Chase && !CanKeepChasingPlayer())
            LosePlayer();

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

        if (isAreaInvestigating)
        {
            UpdateAreaInvestigation();
            return;
        }

        // Fallback: old fixed-target investigate (losing player / forest teleport)
        if (!hasInvestigateTarget) { SetState(MonsterState.ReturnToPatrol); return; }

        if (!agent.pathPending && agent.remainingDistance <= investigateReachedDistance)
        {
            waitTimer += Time.deltaTime;
            float waitDuration = (isLostPlayerInvestigation && lostPlayerInvestigateTime > 0f)
                                  ? lostPlayerInvestigateTime
                                  : investigateWaitTime;

            if (waitTimer >= waitDuration)
            {
                waitTimer                 = 0f;
                hasInvestigateTarget      = false;
                isLostPlayerInvestigation = false;
                SetState(MonsterState.ReturnToPatrol);
                Debug.Log("[Monster] Lost-player investigation complete, returning to patrol.");
            }
        }
    }

    private void UpdateAreaInvestigation()
    {
        if (Time.time >= investigationEndTime)
        {
            StopAreaInvestigation();
            SetState(MonsterState.ReturnToPatrol);
            Debug.Log("[Monster] Area investigation complete, returning to patrol.");
            return;
        }

        bool timeForNewMove = Time.time >= nextRandomInvestigationMoveTime;
        bool reachedDest    = !agent.pathPending && agent.remainingDistance <= investigateReachedDistance;

        if (timeForNewMove || reachedDest)
        {
            if (TryGetRandomInvestigationPoint(out Vector3 point))
            {
                agent.SetDestination(point);
                nextRandomInvestigationMoveTime = Time.time + randomInvestigationMoveInterval;
            }
            else
            {
                nextRandomInvestigationMoveTime = Time.time + 1f;
                Debug.Log("[Monster] Area investigation: no valid NavMesh point found, retrying in 1s.");
            }
        }
    }

    private void UpdateChase()
    {
        if (playerInSafeArea) { SetState(MonsterState.ReturnToPatrol); return; }
        if (playerProtected)  { SetState(MonsterState.Investigate);    return; }

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

        if (!playerInSafeArea && !playerProtected && player != null)
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

        if (playerInSafeArea)       SetState(MonsterState.ReturnToPatrol);
        else if (isAreaInvestigating) SetState(MonsterState.Investigate);
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

    // ── Public API — MonsterEscapeZone ───────────────────────────────────────

    /// <summary>
    /// Called by MonsterEscapeZone. While active, the effective lose-player range
    /// is reduced so the player can shake the monster if they have enough distance.
    /// The monster still chases normally when closer than minDangerDistance.
    /// </summary>
    public void SetPlayerInEscapeZone(bool inZone, float reducedLoseRange, float minDangerDistance)
    {
        playerInEscapeZone        = inZone;
        activeEscapeZoneLoseRange = reducedLoseRange;
        activeEscapeZoneMinDanger = minDangerDistance;

        if (inZone)
            Debug.Log($"[Monster] Escape zone active — reduced lose range = {reducedLoseRange:F0}, " +
                      $"still dangerous within {minDangerDistance:F0}");
        else
            Debug.Log("[Monster] Escape zone deactivated — normal lose range restored.");
    }

    // ── Public API — AreaSafeZone ─────────────────────────────────────────────

    /// <summary>
    /// Called by AreaSafeZone. Prevents the monster from attacking/killing the player
    /// but does NOT return it to patrol — the monster keeps roaming the area.
    /// </summary>
    public void SetPlayerProtected(bool isProtected)
    {
        playerProtected = isProtected;
        Debug.Log(isProtected
            ? "[Monster] Player protected by area safe zone — attacks disabled."
            : "[Monster] Player protection removed — monster can chase again.");
    }

    /// <summary>
    /// Switches the monster into the area-investigation (random roaming) mode.
    /// Called by AreaSafeZone when the player enters, or by EnemyTeleportManager
    /// when a trigger zone has a linked AreaSafeZone.
    /// </summary>
    public void StartAreaInvestigation(
        Vector3  areaCenter,
        float    areaRadius,
        Collider forbiddenSafeAreaCollider,
        float    duration,
        float    moveInterval,
        float    navMeshSampleRadius)
    {
        investigationAreaCenter          = areaCenter;
        investigationAreaRadius          = areaRadius;
        investigationForbiddenCollider   = forbiddenSafeAreaCollider;
        investigationEndTime             = Time.time + duration;
        randomInvestigationMoveInterval  = moveInterval;
        investigationNavMeshSampleRadius = navMeshSampleRadius;
        nextRandomInvestigationMoveTime  = 0f;   // pick first destination immediately
        isAreaInvestigating              = true;
        isLostPlayerInvestigation        = false;
        hasInvestigateTarget             = false;
        isAttacking                      = false;

        SetState(MonsterState.Investigate);

        // Pick and set the first destination right away
        if (TryGetRandomInvestigationPoint(out Vector3 firstPoint) && agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(firstPoint);
        }

        Debug.Log($"[Monster] Area investigation started. Center={areaCenter:F1} " +
                  $"Radius={areaRadius} Duration={duration}s MoveInterval={moveInterval}s");
    }

    /// <summary>Stops area roaming without changing state — state must be set by the caller.</summary>
    private void StopAreaInvestigation()
    {
        isAreaInvestigating          = false;
        investigationForbiddenCollider = null;
    }

    /// <summary>
    /// Tries up to 10 times to find a random valid NavMesh point inside the investigation
    /// area that is not inside the forbidden safe-area collider.
    /// </summary>
    private bool TryGetRandomInvestigationPoint(out Vector3 point)
    {
        const int maxAttempts = 10;
        point = Vector3.zero;

        for (int i = 0; i < maxAttempts; i++)
        {
            Vector2 circle    = Random.insideUnitCircle * investigationAreaRadius;
            Vector3 candidate = investigationAreaCenter + new Vector3(circle.x, 0f, circle.y);

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit,
                                        investigationNavMeshSampleRadius, NavMesh.AllAreas))
                continue;

            // Reject points that fall inside the safe-area collider
            if (investigationForbiddenCollider != null)
            {
                Vector3 closest = investigationForbiddenCollider.ClosestPoint(hit.position);
                if ((closest - hit.position).sqrMagnitude < 0.01f)
                    continue;
            }

            point = hit.position;
            return true;
        }

        return false;
    }

    // ── Internal — losing the player ─────────────────────────────────────────

    private void LosePlayer()
    {
        if (player == null) return;
        Debug.Log("[Monster] Player escaped chase — investigating last known position.");
        isLostPlayerInvestigation = true;
        SetInvestigateTarget(player.position);
    }

    // ── Public API — SafeZone / WaitOut zones ────────────────────────────────

    public void SetPlayerSafe(bool isSafe)
    {
        playerInSafeArea = isSafe;

        if (playerInSafeArea)
        {
            hasInvestigateTarget      = false;
            isAttacking               = false;
            isLostPlayerInvestigation = false;
            playerProtected           = false;  // cabin overrides area protection
            StopAreaInvestigation();
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
        hasInvestigateTarget      = false;
        isAttacking               = false;
        isLostPlayerInvestigation = false;
        playerProtected           = false;
        StopAreaInvestigation();

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
        if (playerInSafeArea || playerProtected) return false;

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
        if (playerInSafeArea || playerProtected) return false;

        float dist = Vector3.Distance(transform.position, player.position);

        if (playerInEscapeZone)
        {
            // If monster is very close, ignore the escape zone — it's still dangerous
            if (dist <= activeEscapeZoneMinDanger) return true;

            // Otherwise use the reduced lose range
            return dist <= activeEscapeZoneLoseRange;
        }

        return dist <= losePlayerRange;
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
