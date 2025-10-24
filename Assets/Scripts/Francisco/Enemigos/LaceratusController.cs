using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class LaceratusController : MonoBehaviour
{
    #region Serialized Fields

    [Header("Detection")]
    [SerializeField] private float visionRange = 7f;
    [SerializeField] private float visionConeAngle = 60f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Movement - Normal State")]
    [SerializeField] private float normalSpeed = 3f;
    [SerializeField] private float patrolDirectionChangeInterval = 4f;
    [SerializeField] private float patrolDirectionChangeAngle = 30f;

    [Header("Movement - Fury State")]
    [SerializeField] private float furySpeedMultiplier = 2f;
    [SerializeField] private float furyJumpDistance = 2f;
    [SerializeField] private float furyJumpReducedDistance = 1f;
    [SerializeField] private float furyJumpDelay = 2f;
    [SerializeField] private float furyJumpCancelDistance = 0.5f;

    [Header("Combat - Normal Attack")]
    [SerializeField] private float normalAttackDamage = 5f;
    [SerializeField] private float normalAttackInterval = 1.5f;
    [SerializeField] private float normalAttackRange = 1f;
    [SerializeField] private float normalAttackSlowDuration = 1f;
    [SerializeField] private float normalAttackSlowPercent = 0.3f;

    [Header("Combat - Fury Attack")]
    [SerializeField] private float furyAttackDamage = 10f;
    [SerializeField] private float furyAttackInterval = 0.75f;
    [SerializeField] private float furyAttackRange = 2f;

    [Header("Fury Mechanics")]
    [SerializeField] private float furyDecayTime = 3f;
    [SerializeField] private float furyRegenerationPerSecond = 1f;
    [SerializeField] private float consecutiveHitPushDistance = 1f;
    [SerializeField] private float screamTransitionDuration = 1.2f;

    [Header("Animation")]
    [SerializeField] private string normalAttackAnimationTrigger = "NormalAttack";
    [SerializeField] private string furyAttackAnimationTrigger = "FuryAttack";
    [SerializeField] private string furyStateAnimationBool = "IsFury";

    [Header("Audio")]
    [SerializeField] private AudioClip screamClip;
    [SerializeField] private AudioClip normalAttackClip;
    [SerializeField] private AudioClip furyAttackClip;

    #endregion

    #region Private Fields

    private NavMeshAgent agent;
    private Animator animator;
    private AudioSource audioSource;
    private EnemyHealth enemyHealth;
    private EnemyKnockbackHandler knockbackHandler;
    private Transform playerTransform;

    private bool isInFury = false;
    private bool playerDetected = false;
    private bool isTransitioningToFury = false;
    private int consecutiveHitsReceived = 0;

    private float lastAttackTime;
    private float lastDamageTime;
    private float lastDamageInflictedTime;
    private float patrolTimer;
    private float currentPatrolAngle;
    private float furyJumpTimer;

    private Coroutine furyDecayCoroutine;
    private Coroutine furyRegenerationCoroutine;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        enemyHealth = GetComponent<EnemyHealth>();
        knockbackHandler = GetComponent<EnemyKnockbackHandler>();

        if (agent != null)
        {
            agent.speed = normalSpeed;
        }
    }

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }

        if (enemyHealth != null)
        {
            enemyHealth.OnDamaged += HandleDamageReceived;
        }

        patrolTimer = Random.Range(0f, patrolDirectionChangeInterval);
        currentPatrolAngle = Random.Range(0f, 360f);
    }

    private void Update()
    {
        if (enemyHealth != null && enemyHealth.IsDead) return;
        if (isTransitioningToFury) return;

        if (!playerDetected)
        {
            CheckPlayerDetection();
            if (!playerDetected)
            {
                Patrol();
            }
        }

        if (playerDetected && playerTransform != null)
        {
            if (isInFury)
            {
                HandleFuryBehavior();
            }
            else
            {
                HandleNormalBehavior();
            }
        }

        if (isInFury)
        {
            CheckFuryDecay();
        }
    }

    private void OnDestroy()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDamaged -= HandleDamageReceived;
        }
    }

    #endregion

    #region Detection

    private void CheckPlayerDetection()
    {
        if (playerTransform == null) return;

        Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer > visionRange) return;

        float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);

        if (angleToPlayer <= visionConeAngle)
        {
            playerDetected = true;
            Debug.Log("Laceratus: Jugador detectado");
        }
    }

    #endregion

    #region Patrol

    private void Patrol()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        patrolTimer += Time.deltaTime;

        if (patrolTimer >= patrolDirectionChangeInterval)
        {
            patrolTimer = 0f;
            currentPatrolAngle += Random.Range(-patrolDirectionChangeAngle, patrolDirectionChangeAngle);
        }

        Vector3 direction = Quaternion.Euler(0, currentPatrolAngle, 0) * Vector3.forward;
        Vector3 targetPosition = transform.position + direction * 2f;

        if (NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    #endregion

    #region Normal Behavior

    private void HandleNormalBehavior()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        agent.speed = normalSpeed;
        agent.SetDestination(playerTransform.position);

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer <= normalAttackRange && Time.time >= lastAttackTime + normalAttackInterval)
        {
            PerformNormalAttack();
        }
    }

    private void PerformNormalAttack()
    {
        lastAttackTime = Time.time;
        lastDamageInflictedTime = Time.time;

        if (animator != null)
        {
            animator.SetTrigger(normalAttackAnimationTrigger);
        }

        if (audioSource != null && normalAttackClip != null)
        {
            audioSource.PlayOneShot(normalAttackClip);
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            IDamageable damageable = player.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(normalAttackDamage);

                PlayerMovement playerMovement = player.GetComponent<PlayerMovement>();
                if (playerMovement != null)
                {
                    StartCoroutine(ApplyPlayerSlow(playerMovement));
                }

                Debug.Log($"Laceratus: Ataque normal - Daño: {normalAttackDamage}");
            }
        }
    }

    private IEnumerator ApplyPlayerSlow(PlayerMovement playerMovement)
    {
        float originalSpeed = playerMovement.MoveSpeed;
        playerMovement.MoveSpeed *= (1f - normalAttackSlowPercent);

        yield return new WaitForSeconds(normalAttackSlowDuration);

        playerMovement.MoveSpeed = originalSpeed;
    }

    #endregion

    #region Fury Behavior

    private void HandleFuryBehavior()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        agent.speed = normalSpeed * furySpeedMultiplier;
        agent.SetDestination(playerTransform.position);

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer <= furyAttackRange && Time.time >= lastAttackTime + furyAttackInterval)
        {
            PerformFuryAttack();
        }
        else if (distanceToPlayer > furyAttackRange)
        {
            furyJumpTimer += Time.deltaTime;

            if (furyJumpTimer >= furyJumpDelay)
            {
                PerformFuryJump(distanceToPlayer);
                furyJumpTimer = 0f;
            }
        }
        else
        {
            furyJumpTimer = 0f;
        }
    }

    private void PerformFuryAttack()
    {
        lastAttackTime = Time.time;
        lastDamageInflictedTime = Time.time;

        if (animator != null)
        {
            animator.SetTrigger(furyAttackAnimationTrigger);
        }

        if (audioSource != null && furyAttackClip != null)
        {
            audioSource.PlayOneShot(furyAttackClip);
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            IDamageable damageable = player.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(furyAttackDamage);
                Debug.Log($"Laceratus: Ataque furia - Daño: {furyAttackDamage}");
            }
        }
    }

    private void PerformFuryJump(float distanceToPlayer)
    {
        if (knockbackHandler == null || playerTransform == null) return;

        Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
        float jumpDistance = furyJumpDistance;

        if (distanceToPlayer < furyJumpCancelDistance)
        {
            return;
        }
        else if (distanceToPlayer < furyJumpDistance)
        {
            jumpDistance = furyJumpReducedDistance;
        }

        knockbackHandler.TriggerKnockback(directionToPlayer, jumpDistance, 0.5f);
        Debug.Log($"Laceratus: Salto de furia - Distancia: {jumpDistance}");
    }

    #endregion

    #region Fury State Management

    private void HandleDamageReceived()
    {
        lastDamageTime = Time.time;
        consecutiveHitsReceived++;

        if (!isInFury)
        {
            StartCoroutine(EnterFuryState());
        }
        else
        {
            if (consecutiveHitsReceived >= 2)
            {
                PerformPushbackScream();
                consecutiveHitsReceived = 0;
            }
        }

        if (furyDecayCoroutine != null)
        {
            StopCoroutine(furyDecayCoroutine);
        }
        furyDecayCoroutine = StartCoroutine(FuryDecayRoutine());

        if (furyRegenerationCoroutine != null)
        {
            StopCoroutine(furyRegenerationCoroutine);
        }
        furyRegenerationCoroutine = StartCoroutine(FuryRegenerationRoutine());
    }

    private IEnumerator EnterFuryState()
    {
        isTransitioningToFury = true;

        if (audioSource != null && screamClip != null)
        {
            audioSource.PlayOneShot(screamClip);
        }

        if (animator != null)
        {
            animator.SetBool(furyStateAnimationBool, true);
        }

        Debug.Log("Laceratus: Entrando en estado de furia");

        yield return new WaitForSeconds(screamTransitionDuration);

        isInFury = true;
        isTransitioningToFury = false;
        consecutiveHitsReceived = 0;
    }

    private void ExitFuryState()
    {
        isInFury = false;
        consecutiveHitsReceived = 0;

        if (animator != null)
        {
            animator.SetBool(furyStateAnimationBool, false);
        }

        if (agent != null && agent.enabled)
        {
            agent.speed = normalSpeed;
        }

        Debug.Log("Laceratus: Saliendo del estado de furia");
    }

    private void CheckFuryDecay()
    {
        if (Time.time - lastDamageTime >= furyDecayTime &&
            Time.time - lastDamageInflictedTime >= furyDecayTime)
        {
            ExitFuryState();
        }
    }

    private IEnumerator FuryDecayRoutine()
    {
        yield return new WaitForSeconds(furyDecayTime);

        if (Time.time - lastDamageInflictedTime >= furyDecayTime)
        {
            ExitFuryState();
        }
    }

    private IEnumerator FuryRegenerationRoutine()
    {
        while (isInFury)
        {
            yield return new WaitForSeconds(1f);

            if (enemyHealth != null && !enemyHealth.IsDead)
            {
                enemyHealth.Heal(furyRegenerationPerSecond);
            }
        }
    }

    private void PerformPushbackScream()
    {
        if (audioSource != null && screamClip != null)
        {
            audioSource.PlayOneShot(screamClip);
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && playerTransform != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

            if (distanceToPlayer <= furyAttackRange)
            {
                EnemyKnockbackHandler playerKnockback = player.GetComponent<EnemyKnockbackHandler>();
                if (playerKnockback != null)
                {
                    Vector3 pushDirection = (playerTransform.position - transform.position).normalized;
                    playerKnockback.TriggerKnockback(pushDirection, consecutiveHitPushDistance, 0.3f);
                    Debug.Log("Laceratus: Onda de empuje activada");
                }
            }
        }
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, visionRange);

        Gizmos.color = Color.red;
        Vector3 leftBoundary = Quaternion.Euler(0, -visionConeAngle, 0) * transform.forward * visionRange;
        Vector3 rightBoundary = Quaternion.Euler(0, visionConeAngle, 0) * transform.forward * visionRange;
        Gizmos.DrawRay(transform.position, leftBoundary);
        Gizmos.DrawRay(transform.position, rightBoundary);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, normalAttackRange);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, furyAttackRange);
    }

    #endregion
}