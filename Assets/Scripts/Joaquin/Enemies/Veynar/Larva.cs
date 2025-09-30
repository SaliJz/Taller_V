using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(EnemyHealth))]
public class Larva : MonoBehaviour
{
    [Header("Stats")]
    [Tooltip("Velocidad de movimiento de la larva.")]
    [SerializeField] private float speed = 6f;
    [Tooltip("Daño infligido al impactar de frente o por los lados.")]
    [SerializeField] private float frontalDamage = 2.5f;
    [Tooltip("Daño infligido al impactar por la espalda.")]
    [SerializeField] private float backDamage = 5f;
    [Tooltip("Tiempo máximo de vida antes de autodestruirse.")]
    [SerializeField] private float lifeTime = 10f;

    [Header("Detección")]
    [Tooltip("Define qué capas son consideradas 'jugador' para la colisión.")]
    [SerializeField] private LayerMask playerLayer;

    private NavMeshAgent agent;
    private Transform player;
    private SimplePool myPool;
    private EnemyHealth enemyHealth;

    private float lifeTimer = 0f;

    private void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.CanDestroy = false;
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

        ReturnToPool();
    }

    /// <summary>
    /// Inicializa la larva, asignándole su pool y activando su IA.
    /// </summary>
    public void Initialize(SimplePool ownerPool, Transform playerTransform)
    {
        myPool = ownerPool;
        if (playerTransform != null) player = playerTransform;
        else player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.speed = speed;
            if (!agent.enabled) agent.enabled = true;
            agent.isStopped = false;
        }

        StopAllCoroutines();
        lifeTimer = 0f;
        StartCoroutine(LifeCycle());
    }

    private IEnumerator LifeCycle()
    {
        while (lifeTimer < lifeTime)
        {
            lifeTimer += Time.deltaTime;

            if (player != null && agent.isOnNavMesh && !agent.isStopped)
            {
                if (Vector3.Distance(agent.destination, player.position) > 1.0f)
                {
                    agent.SetDestination(player.position);
                }
            }
            yield return null;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if ((playerLayer.value & (1 << collision.gameObject.layer)) > 0)
        {
            HandleCollisionWithPlayer(collision.transform);
        }
    }

    private void HandleCollisionWithPlayer(Transform playerObject)
    {
        PlayerHealth playerHealth = playerObject.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            Vector3 fromPlayerToLarva = (transform.position - playerObject.transform.position).normalized;
            float dotProduct = Vector3.Dot(playerObject.transform.forward, fromPlayerToLarva);

            bool isBackHit = dotProduct < 0f;
            float finalDamage = isBackHit ? backDamage : frontalDamage;

            playerHealth.TakeDamage(finalDamage, isBackHit);
            Debug.Log($"[Larva] Golpe a {playerObject.name} por {finalDamage} de daño. ¿Por la espalda?: {isBackHit}");
        }

        enemyHealth.Die();
    }

    private void ReturnToPool()
    {
        StopAllCoroutines();
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        if (myPool != null)
        {
            myPool.Return(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}