using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent), typeof(EnemyHealth))]
public class MorlockEnemy : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private MorlockStats stats;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;

    [Header("Estadisticas")]

    [Header("Salud")]
    public float health = 8f;

    [Header("Movimiento y Posicionamiento")]
    [SerializeField] private float optimalAttackDistance = 10f;
    [SerializeField] private float teleportMinDistance = 5f;
    [SerializeField] private float teleportRange = 5f;
    [SerializeField] private float teleportCooldown = 2.5f;

    [Header("Combate")]
    [SerializeField] private float fireRate = 1f;
    [SerializeField] private float projectileDamage = 5f;
    [SerializeField] private float projectileSpeed = 15f;

    private EnemyHealth enemyHealth;
    private NavMeshAgent agent;
    private Transform player;
    private Animator animator;
    private float fireTimer;
    private float teleportTimer;

    private void Start()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (player == null) ReportDebug("Jugador no encontrado en la escena.", 3);

        InitializedEnemy();
    }

    private void InitializedEnemy()
    {
        if (stats != null)
        {
            optimalAttackDistance = stats.optimalAttackDistance;
            teleportMinDistance = stats.teleportMinDistance;
            teleportRange = stats.teleportRange;
            teleportCooldown = stats.teleportCooldown;
            fireRate = stats.fireRate;
            projectileDamage = stats.projectileDamage;
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
        if (player == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer < teleportMinDistance)
        {
            teleportTimer += Time.deltaTime;
            if (teleportTimer >= stats.teleportCooldown)
            {
                StartCoroutine(TeleportRoutine());
                return;
            }
        }
        else
        {
            teleportTimer = 0;
        }

        // (Simplificado: se queda quieto a distancia óptima, podrías añadir movimiento de "kiting")
        agent.isStopped = true;
        transform.LookAt(new Vector3(player.position.x, transform.position.y, player.position.z));

        fireTimer += Time.deltaTime;
        if (fireTimer >= 1f / fireRate)
        {
            Shoot();
            fireTimer = 0;
        }
    }

    private void Shoot()
    {
        if (animator != null) animator.SetTrigger("Shoot");
        GameObject projectileObj = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);

        MorlockProjectile projectile = projectileObj.GetComponent<MorlockProjectile>();
        if (projectile != null)
        {
            projectile.Initialize(projectileSpeed, projectileDamage);
            ReportDebug("Disparando proyectil.", 1);
        }
    }

    private IEnumerator TeleportRoutine()
    {
        teleportTimer = 0;
        agent.enabled = false;
        if (animator != null) animator.SetTrigger("TeleportOut");

        yield return new WaitForSeconds(0.5f);

        Vector3 randomDirection = Random.insideUnitSphere * teleportRange;
        randomDirection += transform.position;
        NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, stats.teleportRange, 1);

        transform.position = hit.position;

        agent.enabled = true;
        if (animator != null) animator.SetTrigger("TeleportIn");

        yield return new WaitForSeconds(0.5f);
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
