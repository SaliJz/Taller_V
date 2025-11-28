using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(EnemyHealth))]
public class Larva : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float speed = 10f;
    [SerializeField] private float explosionDamage = 4f;
    [SerializeField] private float lifeTime = 10f;
    [SerializeField] private float knockbackForce = 0.5f;

    [Header("Detección / Movimiento")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private float stoppingBuffer = 0.5f;
    [SerializeField] private float attackRange = 5f;
    [SerializeField] private float attackCooldown = 0.5f;

    [Header("Agent tuning")]
    [SerializeField] private float destinationUpdateInterval = 0.18f;
    [SerializeField] private float accelMultiplier = 3f;
    [SerializeField] private float minAcceleration = 8f;
    [SerializeField] private float angularSpeed = 120f;

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;

    [Header("SFX")]
    [SerializeField] private AudioClip moveSFX;
    [SerializeField] private AudioClip attackExplosionSFX;
    [SerializeField] private AudioClip deathSFX;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    private NavMeshAgent agent;
    private Transform player;
    private PlayerHealth playerHealth;
    private EnemyHealth enemyHealth;

    private float moveSoundTimer;
    private float moveSoundRate = 0.4f;
    private bool hasExploded = false;

    private float lifeTimer = 0f;
    private float lastAttackTime = -999f;
    private float lastDestinationTime = -999f;
    private bool initialized = false;

    private void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();

        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        var rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        agent = GetComponent<NavMeshAgent>();
        if (agent != null) ConfigureAgentFromParams();
    }

    private void Update()
    {
        if (!initialized) return;
        if (player == null || enemyHealth == null || enemyHealth.IsDead) return;

        HandleMovementAudio();
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

        if (audioSource != null && deathSFX != null && !hasExploded)
        {
            audioSource.PlayOneShot(deathSFX);
        }

        StopAllCoroutines();
        
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    private void HandleMovementAudio()
    {
        if (agent != null && agent.enabled && !agent.isStopped && agent.velocity.sqrMagnitude > 0.5f)
        {
            moveSoundTimer += Time.deltaTime;
            if (moveSoundTimer >= moveSoundRate)
            {
                if (audioSource != null && moveSFX != null)
                {
                    audioSource.pitch = Random.Range(1.1f, 1.3f);
                    audioSource.PlayOneShot(moveSFX, 0.6f);
                    audioSource.pitch = 1f;
                }
                moveSoundTimer = 0f;
            }
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

        // El agente se detiene justo antes del rango de ataque
        agent.stoppingDistance = attackRange + stoppingBuffer;

        agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
        agent.autoRepath = true;
        agent.enabled = true;
    }

    public void Initialize(Transform playerTransform)
    {
        player = playerTransform ?? GameObject.FindGameObjectWithTag("Player")?.transform;
        playerHealth = player ? player.GetComponent<PlayerHealth>() : null;

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null && !agent.isOnNavMesh)
        {
            ReportDebug("NavMeshAgent no está en el NavMesh. Intentando colocar...", 2);
            if (NavMesh.SamplePosition(transform.position, out var hit, 2f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
                agent.Warp(hit.position);
            }
            else
            {
                ReportDebug("No se pudo colocar en el NavMesh. Destruyendo larva.", 3);
                Die();
                return;
            }
        }

        if (agent != null) ConfigureAgentFromParams();
        StopAllCoroutines();
        lifeTimer = 0f;
        lastAttackTime = -999f;
        lastDestinationTime = -999f;
        hasExploded = false;

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

                // Si está lejos, perseguir
                if (distance > agent.stoppingDistance)
                {
                    if (agent.isStopped) agent.isStopped = false;

                    if (Time.time >= lastDestinationTime + destinationUpdateInterval)
                    {
                        agent.SetDestination(player.position);
                        lastDestinationTime = Time.time;
                    }
                }
                // Si está cerca (dentro del rango de 5m), detenerse y atacar
                else
                {
                    agent.ResetPath();
                    agent.isStopped = true;
                    TryAttack(); // Esto llamará a Die()
                }
            }
            yield return null;
        }

        // Morir si se acaba el tiempo
        Die();
    }

    private void TryAttack()
    {
        if (player == null || enemyHealth == null || enemyHealth.IsDead) return;
        if (Time.time < lastAttackTime + attackCooldown) return;

        hasExploded = true;

        if (audioSource != null && attackExplosionSFX != null)
        {
            audioSource.PlayOneShot(attackExplosionSFX);
        }

        bool damageApplied = false;

        if (damageApplied) return;

        Collider[] hitPlayer = Physics.OverlapSphere(transform.position, attackRange, playerLayer);

        foreach (var hit in hitPlayer)
        {
            var hitTransform = hit.transform;

            // Ejecutar ataque
            ExecuteAttack(hit.gameObject, explosionDamage);

            // Aplicar empuje
            ApplyKnockback(hitTransform);

            ReportDebug($"Drogath atacó al jugador por {explosionDamage} de daño", 1);

            damageApplied = true;
        }

        if (damageApplied)
        {
            ReportDebug($"[Larva] Impacto kamikaze a {player.name}: daño={explosionDamage}", 1);
        }
        else
        {
            ReportDebug($"[Larva] No se pudo aplicar daño a {player.name}.", 2);
        }

        lastAttackTime = Time.time;

        // Comportamiento Kamikaze: Morir al impactar
        Die();
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
        // Calcula la dirección del empuje desde Kronus hacia el jugador
        Vector3 knockbackDirection = (target.position - transform.position).normalized;
        knockbackDirection.y = 0f;

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

    /// <summary>
    /// Inicia la secuencia de muerte de la larva.
    /// </summary>
    public void Die()
    {
        if (enemyHealth != null && !enemyHealth.IsDead)
        {
            enemyHealth.Die();
        }
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange + stoppingBuffer);
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