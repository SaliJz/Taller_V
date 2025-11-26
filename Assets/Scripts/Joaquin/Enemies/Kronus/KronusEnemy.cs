using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(EnemyHealth))]
public class KronusEnemy : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private KronusStats stats;
    [SerializeField] private Transform hitPoint;
    [SerializeField] private GameObject visualHit;
    [SerializeField] private GameObject groundIndicator;

    [Header("Statistics (fallback si no hay KronusStats)")]
    [Header("Health")]
    [SerializeField] private float health = 20f;

    [Header("Movement")]
    [Tooltip("Velocidad de movimiento por defecto si no se encuentra KronusStats.")]
    [SerializeField] private float moveSpeed = 3.5f; 
    [SerializeField] private float dashSpeedMultiplier = 2f;
    [SerializeField] private float dashDuration = 1f;
    [SerializeField] private float dashMaxDistance = 6f;

    [Header("Grounding Configuration")]
    [SerializeField] private LayerMask groundLayer = ~0;

    [Header("Attack")]

    [Header("Cooldown Variable")]
    [Tooltip("El cooldown actual se elegirá aleatoriamente entre estos tres valores al finalizar un ataque.")]
    [SerializeField] private float cooldownShort = 1.8f;
    [SerializeField] private float cooldownMedium = 2.0f;
    [SerializeField] private float cooldownLong = 2.2f;

    [SerializeField] private bool isPercentageDmg = false;
    [SerializeField] private float attackCycleCooldown = 2.5f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackDamagePercentage = 0.2f;
    [SerializeField] private float attackRadius = 1.5f;
    [SerializeField] private float preparationTime = 1f;
    [SerializeField] private float timeAfterAttack = 1f;
    [SerializeField] private float knockbackForce = 1f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Perception")]
    [Tooltip("Radio dentro del cual Kronus detecta al jugador y empezará a perseguir/atacar.")]
    [SerializeField] private float detectionRadius = 12f;
    [SerializeField] private float fieldOfViewAngle = 60f; 
    [SerializeField] private float dashActivationDistance = 9f;
    [SerializeField] private bool isUsingDetectionTimer = true;
    [SerializeField] private float detectionGracePeriod = 1.5f;

    [Header("Level 2 - Eco Persistente")]
    [Tooltip("Activa el comportamiento de Eco Persistente al golpear.")]
    [SerializeField] private bool isLevelTwo = false;
    [Tooltip("Prefab del efecto visual del Eco. Usado para el resplandor púrpura.")]
    [SerializeField] private GameObject echoVisualPrefab;
    [SerializeField] private float echoStartRadius = 2f;
    [SerializeField] private float echoEndRadius = 3f;
    [SerializeField] private float echoExpansionTime = 0.4f;
    [SerializeField] private float echoDurationAfterExpansion = 1.2f;
    [SerializeField] private float echoDamagePerSecond = 3f;
    [SerializeField] private float EchoTickRate = 4f;
    private float EchoDamagePerTick = 3f / 4f;

    [Header("Patrol")]
    [Tooltip("Si se asignan waypoints, Kronus los recorrerá en bucle. Si no, hará roaming aleatorio en patrolRadius.")]
    [SerializeField] private Transform[] patrolWaypoints;
    [SerializeField] private bool loopWaypoints = true;
    [SerializeField] private float patrolRadius = 8f;
    [SerializeField] private float patrolMoveSpeed = 2.5f;
    [SerializeField] private float patrolIdleTime = 1.2f;

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip dashSFX;
    [SerializeField] private AudioClip hammerSmashSFX;
    [SerializeField] private AudioClip deathSFX;
    [SerializeField] private AudioClip hitSFX;

    [Header("Debug Options")]
    [SerializeField] private bool showDetailsOptions = false;
    [SerializeField] private bool showGizmo = false;
    [SerializeField] private float gizmoDuration = 0.25f;

    private EnemyHealth enemyHealth;
    private NavMeshAgent agent;
    private Transform playerTransform;
    private PlayerHealth playerHealth;

    private bool isAttacking = false;
    private float attackTimer;

    private bool hasHitPlayerThisDash = false;
    private Coroutine dashCoroutine;

    private bool isPatrolWaiting = false;
    private int currentWaypointIndex = 0;

    private bool isAlertedByHit = false;

    private bool hasDetectedPlayer = false;
    private float detectionTimer = 0f;

    private List<GameObject> activeEchoes = new List<GameObject>();

    private Vector3 lastMoveDirection = Vector3.forward;
    private int animHashX;
    private int animHashY;
    private int animHashWalking;
    private int animHashDashing;

    private void Awake()
    {
        if (enemyHealth == null) enemyHealth = GetComponent<EnemyHealth>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponent<Animator>();

        animHashX = Animator.StringToHash("Xaxis");
        animHashY = Animator.StringToHash("Yaxis");
        animHashWalking = Animator.StringToHash("Walking");
        animHashDashing = Animator.StringToHash("Dashing");

        if (enemyHealth == null) ReportDebug("Falta EnemyHealth componente.", 3);
        if (agent == null) ReportDebug("Falta NavMeshAgent componente.", 3);
        if (animator != null) ReportDebug("Falta Animator componente.", 2);
    }

    private void Start()
    {
        if (visualHit != null) visualHit.SetActive(false);
        if (groundIndicator != null) groundIndicator.SetActive(false);

        var playerGameObject = GameObject.FindGameObjectWithTag("Player");
        playerTransform = playerGameObject ? playerGameObject.transform : null;
        if (playerTransform == null) ReportDebug("Jugador no encontrado en la escena.", 3);
        else playerTransform.TryGetComponent(out playerHealth);

        InitializedEnemy();

        if (agent != null)
        {
            agent.stoppingDistance = attackRadius;
            agent.updatePosition = true;
            agent.updateRotation = false;
            agent.isStopped = false;
        }

        SetNextPatrolDestination();
    }

    private void InitializedEnemy()
    {
        EchoDamagePerTick = echoDamagePerSecond / EchoTickRate;

        if (stats != null)
        {
            moveSpeed = stats.moveSpeed;
            dashSpeedMultiplier = stats.dashSpeedMultiplier;
            dashDuration = stats.dashDuration;
            dashMaxDistance = stats.dashMaxDistance;
            //attackCycleCooldown = stats.attackCycleCooldown;
            attackDamagePercentage = stats.attackDamagePercentage;
            attackDamage = stats.attackDamage;
            attackRadius = stats.attackRadius;
            preparationTime = stats.preparationTime;
            timeAfterAttack = stats.timeAfterAttack;
            knockbackForce = stats.knockbackForce;
            if (enemyHealth != null) enemyHealth.SetMaxHealth(stats.health);
        }
        else
        {
            ReportDebug("MorlockStats no asignado. Usando valores por defecto.", 2);
            if (enemyHealth != null) enemyHealth.SetMaxHealth(health);
        }

        if (agent != null) agent.speed = patrolMoveSpeed;
    }

    private void OnEnable()
    {
        if (enemyHealth != null) enemyHealth.OnDeath += HandleEnemyDeath;
        if (enemyHealth != null) enemyHealth.OnDamaged += AlertEnemy;
        if (groundIndicator != null) groundIndicator.SetActive(false);
        if (visualHit != null) visualHit.SetActive(false);
    }

    private void OnDisable()
    {
        if (enemyHealth != null) enemyHealth.OnDeath -= HandleEnemyDeath;
        if (enemyHealth != null) enemyHealth.OnDamaged -= AlertEnemy;
        if (groundIndicator != null) groundIndicator.SetActive(false);
        if (visualHit != null) visualHit.SetActive(false);
    }

    private void OnDestroy()
    {
        if (enemyHealth != null) enemyHealth.OnDeath -= HandleEnemyDeath;
        if (enemyHealth != null) enemyHealth.OnDamaged -= AlertEnemy;
        if (groundIndicator != null) Destroy(groundIndicator);
        if (visualHit != null) Destroy(visualHit);
    }

    public void AlertEnemy()
    {
        isAlertedByHit = true;
        hasDetectedPlayer = true;
        detectionTimer = 0f;
        ReportDebug("Kronus alertado por golpe, iniciando persecución.", 1);
    }

    public void ApplyRoomMultiplier(float damageMult, float speedMult)
    {
        attackDamage *= damageMult;

        moveSpeed *= speedMult;
        patrolMoveSpeed *= speedMult; 

        dashSpeedMultiplier *= speedMult;

        ReportDebug($"Multiplicadores de sala aplicados a Kronus. Daño x{damageMult}, Velocidad x{speedMult}. Nueva Velocidad: {moveSpeed:F2}", 1);
    }

    private void HandleEnemyDeath(GameObject enemy)
    {
        if (enemy != gameObject) return;

        isAttacking = false;
        if (dashCoroutine != null) StopCoroutine(dashCoroutine);

        for (int i = activeEchoes.Count - 1; i >= 0; i--)
        {
            if (activeEchoes[i] != null)
            {
                Destroy(activeEchoes[i]);
            }
        }
        activeEchoes.Clear();

        if (agent != null)
        {
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
                agent.updatePosition = false;
                agent.updateRotation = false;
            }
            else
            {
                agent.enabled = false;
            }
        }

        if (audioSource != null && deathSFX != null) audioSource.PlayOneShot(deathSFX);

        this.enabled = false;
    }

    private void Update()
    {
        if (!enabled) return;

        UpdateAnimationAndRotation();

        if (enemyHealth != null && enemyHealth.IsStunned)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            return;
        }

        if (playerTransform == null)
        {
            if (agent != null) agent.isStopped = true;
            PatrolUpdate();
            return;
        }

        Vector3 directionToPlayer = playerTransform.position - transform.position;
        directionToPlayer.y = 0;
        float distToPlayer = directionToPlayer.magnitude;

        bool isInDetectionRange = distToPlayer <= detectionRadius;
        float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer.normalized);
        bool isInFOV = angleToPlayer <= fieldOfViewAngle * 0.5f;

        // Detección visual actual
        bool canSeePlayerNow = isInDetectionRange && isInFOV;

        // Sistema de detección mejorado
        UpdateDetectionState(canSeePlayerNow);

        // Determinar si el jugador está detectado
        bool isPlayerDetected = hasDetectedPlayer || isAlertedByHit;

        if (isPlayerDetected)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh && agent.speed != moveSpeed)
            {
                agent.speed = moveSpeed;
            }

            if (!isAttacking)
            {
                attackTimer += Time.deltaTime;

                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.SetDestination(playerTransform.position);
                }

                if (attackTimer >= attackCycleCooldown && distToPlayer <= dashActivationDistance) 
                {
                    if (!isAttacking)
                    {
                        dashCoroutine = StartCoroutine(DashAttackRoutine());
                    }
                }
            }
        }
        else
        {
            // Ya no detecta al jugador, volver a patrulla
            if (isAlertedByHit)
            {
                isAlertedByHit = false;
                ReportDebug("Alerta por golpe finalizada, volviendo a patrullar.", 1);
            }
            PatrolUpdate();
        }
    }

    /// <summary>
    /// Actualiza el estado de detección del jugador con temporizador de gracia.
    /// </summary>
    /// <param name="canSeePlayerNow">Si el enemigo puede ver al jugador en este frame.</param>
    private void UpdateDetectionState(bool canSeePlayerNow)
    {
        if (canSeePlayerNow)
        {
            // El jugador está visible, resetear el temporizador
            if (!hasDetectedPlayer)
            {
                hasDetectedPlayer = true;
                ReportDebug("Kronus ha detectado al jugador visualmente.", 1);
            }
            detectionTimer = 0f;
        }
        else if (hasDetectedPlayer)
        {
            // El jugador no es visible pero fue detectado previamente
            if (isUsingDetectionTimer)
            {
                // Incrementar el temporizador de gracia
                detectionTimer += Time.deltaTime;

                if (detectionTimer >= detectionGracePeriod)
                {
                    // El temporizador ha expirado, perder detección del jugador
                    hasDetectedPlayer = false;
                    detectionTimer = 0f;
                    ReportDebug($"Kronus perdió al jugador tras {detectionGracePeriod}s sin detección visual.", 1);
                }
            }
            else
            {
                // Sin temporizador, mantener detección
                hasDetectedPlayer = true;
            }
        }
    }

    #region Animation & Rotation

    /// <summary>
    /// Maneja la rotación discreta (8 direcciones) y actualiza el Animator.
    /// </summary>
    private void UpdateAnimationAndRotation()
    {
        if (agent == null || animator == null) return;

        Vector3 velocity = Vector3.zero;

        // Si estamos usando NavMeshAgent, obtenemos su velocidad
        if (agent.enabled && !agent.isStopped)
        {
            velocity = agent.velocity;
        }

        bool isWalking = velocity.sqrMagnitude > 0.1f;
        animator.SetBool(animHashWalking, isWalking);

        if (isWalking)
        {
            Vector3 direction = velocity.normalized;
            direction.y = 0;

            if (direction != Vector3.zero)
            {
                // Snapping a 8 direcciones
                float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

                // Redondear al múltiplo más cercano de 45 grados
                float snappedAngle = Mathf.Round(angle / 45f) * 45f;

                // Aplicar rotación suave pero hacia el ángulo 'snappeado'
                Quaternion targetRotation = Quaternion.Euler(0, snappedAngle, 0);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 720f * Time.deltaTime);

                // Guardam la última dirección para cuando se detenga
                lastMoveDirection = direction;
            }
        }

        animator.SetFloat(animHashX, lastMoveDirection.x);
        animator.SetFloat(animHashY, lastMoveDirection.z);
    }

    #endregion

    #region Patrulla / Roaming

    /// <summary>
    /// Actualiza el comportamiento de patrulla o roaming.
    /// </summary>
    private void PatrolUpdate()
    {
        if (isAttacking) return;

        if (agent != null && agent.enabled && agent.isOnNavMesh && agent.speed != patrolMoveSpeed)
        {
            agent.speed = patrolMoveSpeed;
        }

        if (patrolWaypoints != null && patrolWaypoints.Length > 0)
        {
            if (agent != null && !agent.pathPending && !isPatrolWaiting)
            {
                if (agent.remainingDistance <= agent.stoppingDistance)
                {
                    StartCoroutine(WaitThenMoveToNextWaypoint(patrolIdleTime));
                }
            }
        }
        else
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh && !agent.pathPending && (agent.remainingDistance <= agent.stoppingDistance))
            {
                Vector3 roamPoint;
                if (TryGetRandomPoint(transform.position, patrolRadius, out roamPoint))
                {
                    agent.SetDestination(roamPoint);
                }
            }
        }
    }

    /// <summary>
    /// Espera un tiempo antes de avanzar al siguiente waypoint.
    /// </summary>
    /// <param name="wait">Tiempo a esperar en segundos.</param>
    private IEnumerator WaitThenMoveToNextWaypoint(float wait)
    {
        isPatrolWaiting = true;
        if (agent != null) agent.isStopped = true;
        yield return new WaitForSeconds(wait);

        if (agent != null) agent.isStopped = false;

        // avanzar índice
        if (patrolWaypoints != null && patrolWaypoints.Length > 0)
        {
            currentWaypointIndex++;
            if (currentWaypointIndex >= patrolWaypoints.Length)
            {
                if (loopWaypoints) currentWaypointIndex = 0;
                else
                {
                    // detener patrulla si no loopea
                    currentWaypointIndex = patrolWaypoints.Length - 1;
                    agent.isStopped = true;
                    isPatrolWaiting = false;
                    yield break;
                }
            }
            SetNextPatrolDestination();
        }

        isPatrolWaiting = false;
    }

    /// <summary>
    /// Define el siguiente destino de patrulla, ya sea un waypoint o un punto aleatorio.
    /// </summary>
    private void SetNextPatrolDestination()
    {
        if (patrolWaypoints != null && patrolWaypoints.Length > 0)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh && patrolWaypoints[currentWaypointIndex] != null)
            {
                agent.SetDestination(patrolWaypoints[currentWaypointIndex].position);
            }
        }
        else
        {
            Vector3 roamPoint;
            if (TryGetRandomPoint(transform.position, patrolRadius, out roamPoint) && agent != null)
            {
                if (agent != null && agent.enabled && agent.isOnNavMesh) agent.SetDestination(roamPoint);
            }
        }
    }

    /// <summary>
    /// Intenta obtener un punto válido sobre el NavMesh dentro de radio.
    /// </summary>
    /// <param name="center">Centro del círculo de búsqueda.</param>
    /// <param name="radius">Radio de búsqueda.</param>
    /// <param name="result">Punto encontrado (si retorna true).</param>
    /// <returns>True si se encontró un punto válido.</returns>
    private bool TryGetRandomPoint(Vector3 center, float radius, out Vector3 result)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 rand = center + Random.insideUnitSphere * radius;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(rand, out hit, 2.0f, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }
        result = center;
        return false;
    }

    #endregion

    #region Dash / Attack

    private IEnumerator DashAttackRoutine()
    {
        isAttacking = true;
        attackTimer = 0f;
        hasHitPlayerThisDash = false;

        ReportDebug("Kronus inicia ataque dash.", 1);

        if (animator != null) animator.SetBool(animHashDashing, true);

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.updatePosition = false;
            agent.updateRotation = false;
        }

        float prepTimer = 0f;
        while (prepTimer < preparationTime)
        {
            if (enemyHealth != null && enemyHealth.IsStunned)
            {
                isAttacking = false;
                if (animator != null) animator.SetBool(animHashDashing, false);
                if (agent != null && agent.enabled) agent.updatePosition = true;
                yield break; 
            }
            prepTimer += Time.deltaTime;

            if (playerTransform != null)
            {
                Vector3 dirToPlayer = (playerTransform.position - transform.position).normalized;
                dirToPlayer.y = 0;
                float angle = Mathf.Atan2(dirToPlayer.x, dirToPlayer.z) * Mathf.Rad2Deg;
                float snappedAngle = Mathf.Round(angle / 45f) * 45f;
                transform.rotation = Quaternion.Euler(0, snappedAngle, 0);
            }
            yield return null; // esperar al siguiente frame
        }

        if (audioSource != null && dashSFX != null) audioSource.PlayOneShot(dashSFX);

        Vector3 startPosition = transform.position;
        Vector3 dashTarget = playerTransform != null ? playerTransform.position : transform.position;
        dashTarget.y = startPosition.y;

        Vector3 direction = (dashTarget - startPosition);
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;
        direction.Normalize();

        float distanceToPlayer = Vector3.Distance(new Vector3(startPosition.x, 0, startPosition.z), new Vector3(dashTarget.x, 0, dashTarget.z));
        float dashDistance = Mathf.Min(distanceToPlayer, dashMaxDistance);

        Vector3 finalDashTarget = startPosition + direction * dashDistance;
        finalDashTarget.y = startPosition.y;

        // Validacion previa con NavMesh
        NavMeshHit edgeHit;
        if (NavMesh.Raycast(startPosition, finalDashTarget, out edgeHit, NavMesh.AllAreas))
        {
            finalDashTarget = edgeHit.position;
            finalDashTarget -= direction * 0.5f; // 0.5 Previos al edgeHit
            ReportDebug("Dash acortado: Se detectó fin del NavMesh en la trayectoria.", 1);
        }

        float elapsed = 0f;
        float interruptDistance = attackRadius * 1.5f;

        // Dash por duración
        while (elapsed < dashDuration)
        {
            if (enemyHealth != null && enemyHealth.IsStunned)
            {
                isAttacking = false;
                if (animator != null) animator.SetBool(animHashDashing, false);
                if (agent != null && agent.enabled)
                {
                    agent.Warp(transform.position);
                    agent.updatePosition = true;
                }
                yield break;
            }

            float delta = Time.deltaTime;
            elapsed += delta;

            Vector3 currentPos = transform.position;
            Vector3 toTarget = finalDashTarget - currentPos;
            toTarget.y = 0f;
            float remaining = toTarget.magnitude;

            if (remaining <= 0.1f) break;

            Vector3 dashDir = toTarget.normalized;
            float step = moveSpeed * dashSpeedMultiplier * delta;
            float moveAmount = Mathf.Min(step, remaining);

            // Calcular nueva posición manteniendo Y
            Vector3 newPosition = currentPos + dashDir * moveAmount;

            if (!CheckGroundAtPosition(newPosition))
            {
                ReportDebug("Dash interrumpido: Suelo perdido durante el desplazamiento.", 2);
                break; // Detener el dash inmediatamente y pasar al ataque
            }

            // Muestrear NavMesh para obtener la Y correcta
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(newPosition, out navHit, 2f, NavMesh.AllAreas))
            {
                newPosition.y = navHit.position.y;
            }

            transform.position = newPosition;

            float angle = Mathf.Atan2(dashDir.x, dashDir.z) * Mathf.Rad2Deg;
            float snappedAngle = Mathf.Round(angle / 45f) * 45f;
            transform.rotation = Quaternion.Euler(0, snappedAngle, 0);

            if (animator != null)
            {
                animator.SetFloat(animHashX, dashDir.x);
                animator.SetFloat(animHashY, dashDir.z);
            }

            // Interrupción por proximidad al jugador
            if (playerTransform != null)
            {
                float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
                if (distToPlayer <= interruptDistance)
                {
                    ReportDebug("Dash interrumpido: jugador detectado en la trayectoria.", 1);
                    break;
                }
            }

            yield return null;
        }

        if (animator != null) animator.SetBool(animHashDashing, false);

        if (agent != null && agent.enabled)
        {
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(transform.position, out navHit, 2f, NavMesh.AllAreas))
            {
                agent.Warp(navHit.position);
            }

            agent.updatePosition = true;
        }

        if (enemyHealth != null && enemyHealth.IsStunned)
        {
            isAttacking = false;
            yield break;
        }

        ReportDebug("Kronus preparando martillazo.", 1);

        if (groundIndicator != null)
        {
            groundIndicator.transform.SetParent(null, true);

            Vector3 sampleBase = (hitPoint != null) ? hitPoint.position : transform.position;
            Vector3 indicatorPos = sampleBase;
            NavMeshHit navHit;

            if (NavMesh.SamplePosition(sampleBase, out navHit, 3f, NavMesh.AllAreas))
            {
                indicatorPos = navHit.position;
                indicatorPos.y += 0.01f;
            }
            else
            {
                RaycastHit hit;
                if (Physics.Raycast(sampleBase + Vector3.up * 3f, Vector3.down, out hit, 6f, groundLayer))
                {
                    indicatorPos.y = hit.point.y + 0.01f;
                }
            }

            Collider indCol = groundIndicator.GetComponent<Collider>();
            if (indCol != null) indCol.enabled = false;

            groundIndicator.transform.position = indicatorPos;
            groundIndicator.transform.localScale = new Vector3(attackRadius * 2f, 0.025f, attackRadius * 2f);
            groundIndicator.SetActive(true);
        }

        yield return new WaitForSeconds(timeAfterAttack);

        if (groundIndicator != null) groundIndicator.SetActive(false);

        if (audioSource != null && hammerSmashSFX != null) audioSource.PlayOneShot(hammerSmashSFX);

        PerformHammerSmash();

        if (agent != null && agent.enabled)
        {
            agent.isStopped = false;

            if (playerTransform != null && agent.isOnNavMesh)
            {
                agent.SetDestination(playerTransform.position);
            }
        }

        // Elegir nuevo cooldown aleatorio
        float[] cooldownOptions = { cooldownShort, cooldownMedium, cooldownLong };
        attackCycleCooldown = cooldownOptions[Random.Range(0, cooldownOptions.Length)];

        ReportDebug("Kronus finaliza ataque dash y entra en recuperación de 2s.", 1);
        isAttacking = false;
    }

    public void PerformHammerSmash()
    {
        if (hasHitPlayerThisDash) return;

        if (hitPoint == null || playerHealth == null)
        {
            ReportDebug("PerformHammerSmash: falta hitPoint o playerHealth.", 2);
            return;
        }

        if (visualHit != null) visualHit.SetActive(true);

        Collider[] hitPlayer = Physics.OverlapSphere(hitPoint.position, attackRadius, playerLayer);

        foreach (Collider hit in hitPlayer)
        {
            if (playerHealth != null)
            {
                var hitTransform = hit.transform;
                //bool isCritical;
                float damage;

                if (isPercentageDmg) damage = playerHealth.MaxHealth * attackDamagePercentage;
                else damage = attackDamage;

                // Calcular daño con sistema de críticos
                //float damageToApply = CriticalHitSystem.CalculateDamage(damage, transform, hitTransform, out isCritical);

                if (audioSource != null && hitSFX != null) audioSource.PlayOneShot(hitSFX);

                ExecuteAttack(hit.gameObject, damage);

                // Aplicar empuje
                ApplyKnockback(hitTransform);

                ReportDebug($"Kronus atacó al jugador por {damage} de daño.", 1);

                hasHitPlayerThisDash = true;

                if (isLevelTwo)
                {
                    StartCoroutine(PersistentEchoRoutine(hitPoint.position));
                }

                break; // Solo golpear un jugador
            }
        }

        StartCoroutine(ShowGizmoCoroutine());
    }

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

                return;
            }

            health.TakeDamage(damageAmount, false, AttackDamageType.Melee);
        }
    }

    private void ApplyKnockback(Transform target)
    {
        // Calcula la dirección del empuje (desde Kronus hacia el jugador)
        Vector3 knockbackDirection = (target.position - transform.position).normalized;
        knockbackDirection.y = 0f; // Mantener en el plano horizontal

        // Aplica el empuje
        CharacterController cc = target.GetComponent<CharacterController>();
        Rigidbody rb = target.GetComponent<Rigidbody>();

        if (cc != null)
        {
            // Si el jugador usa CharacterController
            StartCoroutine(ApplyKnockbackOverTime(cc, knockbackDirection * knockbackForce));
        }
        else if (rb != null)
        {
            // Si el jugador usa Rigidbody
            rb.AddForce(knockbackDirection * knockbackForce * 10f, ForceMode.Impulse);
        }

        ReportDebug($"Empuje aplicado al jugador en dirección {knockbackDirection}", 1);
    }

    private IEnumerator ApplyKnockbackOverTime(CharacterController cc, Vector3 knockbackVelocity)
    {
        float duration = 0.2f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (cc != null && cc.enabled)
            {
                cc.Move(knockbackVelocity * Time.deltaTime);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator ShowGizmoCoroutine()
    {
        Vector3 originalScale = visualHit.transform.localScale;

        if (visualHit != null && hitPoint != null)
        {
            if (isLevelTwo)
            {
                ReportDebug("Resplandor púrpura del martillo activado.", 1);
            }

            visualHit.transform.localScale = Vector3.one * attackRadius * 2f;
            visualHit.SetActive(true);
        }
        yield return new WaitForSeconds(gizmoDuration);

        if (visualHit != null && hitPoint != null)
        {
            visualHit.SetActive(false);
            visualHit.transform.localScale = originalScale;
        }
    }

    /// <summary>
    /// Valida y corrige la posición de Kronus si está debajo del terreno o fuera del NavMesh.
    /// </summary>
    private void ValidatePositionOnNavMesh()
    {
        if (agent == null) return;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 2f, NavMesh.AllAreas))
        {
            // Posición válida, mantener
            transform.position = hit.position;
        }
        else
        {
            // Fuera del NavMesh, intentar encontrar posición cercana
            ReportDebug("Kronus fuera del NavMesh, buscando posición válida...", 2);

            for (int i = 0; i < 8; i++)
            {
                Vector3 randomOffset = Random.insideUnitSphere * 2f;
                if (NavMesh.SamplePosition(transform.position + randomOffset, out hit, 2f, NavMesh.AllAreas))
                {
                    transform.position = hit.position;
                    ReportDebug("Kronus reposicionado en NavMesh válido.", 1);
                    return;
                }
            }

            ReportDebug("No se encontró posición válida en NavMesh. Kronus puede estar atrapado.", 3);
        }
    }

    #endregion

    #region Level 2 - Eco Persistente

    private IEnumerator PersistentEchoRoutine(Vector3 center)
    {
        if (playerTransform == null || playerHealth == null) yield break;

        ReportDebug("Eco Persistente activado.", 1);

        GameObject currentEchoInstance = null;
        if (echoVisualPrefab != null)
        {
            currentEchoInstance = Instantiate(echoVisualPrefab, center, Quaternion.identity);
            currentEchoInstance.transform.localScale = Vector3.zero;
            activeEchoes.Add(currentEchoInstance);
        }

        // Ajustar la posición Y para estar sobre el NavMesh/suelo
        NavMeshHit navHit;
        if (NavMesh.SamplePosition(center, out navHit, 2f, NavMesh.AllAreas))
        {
            center = navHit.position;
            center.y += 0.01f; // Pequeño offset para renderizado
        }

        // Variables de tiempo y radio
        float elapsed = 0f;
        float damageTickInterval = 1f / EchoTickRate;
        float tickTimer = 0f;

        // Activar tracking para Gizmo
        //isEchoActive = true;
        //currentEchoCenter = center;

        // Fase de Expansión (0.4s)
        while (elapsed < echoExpansionTime)
        {
            if (currentEchoInstance == null) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / echoExpansionTime);
            float currentRadius = Mathf.Lerp(echoStartRadius, echoEndRadius, t);

            currentEchoInstance.transform.localScale = new Vector3(currentRadius * 2f, 0.01f, currentRadius * 2f);
            currentEchoInstance.transform.position = center;

            yield return null;
        }

        float finalRadius = echoEndRadius;

        // Fase de Duración con Daño (1.2s)
        float damagePhaseElapsed = 0f;
        while (damagePhaseElapsed < echoDurationAfterExpansion)
        {
            if (currentEchoInstance == null) yield break;

            float delta = Time.deltaTime;
            damagePhaseElapsed += delta;
            tickTimer += delta;

            // Aplicar daño por tics
            if (tickTimer >= damageTickInterval)
            {
                // Verificar si el jugador está en el área
                if (playerTransform != null)
                {
                    Vector3 playerPos = playerTransform.position;
                    // Proyección horizontal para la verificación de distancia
                    Vector3 echoCenterFlat = new Vector3(center.x, playerPos.y, center.z);

                    if (Vector3.Distance(playerPos, echoCenterFlat) <= finalRadius)
                    {
                        // Repartir los 3 de daño por segundo en 4 tics (0.75 por tic)
                        playerHealth.TakeDamage(EchoDamagePerTick);
                        ReportDebug($"Eco golpea al jugador por {EchoDamagePerTick:F2} de daño (Tick).", 1);
                    }
                }
                tickTimer -= damageTickInterval; // Restar el intervalo para evitar acumulaciones
            }

            yield return null;
        }

        // Limpieza
        ReportDebug("Eco Persistente finalizado.", 1);
        //isEchoActive = false;

        if (currentEchoInstance != null)
        {
            activeEchoes.Remove(currentEchoInstance);
            Destroy(currentEchoInstance);
        }
    }

    #endregion

    /// <summary>
    /// Lanza un raycast hacia abajo desde una posición ligeramente elevada para confirmar suelo físico.
    /// </summary>
    private bool CheckGroundAtPosition(Vector3 targetPos)
    {
        Vector3 origin = targetPos + Vector3.up * 0.5f;

        // Debug visual en el editor
        if (showGizmo) Debug.DrawRay(origin, Vector3.down * 3.5f, Color.magenta);

        return Physics.Raycast(origin, Vector3.down, 1.5f, groundLayer);
    }

    private void OnDrawGizmos()
    {
        if (!showGizmo) return;

        if (hitPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(hitPoint.position, attackRadius);
        }

        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, transform.forward * 2f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        if (fieldOfViewAngle < 180f)
        {
            Vector3 forward = transform.forward * detectionRadius;
            float halfAngle = fieldOfViewAngle * 0.5f;

            Quaternion leftRayRotation = Quaternion.AngleAxis(-halfAngle, Vector3.up);
            Quaternion rightRayRotation = Quaternion.AngleAxis(halfAngle, Vector3.up);

            Vector3 leftRayDirection = leftRayRotation * forward;
            Vector3 rightRayDirection = rightRayRotation * forward;

            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, leftRayDirection);
            Gizmos.DrawRay(transform.position, rightRayDirection);
        }

        Gizmos.color = new Color(1f, 0.5f, 0f, 1f);
        Gizmos.DrawWireSphere(transform.position, patrolRadius);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, dashActivationDistance);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, dashMaxDistance);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    /// <summary> 
    /// Función de depuración para reportar mensajes en la consola de Unity. 
    /// </summary> 
    /// <<param name="message">Mensaje a reportar.</param> >
    /// <param name="reportPriorityLevel">Nivel de prioridad: Debug, Warning, Error.</param>
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[KronusEnemy] {message}");
                break;
            case 2:
                Debug.LogWarning($"[KronusEnemy] {message}");
                break;
            case 3:
                Debug.LogError($"[KronusEnemy] {message}");
                break;
            default:
                Debug.Log($"[KronusEnemy] {message}");
                break;
        }
    }
}