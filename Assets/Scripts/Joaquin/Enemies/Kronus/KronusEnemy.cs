using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent), typeof(EnemyHealth))]
public class KronusEnemy : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private KronusStats stats;
    [SerializeField] private Transform hitPoint;
    [SerializeField] private GameObject visualHit;

    [Header("Statistics (fallback si no hay KronusStats)")]
    [Header("Health")]
    [SerializeField] private float health = 10f;

    [Header("Movement")]
    [Tooltip("Velocidad de movimiento por defecto si no se encuentra KronusStats.")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float dashSpeedMultiplier = 2f;
    [SerializeField] private float dashDuration = 1f;

    [Header("Attack")]
    [SerializeField] private float attackCycleCooldown = 5f;
    [SerializeField] private float attackDamagePercentage = 0.2f;
    [SerializeField] private float attackRadius = 2f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Perception")]
    [Tooltip("Radio dentro del cual Kronus detecta al jugador y empezará a perseguir/atacar.")]
    [SerializeField] private float detectionRadius = 12f;

    [Header("Patrol")]
    [Tooltip("Si se asignan waypoints, Kronus los recorrerá en bucle. Si no, hará roaming aleatorio en patrolRadius.")]
    [SerializeField] private Transform[] patrolWaypoints;
    [SerializeField] private bool loopWaypoints = true;
    [SerializeField] private float patrolRadius = 8f; // usado si no hay waypoints
    [SerializeField] private float patrolIdleTime = 1.2f; // espera entre puntos

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip dashAttackSFX;
    [SerializeField] private AudioClip deathSFX;
    [SerializeField] private AudioClip hitSFX;

    [Header("Debug Options")]
    [SerializeField] private bool showDetailsOptions = false;

    private EnemyHealth enemyHealth;
    private NavMeshAgent agent;
    private Transform playerTransform;
    private PlayerHealth playerHealth;
    private Animator animator;

    private bool isAttacking = false;
    private float attackTimer;

    private bool hasHitPlayerThisDash = false;
    private Coroutine dashCoroutine;

    private bool isPatrolWaiting = false;
    private int currentWaypointIndex = 0;

    private void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        if (enemyHealth == null) ReportDebug("Falta EnemyHealth componente.", 3);
        if (agent == null) ReportDebug("Falta NavMeshAgent componente.", 3);
    }

    private void Start()
    {
        visualHit.SetActive(false);

        var playerGameObject = GameObject.FindGameObjectWithTag("Player");
        playerTransform = playerGameObject ? playerGameObject.transform : null;
        if (playerTransform == null) ReportDebug("Jugador no encontrado en la escena.", 3);
        else playerTransform.TryGetComponent(out playerHealth);

        InitializedEnemy();

        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.stoppingDistance = attackRadius;
            agent.updatePosition = true;
            agent.updateRotation = true;
            agent.isStopped = false;
        }

        SetNextPatrolDestination();
    }

    private void InitializedEnemy()
    {
        if (stats != null)
        {
            moveSpeed = stats.moveSpeed;
            dashSpeedMultiplier = stats.dashSpeedMultiplier;
            dashDuration = stats.dashDuration;
            attackCycleCooldown = stats.attackCycleCooldown;
            attackDamagePercentage = stats.attackDamagePercentage;
            attackRadius = stats.attackRadius;
            if (enemyHealth != null) enemyHealth.SetMaxHealth(stats.health);
        }
        else
        {
            ReportDebug("MorlockStats no asignado. Usando valores por defecto.", 2);
            if (enemyHealth != null) enemyHealth.SetMaxHealth(health);
        }
    }

    private void OnEnable()
    {
        if (enemyHealth != null) enemyHealth.OnDeath += HandleEnemyDeath;
    }

    private void OnDisable()
    {
        if (enemyHealth != null) enemyHealth.OnDeath -= HandleEnemyDeath;
        if (dashCoroutine != null) StopCoroutine(dashCoroutine);
    }

    private void HandleEnemyDeath(GameObject enemy)
    {
        if (enemy != gameObject) return;

        isAttacking = false;
        if (dashCoroutine != null) StopCoroutine(dashCoroutine);

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
        if (!enabled) return;

        if (playerTransform == null)
        {
            if (agent != null) agent.isStopped = true;
            PatrolUpdate();
            return;
        }

        float sqrDistToPlayer = (playerTransform.position - transform.position).sqrMagnitude;
        float detectionSqr = detectionRadius * detectionRadius;

        if (sqrDistToPlayer <= detectionSqr)
        {

            if (!isAttacking)
            {
                attackTimer += Time.deltaTime;

                if (agent != null)
                {
                    agent.isStopped = false;
                    agent.SetDestination(playerTransform.position);
                }

                if (attackTimer >= attackCycleCooldown)
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
            PatrolUpdate();
        }
    }

    #region Patrulla / Roaming

    /// <summary>
    /// Actualiza el comportamiento de patrulla o roaming.
    /// </summary>
    private void PatrolUpdate()
    {
        if (isAttacking) return;

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
            if (agent != null && !agent.pathPending && (agent.remainingDistance <= agent.stoppingDistance))
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
            if (agent != null && patrolWaypoints[currentWaypointIndex] != null)
            {
                agent.SetDestination(patrolWaypoints[currentWaypointIndex].position);
            }
        }
        else
        {
            Vector3 roamPoint;
            if (TryGetRandomPoint(transform.position, patrolRadius, out roamPoint) && agent != null)
            {
                agent.SetDestination(roamPoint);
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

        if (agent != null)
        {
            agent.isStopped = true;
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.enabled = false;
        }

        if (animator != null) animator.SetTrigger("StartDash");
        if (audioSource != null && dashAttackSFX != null) audioSource.PlayOneShot(dashAttackSFX);

        Vector3 dashTarget = playerTransform != null ? playerTransform.position : transform.position;
        dashTarget.y = transform.position.y;

        float startTime = Time.time;
        float endTime = startTime + dashDuration;

        while (Time.time < endTime)
        {
            if (dashTarget == null) break;

            transform.position = Vector3.MoveTowards(transform.position, dashTarget, moveSpeed * dashSpeedMultiplier * Time.deltaTime);
            
            Vector3 direction = (dashTarget - transform.position);

            direction.y = 0f;

            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
            }

            PerformHitDetection();

            yield return null;
        }

        if (visualHit.activeSelf) visualHit.SetActive(false);

        if (animator != null) animator.SetTrigger("Attack");

        PerformHitDetection();

        if (agent != null)
        {
            agent.enabled = true;
            agent.isStopped = false;
            agent.updatePosition = true;
            agent.updateRotation = true;
            agent.Warp(transform.position);
            agent.SetDestination(playerTransform != null ? playerTransform.position : transform.position);
        }

        if (visualHit.activeSelf) visualHit.SetActive(false);

        yield return new WaitForSeconds(2.5f);

        ReportDebug("Kronus finaliza ataque dash.", 1);

        isAttacking = false;
        if (visualHit.activeSelf) visualHit.SetActive(false);
    }

    public void PerformHitDetection()
    {
        if (hasHitPlayerThisDash) return;

        if (hitPoint == null || playerHealth == null)
        {
            ReportDebug("PerformHitDetection: falta hitPoint o playerHealth.", 2);
            return;
        }

        visualHit.SetActive(true);

        Collider[] hitPlayer = Physics.OverlapSphere(hitPoint.position, attackRadius, playerLayer);

        float baseDamage = playerHealth.MaxHealth * attackDamagePercentage;

        foreach (Collider collider in hitPlayer)
        {
            if (playerHealth != null)
            {
                var hitTransform = collider.transform;
                bool isCritical;
                float damage = playerHealth.MaxHealth * attackDamagePercentage;
                float damageToApply = CriticalHitSystem.CalculateDamage(damage, transform, hitTransform, out isCritical);
                
                if (audioSource != null && hitSFX != null) audioSource.PlayOneShot(hitSFX);
                playerHealth.TakeDamage(damageToApply);

                ReportDebug($"Kronus atacó al jugador por {damageToApply} de daño. Crítico: {isCritical}", 1);

                hasHitPlayerThisDash = true;
            }
        }

    }

    #endregion

    private void OnDrawGizmos()
    {
        if (hitPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(hitPoint.position, attackRadius);
        }

        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, transform.forward * 2f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = new Color(1f, 0.5f, 0f, 1f);
        Gizmos.DrawWireSphere(transform.position, patrolRadius);
    }

    private void OnGUI()
    {
        if (showDetailsOptions)
        {
            GUI.Label(new Rect(10, 10, 300, 20), $"Kronus Enemy Details:");
            GUI.Label(new Rect(10, 30, 300, 20), $"- Move Speed: {moveSpeed}");
            GUI.Label(new Rect(10, 50, 300, 20), $"- Dash Speed Multiplier: {dashSpeedMultiplier}");
            GUI.Label(new Rect(10, 70, 300, 20), $"- Dash Duration: {dashDuration}");
            GUI.Label(new Rect(10, 90, 300, 20), $"- Attack Cycle Cooldown: {attackCycleCooldown}");
            GUI.Label(new Rect(10, 110, 300, 20), $"- Attack Damage Percentage: {attackDamagePercentage * 100}%");
            GUI.Label(new Rect(10, 130, 300, 20), $"- Attack Radius: {attackRadius}");
            if (enemyHealth != null)
            {
                GUI.Label(new Rect(10, 150, 300, 20), $"- Current Health: {enemyHealth.CurrentHealth}/{enemyHealth.MaxHealth}");
            }
            GUI.Label(new Rect(10, 170, 300, 20), $"- Time until next attack: {Mathf.Max(0, attackCycleCooldown - attackTimer):F2} seconds");
        }
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