using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class Monster_Movement : MonoBehaviour
{
    private enum MonsterState
    {
        Patrol,
        Investigate,
        Chase,
        ReturnToPatrol
    }

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;

    [Header("Patrol")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float patrolPointReachedDistance = 2f;
    [SerializeField] private float patrolWaitTime = 1.5f;

    [Header("Detection")]
    [SerializeField] private float chaseRange = 50f;
    [SerializeField] private float losePlayerRange = 70f;
    [SerializeField] private bool requireLineOfSight = false;
    [SerializeField] private LayerMask lineOfSightMask = ~0;

    [Header("Movement")]
    [SerializeField] private float patrolSpeed = 3f;
    [SerializeField] private float investigateSpeed = 4f;
    [SerializeField] private float chaseSpeed = 6f;

    [Header("Investigation")]
    [SerializeField] private float investigateReachedDistance = 3f;
    [SerializeField] private float investigateWaitTime = 4f;

    [Header("Attack")]
    [SerializeField] private float attackStartRange = 4f;
    [SerializeField] private float attackDamageRange = 4.5f;
    [SerializeField] private float stopBeforeAttackDistance = 3.5f;
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private float attackHitDelay = 0.7f;
    [SerializeField] private float afterAttackDelay = 0.4f;

    [Header("Animator Parameters")]
    [SerializeField] private string attackTriggerName = "Attack";
    [SerializeField] private bool useCrawlBool = true;
    [SerializeField] private string crawlBoolName = "Idle to crawl";

    private MonsterState state = MonsterState.Patrol;

    private int currentPatrolIndex;
    private float waitTimer;

    private Vector3 investigateTarget;
    private bool hasInvestigateTarget;

    private bool playerInSafeArea;
    private bool isAttacking;
    private float nextAttackTime;

    private PlayerDeath playerDeath;

    private void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
                player = playerObject.transform;
        }

        if (player != null)
            playerDeath = player.GetComponent<PlayerDeath>();

        if (agent == null)
        {
            Debug.LogError("Monster has no NavMeshAgent assigned.");
            enabled = false;
            return;
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogError("Monster NavMeshAgent is not on a NavMesh. Move the monster onto the baked NavMesh.");
            enabled = false;
            return;
        }

        agent.stoppingDistance = stopBeforeAttackDistance;

        SetState(MonsterState.Patrol);
        GoToNextPatrolPoint();
    }

    private void Update()
    {
        if (player == null || agent == null)
            return;

        if (!agent.isOnNavMesh)
            return;

        if (playerDeath != null && playerDeath.IsDead)
        {
            StopMonster();
            return;
        }

        if (isAttacking)
            return;

        UpdateAnimatorMovement();

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // Attack starts BEFORE the monster touches the player.
        if (!playerInSafeArea && distanceToPlayer <= attackStartRange && Time.time >= nextAttackTime)
        {
            StartCoroutine(AttackPlayer());
            return;
        }

        bool canChasePlayer = !playerInSafeArea && CanDetectPlayer();

        if (canChasePlayer)
        {
            SetState(MonsterState.Chase);
        }
        else if (state == MonsterState.Chase && playerInSafeArea)
        {
            SetState(MonsterState.ReturnToPatrol);
        }
        else if (state == MonsterState.Chase && !CanKeepChasingPlayer())
        {
            SetInvestigateTarget(player.position);
        }

        switch (state)
        {
            case MonsterState.Patrol:
                UpdatePatrol();
                break;

            case MonsterState.Investigate:
                UpdateInvestigate();
                break;

            case MonsterState.Chase:
                UpdateChase();
                break;

            case MonsterState.ReturnToPatrol:
                UpdateReturnToPatrol();
                break;
        }
    }

    private void UpdatePatrol()
    {
        agent.isStopped = false;
        agent.speed = patrolSpeed;
        agent.stoppingDistance = 0.5f;

        if (patrolPoints == null || patrolPoints.Length == 0)
            return;

        if (!agent.pathPending && agent.remainingDistance <= patrolPointReachedDistance)
        {
            waitTimer += Time.deltaTime;

            if (waitTimer >= patrolWaitTime)
            {
                waitTimer = 0f;
                GoToNextPatrolPoint();
            }
        }
    }

    private void UpdateInvestigate()
    {
        agent.isStopped = false;
        agent.speed = investigateSpeed;
        agent.stoppingDistance = 1f;

        if (!hasInvestigateTarget)
        {
            SetState(MonsterState.ReturnToPatrol);
            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= investigateReachedDistance)
        {
            waitTimer += Time.deltaTime;

            if (waitTimer >= investigateWaitTime)
            {
                waitTimer = 0f;
                hasInvestigateTarget = false;
                SetState(MonsterState.ReturnToPatrol);
            }
        }
    }

    private void UpdateChase()
    {
        if (playerInSafeArea)
        {
            SetState(MonsterState.ReturnToPatrol);
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // If close enough to attack, stop moving already.
        // This prevents the monster from running into the player.
        if (distanceToPlayer <= attackStartRange)
        {
            StopMonster();
            FacePlayer();
            return;
        }

        agent.isStopped = false;
        agent.speed = chaseSpeed;
        agent.stoppingDistance = stopBeforeAttackDistance;
        agent.SetDestination(player.position);
    }

    private void UpdateReturnToPatrol()
    {
        agent.isStopped = false;
        agent.speed = patrolSpeed;
        agent.stoppingDistance = 0.5f;

        if (patrolPoints == null || patrolPoints.Length == 0)
            return;

        Transform patrolPoint = patrolPoints[currentPatrolIndex];

        if (patrolPoint != null)
            agent.SetDestination(patrolPoint.position);

        if (!agent.pathPending && agent.remainingDistance <= patrolPointReachedDistance)
        {
            SetState(MonsterState.Patrol);
            GoToNextPatrolPoint();
        }
    }

    private IEnumerator AttackPlayer()
    {
        isAttacking = true;
        nextAttackTime = Time.time + attackCooldown;

        StopMonster();
        FacePlayer();

        if (animator != null)
        {
            animator.ResetTrigger(attackTriggerName);
            animator.SetTrigger(attackTriggerName);
        }

        Debug.Log("Monster attack started.");

        yield return new WaitForSeconds(attackHitDelay);

        if (!playerInSafeArea && player != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);

            if (distanceToPlayer <= attackDamageRange)
            {
                if (playerDeath == null)
                    playerDeath = player.GetComponent<PlayerDeath>();

                if (playerDeath != null)
                {
                    playerDeath.KillPlayer();
                }
                else
                {
                    Debug.LogWarning("PlayerDeath script missing on player.");
                }
            }
            else
            {
                Debug.Log("Monster attack missed. Player was too far away.");
            }
        }

        yield return new WaitForSeconds(afterAttackDelay);

        isAttacking = false;

        if (playerDeath != null && playerDeath.IsDead)
        {
            StopMonster();
            yield break;
        }

        if (playerInSafeArea)
        {
            SetState(MonsterState.ReturnToPatrol);
        }
        else
        {
            agent.isStopped = false;
            SetState(MonsterState.Chase);
        }
    }

    private void StopMonster()
    {
        if (agent == null || !agent.isOnNavMesh)
            return;

        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        agent.ResetPath();
    }

    private void FacePlayer()
    {
        if (player == null)
            return;

        Vector3 lookDirection = player.position - transform.position;
        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(lookDirection);
    }

    private void GoToNextPatrolPoint()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
            return;

        currentPatrolIndex++;

        if (currentPatrolIndex >= patrolPoints.Length)
            currentPatrolIndex = 0;

        Transform target = patrolPoints[currentPatrolIndex];

        if (target != null && agent != null && agent.isOnNavMesh)
            agent.SetDestination(target.position);
    }

    private bool CanDetectPlayer()
    {
        if (playerInSafeArea)
            return false;

        float distance = Vector3.Distance(transform.position, player.position);

        if (distance > chaseRange)
            return false;

        if (!requireLineOfSight)
            return true;

        Vector3 origin = transform.position + Vector3.up * 1.5f;
        Vector3 target = player.position + Vector3.up * 1.2f;
        Vector3 direction = target - origin;

        if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, chaseRange, lineOfSightMask, QueryTriggerInteraction.Ignore))
        {
            return hit.transform == player || hit.transform.IsChildOf(player);
        }

        return false;
    }

    private bool CanKeepChasingPlayer()
    {
        if (playerInSafeArea)
            return false;

        float distance = Vector3.Distance(transform.position, player.position);
        return distance <= losePlayerRange;
    }

    private void SetState(MonsterState newState)
    {
        if (state == newState)
            return;

        state = newState;
        waitTimer = 0f;
    }

    public void SetInvestigateTarget(Vector3 target)
    {
        if (playerInSafeArea)
            return;

        investigateTarget = target;
        hasInvestigateTarget = true;

        SetState(MonsterState.Investigate);

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = investigateSpeed;
            agent.stoppingDistance = 1f;
            agent.SetDestination(investigateTarget);
        }
    }

    public void SetPlayerSafe(bool isSafe)
    {
        playerInSafeArea = isSafe;

        if (playerInSafeArea)
        {
            hasInvestigateTarget = false;
            isAttacking = false;

            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = false;
            }

            SetState(MonsterState.ReturnToPatrol);
        }
    }

    private void UpdateAnimatorMovement()
    {
        if (animator == null || !useCrawlBool)
            return;

        bool isMoving = agent != null && agent.velocity.magnitude > 0.1f && !agent.isStopped;

        animator.SetBool(crawlBoolName, isMoving);
    }
}