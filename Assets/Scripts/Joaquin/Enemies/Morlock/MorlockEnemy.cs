using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(EnemyHealth))]
public partial class MorlockEnemy : MonoBehaviour
{
    private enum MorlockState { Patrol, Pursue1, Pursue2, Pursue3, Repositioning }
    private MorlockState currentState;

    [Header("Referencias")]
    [SerializeField] private MorlockStats stats;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;

    #region Variables

    [Header("Statistics (fallback si no hay MorlockStats)")]
    [Header("Health")]
    [SerializeField] private float health = 15f;
    [SerializeField] private float moveSpeed = 4f;
    [Tooltip("Radio dentro del cual Morlock detecta al jugador y empezará a perseguir/atacar.")]
    [SerializeField] private float detectionRadius = 50f;
    [SerializeField] private float teleportRange = 5f;

    [Header("Combat")]
    [SerializeField] private float fireRate = 1f;
    [SerializeField] private float projectileDamage = 1f;
    [SerializeField] private float projectileSpeed = 12f;
    [SerializeField] private int maxDamageIncrease = 2;
    [SerializeField] private float maxRangeForDamageIncrease = 6f;
    [SerializeField] private float maxDistanceForDamageStart = 20f;
    [SerializeField] private float attackRange = 50f;

    [Header("Patrol")]
    [Tooltip("Si se asignan waypoints, Morlock los recorrerá en bucle. Si no, hará roaming aleatorio en patrolRadius.")]
    [SerializeField] private Transform[] patrolWaypoints;
    //[SerializeField] private bool loopWaypoints = true;
    [SerializeField] private float patrolRadius = 8f; // usado si no hay waypoints
    [SerializeField] private float patrolIdleTime = 1.2f; // espera entre puntos

    [Header("Perseguir 1")]
    [SerializeField] private float p1_teleportCooldown = 2.5f;
    [SerializeField] private float p1_pursuitAdvanceDistance = 4f;
    [SerializeField] private float p1_pursuitLateralVariationMin = 3f;
    [SerializeField] private float p1_pursuitLateralVariationMax = 5f;

    [Header("Perseguir 2")]
    [Tooltip("Distancia para activar Perseguir2")]
    [SerializeField] private float p2_activationRadius = 5f;
    [SerializeField] private float p2_teleportCooldown = 2.5f;
    [SerializeField] private float p2_teleportRange = 5f;

    [Header("Perseguir 3")]
    [SerializeField] private float p3_teleportCooldown = 1.5f;
    [SerializeField] private float p3_teleportRange = 10f;

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip teleportSFX;
    [SerializeField] private AudioClip deathSFX;
    [SerializeField] private AudioClip shootSFX;

    #endregion

    private EnemyHealth enemyHealth;
    private NavMeshAgent agent;
    private Transform playerTransform;
    private Animator animator;
    private CharacterController playerCharacterController;

    private bool isDead = false;
    private Coroutine currentBehaviorCoroutine = null;
    private Coroutine shootCoroutine = null;

    private int currentWaypointIndex = 0;

    private void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        if (enemyHealth == null) ReportDebug("Componente EnemyHealth no encontrado en el enemigo.", 3);
        if (agent == null) ReportDebug("Componente NavMeshAgent no encontrado en el enemigo.", 3);
    }

    private void Start()
    {
        var playerGameObject = GameObject.FindGameObjectWithTag("Player");
        if (playerGameObject != null)
        {
            playerTransform = playerGameObject.transform;
            playerCharacterController = playerGameObject.GetComponent<CharacterController>();
        }

        InitializedEnemy();

        ChangeState(MorlockState.Patrol);
    }

    
    private void InitializedEnemy()
    {
        if (stats != null)
        {
            health = stats.health;
            moveSpeed = stats.moveSpeed;
            attackRange = stats.optimalAttackDistance;
            teleportRange = stats.teleportRange;
            fireRate = stats.fireRate;
            projectileDamage = stats.projectileDamage;
            projectileSpeed = stats.projectileSpeed;
            enemyHealth.SetMaxHealth(stats.health);
        }
        else
        {
            ReportDebug("MorlockStats no asignado. Usando valores por defecto.", 2);
        }

        enemyHealth.SetMaxHealth(health);

        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.stoppingDistance = attackRange;
            agent.updatePosition = true;
            agent.updateRotation = true;
            agent.isStopped = false;
        }
    }

    private void OnEnable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath += HandleEnemyDeath;
            enemyHealth.OnDamaged += HandleDamageTaken;
        }
    }

    private void OnDisable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleEnemyDeath;
            enemyHealth.OnDamaged -= HandleDamageTaken;
        }

        StopAllCoroutines();
    }

    private void OnDestroy()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleEnemyDeath;
            enemyHealth.OnDamaged -= HandleDamageTaken;
        }

        StopAllCoroutines();
    }

    private void HandleDamageTaken()
    {
        if (!isDead && currentState == MorlockState.Pursue2)
        {
            ChangeState(MorlockState.Pursue3);
        }
    }

    private void HandleEnemyDeath(GameObject enemy)
    {
        if (isDead || enemy != gameObject) return;
        isDead = true;

        ChangeState(MorlockState.Repositioning);
        StopAllCoroutines();

        if (agent != null)
        {
            agent.isStopped = true;
            agent.updatePosition = false;
            agent.updateRotation = false;
        }

        if (animator != null) animator.SetTrigger("Die");
        if (audioSource != null && deathSFX != null) audioSource.PlayOneShot(deathSFX);

        this.enabled = false;
    }

    private void Update()
    {
        if (isDead || playerTransform == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer > detectionRadius && (currentState != MorlockState.Patrol && currentState != MorlockState.Repositioning))
        {
            ChangeState(MorlockState.Patrol);
            return;
        }

        switch (currentState)
        {
            case MorlockState.Patrol:
                if (distanceToPlayer <= detectionRadius)
                {
                    ChangeState(MorlockState.Pursue1);
                }
                break;

            case MorlockState.Pursue1:
                if (distanceToPlayer <= p2_activationRadius)
                {
                    ChangeState(MorlockState.Pursue2);
                }
                break;

            case MorlockState.Pursue2:
                //if (distanceToPlayer > p2_activationRadius)
                //{
                //    ChangeState(MorlockState.Perseguir1);
                //}
                break;

            case MorlockState.Pursue3:
                break;
        }
    }

    /// <summary>
    /// Cambia a un nuevo estado, deteniendo la lógica del estado anterior.
    /// </summary>
    /// <param name="newState"> El estado para actualiza. Si es el mismo, no pasara nada </param>
    private void ChangeState(MorlockState newState)
    {
        if (currentState == newState && currentState != MorlockState.Repositioning) return;

        if (currentBehaviorCoroutine != null)
        {
            StopCoroutine(currentBehaviorCoroutine);
            currentBehaviorCoroutine = null;
        }

        if (shootCoroutine != null)
        {
            StopCoroutine(shootCoroutine);
            shootCoroutine = null;
        }

        currentState = newState;

        switch (currentState)
        {
            case MorlockState.Patrol:
                currentBehaviorCoroutine = StartCoroutine(PatrolRoutine());
                break;
            case MorlockState.Pursue1:
                currentBehaviorCoroutine = StartCoroutine(Pursuit1Routine());
                break;
            case MorlockState.Pursue2:
                currentBehaviorCoroutine = StartCoroutine(Pursuit2Routine());
                break;
            case MorlockState.Pursue3:
                currentBehaviorCoroutine = StartCoroutine(Pursuit3Routine());
                break;
            case MorlockState.Repositioning:
                break;
        }
    }

    #region Corutinas de Estado

    private IEnumerator PatrolRoutine()
    {
        while (currentState == MorlockState.Patrol)
        {
            Vector3 targetPosition;
            if (patrolWaypoints != null && patrolWaypoints.Length > 0)
            {
                targetPosition = patrolWaypoints[currentWaypointIndex].position;
                currentWaypointIndex = (currentWaypointIndex + 1) % patrolWaypoints.Length;
            }
            else
            {
                TryGetRandomPoint(transform.position, patrolRadius, out targetPosition);
            }

            TeleportToPosition(targetPosition);
            yield return new WaitForSeconds(patrolIdleTime);
        }
    }

    private IEnumerator Pursuit1Routine()
    {
        while (currentState == MorlockState.Pursue1)
        {
            Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
            Vector3 advancePosition = transform.position + directionToPlayer * p1_pursuitAdvanceDistance;

            Vector3 lateralDirection = Vector3.Cross(directionToPlayer, Vector3.up);
            float lateralOffset = Random.Range(p1_pursuitLateralVariationMin, p1_pursuitLateralVariationMax) * (Random.value > 0.5f ? 1f : -1f);

            Vector3 targetPosition = advancePosition + lateralDirection * lateralOffset;

            TeleportToPosition(targetPosition);
            StartShootCoroutine();

            yield return new WaitForSeconds(p1_teleportCooldown);
        }
    }

    private IEnumerator Pursuit2Routine()
    {
        int lastIndex = -1;

        while (currentState == MorlockState.Pursue2)
        {
            Vector3[] teleportPositions = new Vector3[4];
            float[] angles = { 0f, 90f, 180f, 270f };

            for (int i = 0; i < 4; i++)
            {
                Vector3 offset = Quaternion.Euler(0, angles[i], 0) * Vector3.forward * p2_teleportRange;
                teleportPositions[i] = playerTransform.position + offset;
            }

            int newIndex;
            do
            {
                newIndex = Random.Range(0, teleportPositions.Length);
            } while (newIndex == lastIndex);

            lastIndex = newIndex;
            Vector3 targetPosition = teleportPositions[newIndex];

            TeleportToPosition(targetPosition);
            StartShootCoroutine();

            yield return new WaitForSeconds(p2_teleportCooldown);
        }
    }

    private IEnumerator Pursuit3Routine()
    {
        int lastIndex = -1;

        while (currentState == MorlockState.Pursue3)
        {
            Vector3[] teleportPositions = new Vector3[4];
            float[] angles = { 45f, 135f, 225f, 315f };

            for (int i = 0; i < 4; i++)
            {
                Vector3 offset = Quaternion.Euler(0, angles[i], 0) * Vector3.forward * p3_teleportRange;
                teleportPositions[i] = playerTransform.position + offset;
            }

            int newIndex;
            do
            {
                newIndex = Random.Range(0, teleportPositions.Length);
            } while (newIndex == lastIndex);

            lastIndex = newIndex;
            Vector3 targetPosition = teleportPositions[newIndex];

            TeleportToPosition(targetPosition);
            StartShootCoroutine();

            yield return new WaitForSeconds(p3_teleportCooldown);
        }
    }

    #endregion

    #region Lógica Centralizada (Teletransporte y Disparo)

    private void TeleportToPosition(Vector3 targetPosition)
    {
        if (animator != null) animator.SetTrigger("Teleport");
        if (audioSource != null && teleportSFX != null) audioSource.PlayOneShot(teleportSFX);

        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPosition, out hit, 10f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            agent.Warp(hit.position);

            if (playerTransform != null && currentState != MorlockState.Patrol)
            {
                Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
                directionToPlayer.y = 0;
                if (directionToPlayer != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(directionToPlayer);
                }
            }
        }

        RegisterTeleportForDebug();
    }

    private void StartShootCoroutine()
    {
        if (shootCoroutine != null)
        {
            StopCoroutine(shootCoroutine);
        }
        shootCoroutine = StartCoroutine(ShootAfterDelayRoutine());
    }

    private IEnumerator ShootAfterDelayRoutine()
    {
        yield return new WaitForSeconds(fireRate);

        if (!isDead && currentState != MorlockState.Patrol && currentState != MorlockState.Repositioning)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            if (distanceToPlayer <= attackRange)
            {
                Shoot();
            }
        }

        shootCoroutine = null;
    }

    private void Shoot()
    {
        if (animator != null) animator.SetTrigger("Shoot");
        if (audioSource != null && shootSFX != null) audioSource.PlayOneShot(shootSFX);

        RegisterShootForDebug();

        Vector3 aimPoint = playerTransform.position;
        if (playerCharacterController != null)
        {
            aimPoint = CalculateInterceptPoint(playerTransform.position, playerCharacterController.velocity);
        }

        Vector3 directionToAim = (aimPoint - firePoint.position).normalized;
        firePoint.rotation = Quaternion.LookRotation(directionToAim);

        GameObject projectileObj = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        MorlockProjectile projectile = projectileObj.GetComponent<MorlockProjectile>();

        if (projectile != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            float calculatedDamage = CalculateDamageByDistance(distanceToPlayer);
            projectile.Initialize(projectileSpeed, calculatedDamage);
        }
    }

    #endregion

    #region Métodos de Ayuda (Cálculos)

    /// <summary>
    /// Calcula el daño del proyectil basado en la distancia al jugador.
    /// Escalado lineal: 6 unidades = 2 daño, 20+ unidades = 1 daño
    /// </summary>
    private float CalculateDamageByDistance(float distance)
    {
        if (distance <= maxRangeForDamageIncrease) return maxDamageIncrease;
        if (distance >= maxDistanceForDamageStart) return projectileDamage;

        float t = (distance - maxRangeForDamageIncrease) / (maxDistanceForDamageStart - maxRangeForDamageIncrease);
        return Mathf.Lerp(maxDamageIncrease, projectileDamage, t);
    }

    /// <summary>
    /// Calcula el punto de intercepción para un disparo, prediciendo el movimiento del objetivo.
    /// </summary>
    private Vector3 CalculateInterceptPoint(Vector3 targetPosition, Vector3 targetVelocity)
    {
        Vector3 directionToTarget = targetPosition - firePoint.position;
        float distance = directionToTarget.magnitude;

        if (projectileSpeed <= 0) return targetPosition;

        float timeToIntercept = distance / projectileSpeed;
        Vector3 futurePosition = targetPosition + targetVelocity * timeToIntercept;
        futurePosition += Random.insideUnitSphere * 0.5f;

        return futurePosition;
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
                Debug.Log($"[MorlockEnemy] {message}");
                break;
            case 2:
                Debug.LogWarning($"[MorlockEnemy] {message}");
                break;
            case 3:
                Debug.LogError($"[MorlockEnemy] {message}");
                break;
            default:
                Debug.Log($"[MorlockEnemy] {message}");
                break;
        }
    }
}