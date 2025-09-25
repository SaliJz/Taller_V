using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using UnityEngine.Android;

[RequireComponent(typeof(NavMeshAgent), typeof(EnemyHealth))]
public class KronusEnemy : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private KronusStats stats;
    [SerializeField] private Transform hitPoint;

    [Header("Estadisticas")]

    [Header("Salud")]
    [SerializeField] private float health = 10f;

    [Header("Movimiento")]
    [Tooltip("Velocidad de movimiento por defecto si no se encuentra KronusStats.")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float dashSpeedMultiplier = 2f;
    [SerializeField] private float dashDuration = 1f;

    [Header("Ataque")]
    [SerializeField] private float attackCycleCooldown = 5f;
    [SerializeField] private float attackDamagePercentage = 0.2f;
    [SerializeField] private float attackRadius = 2f;
    [SerializeField] private LayerMask playerLayer;

    private EnemyHealth enemyHealth;
    private NavMeshAgent agent;
    private Transform player;
    private PlayerHealth playerHealth;
    private Animator animator;
    private float attackTimer;

    private void Start()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (player == null) ReportDebug("Jugador no encontrado en la escena.", 3);
        if (player != null) playerHealth = player.GetComponent<PlayerHealth>();

        InitializedEnemy();

        agent.speed = moveSpeed;
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
            enemyHealth.SetMaxHealth(stats.health);
        }
        else
        {
            ReportDebug("MorlockStats no asignado. Usando valores por defecto.", 2);
            enemyHealth.SetMaxHealth(health);
        }
    }

    private void OnEnable()
    {
        enemyHealth.OnDeath += HandleEnemyDeath;
    }

    private void OnDisable()
    {
        enemyHealth.OnDeath -= HandleEnemyDeath;
    }

    private void HandleEnemyDeath(GameObject enemy)
    {
        if (enemy == gameObject)
        {
            agent.isStopped = true;
            if (animator != null) animator.SetTrigger("Die");
            this.enabled = false;
        }
    }

    private void Update()
    {
        if (player == null)
        {
            agent.isStopped = true;
            return;
        }

        attackTimer += Time.deltaTime;

        if (attackTimer >= attackCycleCooldown)
        {
            StartCoroutine(DashAttackRoutine());
        }
        else
        {
            agent.isStopped = false;
            agent.SetDestination(player.position);
        }
    }

    private IEnumerator DashAttackRoutine()
    {
        ReportDebug("Kronus inicia ataque dash.", 1);

        attackTimer = 0f;
        agent.isStopped = true;

        if (animator != null) animator.SetTrigger("StartDash");

        Vector3 playerLastPosition = player.position;
        playerLastPosition.y = transform.position.y;

        float startTime = Time.time;
        while (Time.time < startTime + dashDuration)
        {
            transform.position = Vector3.MoveTowards(transform.position, playerLastPosition,
                                                     moveSpeed * dashSpeedMultiplier * Time.deltaTime);
            yield return null;
        }

        if (animator != null) animator.SetTrigger("Attack");

        if (Vector3.Distance(transform.position, player.position) <= attackRadius)
        {
            PerformHitDetection();
        }

        yield return new WaitForSeconds(2.5f);

        ReportDebug("Kronus finaliza ataque dash.", 1);
    }

    public void PerformHitDetection()
    {
        Collider[] hitPlayer = Physics.OverlapSphere(hitPoint.position, attackRadius, playerLayer);

        foreach (Collider player in hitPlayer)
        {
            if (playerHealth != null)
            {
                bool isCritical;
                float damage = playerHealth.MaxHealth * attackDamagePercentage;
                float finalDamageWithCrit = CriticalHitSystem.CalculateDamage(attackDamagePercentage, transform, player.transform, out isCritical);
                
                playerHealth.TakeDamage(finalDamageWithCrit);
                ReportDebug($"Kronus atacó al jugador por {finalDamageWithCrit} de daño.", 1);
            }
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