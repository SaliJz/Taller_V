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

    [Header("Patrol - Stuck Check")] 
    [SerializeField] private float stuckCheckInterval = 2f;
    [SerializeField] private float stuckDistanceThreshold = 0.1f; 

    [Header("Movement - Fury State")]
    [SerializeField] private float furySpeedMultiplier = 2f;
    [SerializeField] private float furyLevel2SpeedBonus = 0.3f;
    [SerializeField] private float furyJumpDistance = 2f;
    [SerializeField] private float furyJumpReducedDistance = 1f;
    [SerializeField] private float furyJumpDelay = 2f;
    [SerializeField] private float furyJumpCancelDistance = 0.5f;
    [SerializeField] private float furyJumpDuration = 0.5f;

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
    [SerializeField] private float furyDecayNoDamageTime = 5f; 
    [SerializeField] private float furyRegenerationPerSecond = 1f;
    [SerializeField] private float consecutiveHitPushDistance = 1f;
    [SerializeField] private float screamTransitionDuration = 1.2f;
    [SerializeField] private int maxFuryLevel = 2;
    //[SerializeField] private float knockbackResistance = 0.9f;

    [Header("Visual Feedback")]
    [SerializeField] private Material normalStateMaterial;
    [SerializeField] private Material furyStateMaterial;
    [SerializeField] private Renderer enemyRenderer;
    [SerializeField] private GameObject normalAttackHitbox;
    [SerializeField] private GameObject furyAttackHitbox;
    [SerializeField] private GameObject screamHitbox;
    [SerializeField] private float hitboxDisplayDuration = 0.2f;

    [Header("Animation")]
    [SerializeField] private string normalAttackAnimationTrigger = "NormalAttack";
    [SerializeField] private string furyAttackAnimationTrigger = "FuryAttack";
    [SerializeField] private string furyStateAnimationBool = "IsFury";
    [SerializeField] private string screamAnimationTrigger = "Scream";

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;

    [Header("SFX: Estados y Movimiento")]
    [SerializeField] private AudioClip presenceSFX;
    [SerializeField] private AudioClip normalMoveSFX;
    [SerializeField] private AudioClip furyMoveSFX;
    [SerializeField] private AudioClip furyTransitionSFX;
    [SerializeField] private AudioClip calmTransitionSFX;

    [Header("SFX: Combate")]
    [SerializeField] private AudioClip normalAttackClip;
    [SerializeField] private AudioClip furyAttackClip;
    [SerializeField] private AudioClip furyJumpSFX;
    [SerializeField] private AudioClip pushbackSFX;
    [SerializeField] private AudioClip deathSFX;

    #endregion

    #region Private Fields

    private NavMeshAgent agent;
    private Animator animator;
    private EnemyHealth enemyHealth;
    private EnemyKnockbackHandler knockbackHandler;
    private Transform playerTransform;

    private bool isInFury = false;
    private bool playerDetected = false;
    private bool isTransitioningToFury = false;
    private bool isPerformingJump = false;
    private int consecutiveHitsReceived = 0;
    private int furyLevel = 0;

    private float lastAttackTime;
    private float lastDamageTime;
    private float lastDamageInflictedTime;
    private float patrolTimer;
    private float furyJumpTimer;
    private float timeSinceOutOfRange;

    private float stuckCheckTimer;
    private Vector3 lastPatrolPosition;

    private Coroutine furyDecayCoroutine;
    private Coroutine furyRegenerationCoroutine;
    private Material originalMaterial;

    private Vector3 currentPatrolDirection;

    private float audioStepTimer;
    private float idleAudioTimer;
    private float idleAudioInterval;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        enemyHealth = GetComponent<EnemyHealth>();
        knockbackHandler = GetComponent<EnemyKnockbackHandler>();
        if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>();

        if (agent != null)
        {
            agent.speed = normalSpeed;
        }

        if (enemyRenderer != null)
        {
            originalMaterial = enemyRenderer.material;
        }

        if (normalAttackHitbox != null) normalAttackHitbox.SetActive(false);
        if (furyAttackHitbox != null) furyAttackHitbox.SetActive(false);
        if (screamHitbox != null) screamHitbox.SetActive(false);

        if (knockbackHandler != null)
        {
            // knockbackHandler.knockbackResistance = knockbackResistance;
        }
    }

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }

        patrolTimer = Random.Range(0f, patrolDirectionChangeInterval);

        float randomInitialAngle = Random.Range(0f, 360f);
        currentPatrolDirection = Quaternion.Euler(0, randomInitialAngle, 0) * Vector3.forward;
        currentPatrolDirection.Normalize();

        lastPatrolPosition = transform.position;
    }

    private void OnEnable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDamaged += HandleDamageReceived;
            enemyHealth.OnDeath += HandleDeath;
        }
    }

    private void OnDisable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDamaged -= HandleDamageReceived;
            enemyHealth.OnDeath -= HandleDeath;
        }
    }

    private void OnDestroy()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDamaged -= HandleDamageReceived;
            enemyHealth.OnDeath -= HandleDeath;
        }
    }

    private void Update()
    {
        if (enemyHealth != null && enemyHealth.IsDead) return;
        if (enemyHealth != null && enemyHealth.IsStunned)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            Debug.Log("[Laceratus] Stunned");
            return;
        }
        if (isTransitioningToFury || isPerformingJump) return;

        HandleAudioLoop();

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

    #endregion

    #region Audio System

    private void HandleAudioLoop()
    {
        if (agent == null || !agent.enabled) return;

        bool isMoving = !agent.isStopped && agent.velocity.sqrMagnitude > 0.2f;

        if (isMoving)
        {
            audioStepTimer += Time.deltaTime;

            float stepRate = isInFury ? 0.35f : 0.6f;

            if (audioStepTimer >= stepRate)
            {
                AudioClip clipToPlay = isInFury ? furyMoveSFX : normalMoveSFX;

                if (audioSource != null && clipToPlay != null)
                {
                    if (isInFury) audioSource.pitch = Random.Range(1.1f, 1.3f);
                    else audioSource.pitch = Random.Range(0.9f, 1.1f);

                    audioSource.PlayOneShot(clipToPlay, 0.75f);
                    audioSource.pitch = 1f;
                }
                audioStepTimer = 0f;
            }
            ResetIdleAudioTimer();
        }
        else
        {
            idleAudioTimer += Time.deltaTime;
            if (idleAudioTimer >= idleAudioInterval)
            {
                if (audioSource != null && presenceSFX != null)
                {
                    audioSource.PlayOneShot(presenceSFX);
                }
                ResetIdleAudioTimer();
            }
        }
    }

    private void ResetIdleAudioTimer()
    {
        idleAudioTimer = 0f;
        idleAudioInterval = Random.Range(4f, 8f);
    }

    private void HandleDeath(GameObject enemy)
    {
        if (enemy != gameObject) return;

        if (audioSource != null && deathSFX != null)
        {
            audioSource.PlayOneShot(deathSFX);
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
        stuckCheckTimer += Time.deltaTime;

        if (stuckCheckTimer >= stuckCheckInterval)
        {
            float distanceMoved = Vector3.Distance(transform.position, lastPatrolPosition);

            if (distanceMoved < stuckDistanceThreshold)
            {
                Debug.Log("Laceratus: ¡Atascado detectado! Forzando nuevo rumbo aleatorio.");

                ChangePatrolDirection();
                agent.ResetPath();
            }

            lastPatrolPosition = transform.position;
            stuckCheckTimer = 0f;
        }

        if (patrolTimer >= patrolDirectionChangeInterval)
        {
            patrolTimer = 0f;
            ChangePatrolDirection(); 
        }

        if (!agent.hasPath || agent.remainingDistance < 0.5f)
        {
            SetNewPatrolDestination();
        }
    }

    private void SetNewPatrolDestination()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        int attempts = 0;
        int maxAttempts = 10;
        float searchRadius = 6f; 

        while (attempts < maxAttempts)
        {
            Vector3 randomPoint = Random.insideUnitSphere * searchRadius;
            randomPoint += transform.position;
            randomPoint.y = transform.position.y;

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, searchRadius, NavMesh.AllAreas))
            {
                NavMeshPath path = new NavMeshPath();
                if (agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    float straightDistance = Vector3.Distance(transform.position, hit.position);
                    float pathLength = CalculatePathLength(path); 

                    if (pathLength < straightDistance * 2.5f)
                    {
                        agent.SetDestination(hit.position);

                        currentPatrolDirection = (hit.position - transform.position).normalized;

                        Debug.Log($"Laceratus: Nuevo destino aleatorio fijado a {straightDistance:F1}m (camino: {pathLength:F1}m).");
                        return;
                    }
                }
            }

            attempts++;
        }

        ChangePatrolDirection();
        agent.ResetPath();
        Debug.Log("Laceratus: Error al encontrar destino NavMesh válido, forzando nuevo rumbo.");
    }

    private float CalculatePathLength(NavMeshPath path)
    {
        float length = 0f;
        for (int i = 1; i < path.corners.Length; i++)
        {
            length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        }
        return length;
    }

    private void ChangePatrolDirection()
    {
        float decision = Random.value;

        if (decision < 0.4f) 
        {
            float randomAngle = Random.Range(-patrolDirectionChangeAngle, patrolDirectionChangeAngle);
            currentPatrolDirection = Quaternion.Euler(0, randomAngle, 0) * currentPatrolDirection;
            Debug.Log($"Laceratus: Cambio de dirección de patrulla (Suave: {randomAngle:F1}°) - Vector: {currentPatrolDirection}");
        }
        else if (decision < 0.8f) 
        {
            float escapeAngle = Random.Range(90f, 180f) * (Random.value > 0.5f ? 1f : -1f);
            currentPatrolDirection = Quaternion.Euler(0, escapeAngle, 0) * transform.forward;
            Debug.Log($"Laceratus: Cambio de dirección de patrulla (Brusco: {escapeAngle:F1}°) - Vector: {currentPatrolDirection}");
        }
        else 
        {
            float randomNewAngle = Random.Range(0f, 360f);
            currentPatrolDirection = Quaternion.Euler(0, randomNewAngle, 0) * Vector3.forward;
            Debug.Log($"Laceratus: Cambio de dirección de patrulla (Totalmente Aleatorio: {randomNewAngle:F1}°) - Vector: {currentPatrolDirection}");
        }

        currentPatrolDirection.Normalize();
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

        if (normalAttackHitbox != null)
        {
            StartCoroutine(ShowHitbox(normalAttackHitbox));
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, normalAttackRange, playerLayer);

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                IDamageable damageable = hit.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    ExecuteAttack(hit.gameObject, normalAttackDamage);

                    PlayerMovement playerMovement = hit.GetComponent<PlayerMovement>();
                    if (playerMovement != null)
                    {
                        StartCoroutine(ApplyPlayerSlow(playerMovement));
                    }

                    Debug.Log($"Laceratus: Ataque normal - Daño: {normalAttackDamage}");
                }
                break;
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

        agent.SetDestination(playerTransform.position);

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer <= furyAttackRange)
        {
            timeSinceOutOfRange = 0f;
            furyJumpTimer = 0f;

            if (Time.time >= lastAttackTime + furyAttackInterval)
            {
                PerformFuryAttack();
            }
        }
        else
        {
            timeSinceOutOfRange += Time.deltaTime;
            furyJumpTimer += Time.deltaTime;

            if (timeSinceOutOfRange >= furyJumpDelay && furyJumpTimer >= furyJumpDelay)
            {
                PerformFuryJump(distanceToPlayer);
                furyJumpTimer = 0f;
            }
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

        if (furyAttackHitbox != null)
        {
            StartCoroutine(ShowHitbox(furyAttackHitbox));
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, furyAttackRange, playerLayer);

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                IDamageable damageable = hit.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    ExecuteAttack(hit.gameObject, furyAttackDamage);
                    Debug.Log($"Laceratus: Ataque furia - Daño: {furyAttackDamage}");
                }
                break;
            }
        }
    }

    private void PerformFuryJump(float distanceToPlayer)
    {
        if (knockbackHandler == null || playerTransform == null) return;

        if (distanceToPlayer < furyJumpCancelDistance)
        {
            Debug.Log("Laceratus: Salto cancelado - jugador demasiado cerca");
            return;
        }

        Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
        directionToPlayer.y = 0;

        float jumpDistance = furyJumpDistance;

        if (distanceToPlayer < furyJumpDistance)
        {
            jumpDistance = furyJumpReducedDistance;
        }

        StartCoroutine(ExecuteFuryJump(directionToPlayer, jumpDistance));
    }

    private IEnumerator ExecuteFuryJump(Vector3 direction, float distance)
    {
        isPerformingJump = true;

        if (audioSource != null && furyJumpSFX != null) audioSource.PlayOneShot(furyJumpSFX);

        if (agent != null && agent.enabled) agent.enabled = false;

        Vector3 startPosition = transform.position;
        Vector3 targetPosition = startPosition + direction * distance;

        if (NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, distance, NavMesh.AllAreas))
        {
            targetPosition = hit.position;
        }

        float elapsed = 0f;
        float jumpHeight = 1.5f;

        while (elapsed < furyJumpDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / furyJumpDuration;

            Vector3 currentPos = Vector3.Lerp(startPosition, targetPosition, t);
            float heightOffset = Mathf.Sin(t * Mathf.PI) * jumpHeight;

            currentPos.y += heightOffset;
            transform.position = currentPos;

            yield return null;
        }

        transform.position = targetPosition;

        if (agent != null)
        {
            agent.enabled = true;
        }

        isPerformingJump = false;

        Debug.Log($"Laceratus: Salto de furia completado - Distancia: {distance}");
    }

    #endregion

    #region Fury State Management

    private void HandleDamageReceived()
    {
        lastDamageTime = Time.time;

        if (!playerDetected && playerTransform != null)
        {
            playerDetected = true;
            Debug.Log("Laceratus: ¡Jugador detectado por daño recibido!");
        }

        if (isInFury && knockbackHandler != null)
        {
            StartCoroutine(ReduceKnockbackEffect());
        }

        if (!isInFury)
        {
            StartCoroutine(EnterFuryState());
        }
        else
        {
            consecutiveHitsReceived++;

            if (furyLevel < maxFuryLevel)
            {
                furyLevel++;
                UpdateFurySpeed();
                Debug.Log($"Laceratus: Nivel de furia aumentado a {furyLevel}");
            }

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

        if (isInFury)
        {
            if (furyRegenerationCoroutine != null)
            {
                StopCoroutine(furyRegenerationCoroutine);
            }
            furyRegenerationCoroutine = StartCoroutine(FuryRegenerationRoutine());
        }
    }

    private IEnumerator ReduceKnockbackEffect()
    {
        yield return null;

        if (agent != null && agent.enabled && !agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                transform.position = hit.position + Vector3.up * agent.baseOffset;
            }
        }
    }

    private IEnumerator EnterFuryState()
    {
        isTransitioningToFury = true;
        furyLevel = 1;

        if (audioSource != null && furyTransitionSFX != null) audioSource.PlayOneShot(furyTransitionSFX);

        if (animator != null)
        {
            animator.SetBool(furyStateAnimationBool, true);
            if (!string.IsNullOrEmpty(screamAnimationTrigger))
            {
                animator.SetTrigger(screamAnimationTrigger);
            }
        }

        ChangeMaterialToFury();

        Debug.Log("Laceratus: Entrando en estado de furia");

        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
        }

        yield return new WaitForSeconds(screamTransitionDuration);

        if (agent != null && agent.enabled)
        {
            agent.isStopped = false;
        }

        isInFury = true;
        isTransitioningToFury = false;
        consecutiveHitsReceived = 0;

        UpdateFurySpeed();

        if (furyRegenerationCoroutine != null)
        {
            StopCoroutine(furyRegenerationCoroutine);
        }
        furyRegenerationCoroutine = StartCoroutine(FuryRegenerationRoutine());
    }

    private void UpdateFurySpeed()
    {
        if (agent == null || !agent.enabled) return;

        float speedMultiplier = furySpeedMultiplier;

        if (furyLevel >= 2)
        {
            speedMultiplier += furyLevel2SpeedBonus;
        }

        agent.speed = normalSpeed * speedMultiplier;

        Debug.Log($"Laceratus: Velocidad ajustada a {agent.speed} unidades/s (Nivel {furyLevel})");
    }

    private void ExitFuryState()
    {
        isInFury = false;
        furyLevel = 0;
        consecutiveHitsReceived = 0;

        if (audioSource != null && calmTransitionSFX != null) audioSource.PlayOneShot(calmTransitionSFX);

        if (animator != null) animator.SetBool(furyStateAnimationBool, false);

        if (agent != null && agent.enabled) agent.speed = normalSpeed;

        ChangeMaterialToNormal();

        if (furyRegenerationCoroutine != null)
        {
            StopCoroutine(furyRegenerationCoroutine);
            furyRegenerationCoroutine = null;
        }

        Debug.Log("Laceratus: Saliendo del estado de furia");
    }

    private void CheckFuryDecay()
    {
        float decayTime = furyDecayTime;

        if (playerDetected && Time.time - lastDamageInflictedTime < furyDecayNoDamageTime)
        {
            decayTime = furyDecayNoDamageTime;
        }

        if (Time.time - lastDamageTime >= decayTime &&
            Time.time - lastDamageInflictedTime >= decayTime)
        {
            ExitFuryState();
        }
    }

    private IEnumerator FuryDecayRoutine()
    {
        float decayTime = furyDecayTime;

        if (playerDetected && Time.time - lastDamageInflictedTime < furyDecayNoDamageTime)
        {
            decayTime = furyDecayNoDamageTime;
        }

        yield return new WaitForSeconds(decayTime);

        if (isInFury && Time.time - lastDamageInflictedTime >= decayTime)
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
                Debug.Log($"Laceratus: Regeneración +{furyRegenerationPerSecond} PV");
            }
        }
    }

    private void PerformPushbackScream()
    {
        if (audioSource != null && pushbackSFX != null)
        {
            audioSource.PlayOneShot(pushbackSFX);
        }
        else if (audioSource != null && furyTransitionSFX != null)
        {
            audioSource.PlayOneShot(furyTransitionSFX);
        }

        if (animator != null)
        {
            animator.SetTrigger(screamAnimationTrigger);
        }

        if (screamHitbox != null)
        {
            StartCoroutine(ShowHitbox(screamHitbox));
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, furyAttackRange, playerLayer);

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                Vector3 pushDirection = (hit.transform.position - transform.position).normalized;
                pushDirection.y = 0;

                EnemyKnockbackHandler playerKnockback = hit.GetComponent<EnemyKnockbackHandler>();
                if (playerKnockback != null)
                {
                    playerKnockback.TriggerKnockback(pushDirection, consecutiveHitPushDistance, 0.3f);
                }
                else
                {
                    CharacterController controller = hit.GetComponent<CharacterController>();
                    if (controller != null)
                    {
                        StartCoroutine(PushPlayerWithController(controller, pushDirection));
                    }
                    else
                    {
                        Rigidbody rb = hit.GetComponent<Rigidbody>();
                        if (rb != null)
                        {
                            rb.AddForce(pushDirection * consecutiveHitPushDistance * 10f, ForceMode.Impulse);
                        }
                    }
                }

                Debug.Log("Laceratus: Onda de empuje activada");
                break;
            }
        }
    }

    private IEnumerator PushPlayerWithController(CharacterController controller, Vector3 direction)
    {
        float pushSpeed = consecutiveHitPushDistance / 0.3f;
        float elapsed = 0f;

        while (elapsed < 0.3f)
        {
            controller.Move(direction * pushSpeed * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    #endregion

    #region Attack Execution

    private void ExecuteAttack(GameObject target, float damageAmount)
    {
        if (target.TryGetComponent<PlayerBlockSystem>(out var blockSystem) && target.TryGetComponent<PlayerHealth>(out var health))
        {
            // Verificar si el ataque es bloqueado
            if (blockSystem.IsBlocking && blockSystem.CanBlockAttack(transform.position))
            {
                float remainingDamage = blockSystem.ProcessBlockedAttack(damageAmount);

                if (remainingDamage > 0f)
                {
                    health.TakeDamage(remainingDamage, false, AttackDamageType.Melee);
                }
                Debug.Log($"<color=red>[Laceratus] Ataque bloqueado por el jugador.</color>");
                return;
            }

            health.TakeDamage(damageAmount, false, AttackDamageType.Melee);
        }
    }

    #endregion

    #region Visual Feedback

    private void ChangeMaterialToFury()
    {
        if (enemyRenderer != null && furyStateMaterial != null)
        {
            enemyRenderer.material = furyStateMaterial;
        }
    }

    private void ChangeMaterialToNormal()
    {
        if (enemyRenderer != null && normalStateMaterial != null)
        {
            enemyRenderer.material = normalStateMaterial;
        }
        else if (enemyRenderer != null && originalMaterial != null)
        {
            enemyRenderer.material = originalMaterial;
        }
    }

    private IEnumerator ShowHitbox(GameObject hitbox)
    {
        if (hitbox != null)
        {
            hitbox.SetActive(true);
            yield return new WaitForSeconds(hitboxDisplayDuration);
            hitbox.SetActive(false);
        }
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.1f); 
        Gizmos.DrawSphere(transform.position, visionRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, visionRange);

        Vector3 forward = transform.forward;
        Vector3 leftBoundary = Quaternion.Euler(0, -visionConeAngle, 0) * forward;
        Vector3 rightBoundary = Quaternion.Euler(0, visionConeAngle, 0) * forward;

        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, leftBoundary * visionRange);
        Gizmos.DrawRay(transform.position, rightBoundary * visionRange);

        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, forward * visionRange);

        Gizmos.color = new Color(1f, 0f, 0f, 0.3f); 
        int segments = 20;
        Vector3 previousPoint = transform.position + leftBoundary * visionRange;

        for (int i = 1; i <= segments; i++)
        {
            float angle = Mathf.Lerp(-visionConeAngle, visionConeAngle, i / (float)segments);
            Vector3 direction = Quaternion.Euler(0, angle, 0) * forward;
            Vector3 point = transform.position + direction * visionRange;

            Gizmos.DrawLine(previousPoint, point);
            Gizmos.DrawLine(transform.position, point);

            previousPoint = point;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, normalAttackRange);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, furyAttackRange);

        if (Application.isPlaying && isInFury)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, furyJumpDistance);

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, furyJumpReducedDistance);
        }

        if (Application.isPlaying && !playerDetected)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, currentPatrolDirection * 3f);
        }

        if (Application.isPlaying && playerDetected && playerTransform != null)
        {
            Gizmos.color = isInFury ? Color.red : Color.yellow;
            Gizmos.DrawLine(transform.position, playerTransform.position);
        }
    }

    #endregion
}