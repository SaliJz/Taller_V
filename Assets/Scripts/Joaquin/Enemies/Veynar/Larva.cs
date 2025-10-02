using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(EnemyHealth))]
public class Larva : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float speed = 12f;
    [SerializeField] private float frontalDamage = 2.5f;
    [SerializeField] private float backDamage = 5f;
    [SerializeField] private float lifeTime = 10f;
    [SerializeField] private float deathCooldown = 0.5f;

    [Header("Detección / Movimiento")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private float stoppingBuffer = 0.5f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackCooldown = 0.5f;

    [Header("Agent tuning (tunea en Inspector)")]
    [Tooltip("Cadencia mínima en segundos para re-evaluar SetDestination (reduce CPU y jitter).")]
    [SerializeField] private float destinationUpdateInterval = 0.18f;
    [Tooltip("Multiplicador para acceleration (accel = speed * accelMultiplier).")]
    [SerializeField] private float accelMultiplier = 3f;
    [SerializeField] private float minAcceleration = 8f;
    [SerializeField] private float angularSpeed = 120f;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    private NavMeshAgent agent;
    private Transform player;
    private PlayerHealth playerHealth;
    private EnemyHealth enemyHealth;

    private float lifeTimer = 0f;
    private float lastAttackTime = -999f;
    private float lastDestinationTime = -999f;
    private bool initialized = false;

    private void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();

        var rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (enemyHealth != null) enemyHealth.CanHealPlayer = false;
        if (enemyHealth != null) enemyHealth.CanDestroy = true;
        if (enemyHealth != null) enemyHealth.DeathCooldown = deathCooldown;

        agent = GetComponent<NavMeshAgent>();
        if (agent != null) ConfigureAgentFromParams();
    }

    private void Update()
    {
        if (!initialized) return;

        if (player == null || enemyHealth == null || enemyHealth.IsDead) return;

        float distance = Vector3.Distance(transform.position, player.position);
        if (distance <= attackRange && Time.time >= lastAttackTime + attackCooldown)
        {
            TryAttack();
        }
    }

    private void OnEnable()
    {
        if (enemyHealth != null) enemyHealth.OnDeath += HandleEnemyDeath;
    }

    private void OnDisable()
    {
        if (enemyHealth != null) enemyHealth.OnDeath -= HandleEnemyDeath;
    }

    private void OnDestroy()
    {
        if (enemyHealth != null) enemyHealth.OnDeath -= HandleEnemyDeath;
    }

    private void HandleEnemyDeath(GameObject enemy)
    {
        if (enemy != gameObject) return;

        StopAllCoroutines();
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    private void ConfigureAgentFromParams()
    {
        if (agent == null) return;

        agent.speed = speed;
        agent.acceleration = Mathf.Max(minAcceleration, speed * accelMultiplier);
        agent.angularSpeed = angularSpeed;
        agent.updatePosition = true;
        agent.updateRotation = true;

        agent.stoppingDistance = attackRange + stoppingBuffer;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
        agent.autoRepath = true;
        agent.enabled = true;
    }

    /// <summary>
    /// Inicializa la larva con referencia al jugador.
    /// </summary>
    public void Initialize(Transform playerTransform)
    {
        player = playerTransform ?? GameObject.FindGameObjectWithTag("Player")?.transform;
        playerHealth = player ? player.GetComponent<PlayerHealth>() : null;

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null && !agent.isOnNavMesh)
        {
            ReportDebug("NavMeshAgent no está en el NavMesh. Intentando colocar...", 2);
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, 2f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
                agent.Warp(hit.position);
            }
            else
            {
                ReportDebug("No se pudo colocar en el NavMesh. Destruyendo larva.", 3);
                if (enemyHealth != null && !enemyHealth.IsDead) enemyHealth.Die();
                return;
            }
        }

        if (agent != null) ConfigureAgentFromParams();

        StopAllCoroutines();

        lifeTimer = 0f;
        lastAttackTime = -999f;
        lastDestinationTime = -999f;

        StartCoroutine(LifeCycle());
        initialized = true;
    }

    private IEnumerator LifeCycle()
    {
        while (lifeTimer < lifeTime)
        {
            if (enemyHealth == null || enemyHealth.IsDead) yield break;

            lifeTimer += Time.deltaTime;

            if (player == null)
            {
                player = GameObject.FindGameObjectWithTag("Player")?.transform;
                playerHealth = player ? player.GetComponent<PlayerHealth>() : null;
            }

            if (player != null && agent != null && agent.isOnNavMesh)
            {
                float distance = Vector3.Distance(transform.position, player.position);

                if (distance > attackRange + stoppingBuffer)
                {
                    if (agent.isStopped) agent.isStopped = false;

                    if (Time.time >= lastDestinationTime + destinationUpdateInterval)
                    {
                        Vector3 currentTarget = agent.hasPath ? agent.destination : Vector3.positiveInfinity;
                        if (!agent.hasPath || Vector3.Distance(currentTarget, player.position) > 0.35f)
                        {
                            agent.SetDestination(player.position);
                            lastDestinationTime = Time.time;
                            //ReportDebug($"[Larva] SetDestination -> {player.position} (speed={agent.speed}, accel={agent.acceleration})", 1);
                        }
                    }
                }
                else
                {
                    agent.ResetPath();
                    agent.isStopped = true;
                    TryAttack();
                }
            }

            yield return null;
        }

        if (enemyHealth != null && !enemyHealth.IsDead) enemyHealth.Die();
    }

    private void TryAttack()
    {
        if (player == null || enemyHealth == null || enemyHealth.IsDead) return;

        if (Time.time < lastAttackTime + attackCooldown) return;

        Vector3 fromPlayerToLarva = (transform.position - player.position).normalized;
        float dotProduct = Vector3.Dot(player.forward, fromPlayerToLarva);
        bool isBackHit = dotProduct < 0f;
        float finalDamage = isBackHit ? backDamage : frontalDamage;

        bool damageApplied = false;

        var damageable = player.GetComponent<IDamageable>();
        if (damageable != null)
        {
            try
            {
                damageable.TakeDamage(finalDamage, isBackHit);
                damageApplied = true;
            }
            catch
            {
                try
                {
                    damageable.TakeDamage(Mathf.RoundToInt(finalDamage), isBackHit);
                    damageApplied = true;
                }
                catch { damageApplied = false; }
            }
        }
        else if (playerHealth != null)
        {
            try
            {
                playerHealth.TakeDamage(finalDamage, isBackHit);
                damageApplied = true;
            }
            catch
            {
                try
                {
                    playerHealth.TakeDamage(Mathf.RoundToInt(finalDamage), isBackHit);
                    damageApplied = true;
                }
                catch { damageApplied = false; }
            }
        }

        if (damageApplied)
        {
            ReportDebug($"[Larva] Impacto a {player.name}: daño={finalDamage} backHit={isBackHit}", 1);
        }
        else
        {
            ReportDebug($"[Larva] No se pudo aplicar daño a {player.name} (no IDamageable/PlayerHealth compatible).", 2);
        }

        lastAttackTime = Time.time;

        if (!enemyHealth.IsDead) enemyHealth.Die();
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange + stoppingBuffer);
        if (agent != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.5f);
        }
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    /// <summary> 
    /// Función de depuración para reportar mensajes en la consola de Unity. 
    /// </summary> 
    /// <param name="message">Mensaje a reportar.</param>
    /// <param name="reportPriorityLevel">Nivel de prioridad: Debug, Warning, Error.</param>
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[Larva] {message}");
                break;
            case 2:
                Debug.LogWarning($"[Larva] {message}");
                break;
            case 3:
                Debug.LogError($"[Larva] {message}");
                break;
            default:
                Debug.Log($"[Larva] {message}");
                break;
        }
    }
}