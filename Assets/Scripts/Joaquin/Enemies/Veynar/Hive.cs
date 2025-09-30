using UnityEngine;
using System.Collections;
using UnityEngine.AI;

[RequireComponent(typeof(EnemyHealth))]
public class Hive : MonoBehaviour
{
    [Header("Hive settings")]
    [SerializeField] private float larvaSpawnInterval = 2.5f;
    [SerializeField] private int maxLarvas = 4;
    [SerializeField] private float autoDestroyAfterMaxSeconds = 10f;
    [SerializeField] private GameObject larvaPrefab;
    [SerializeField] private NavMeshObstacle obstacle;
    [SerializeField] private NavMeshAgent navMeshAgent;
    [Tooltip("Tiempo de cooldown antes de destruirse.")]
    [SerializeField] private float deathCooldown = 0.5f;

    [Header("References")]
    [SerializeField] private AudioClip spawnLarvaSFX;
    [SerializeField] private ParticleSystem spawnVFX;

    private int spawnedCount = 0;
    private bool isProducing = true;
    private VeynarEnemy owner;
    private EnemyHealth enemyHealth;

    public VeynarEnemy Owner => owner;
    public int SpawnedCount => spawnedCount;

    private void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        obstacle = GetComponent<NavMeshObstacle>();
        navMeshAgent = GetComponent<NavMeshAgent>();

        if (enemyHealth != null) enemyHealth.CanHealPlayer = false;
        if (enemyHealth != null) enemyHealth.CanDestroy = true;
        if (enemyHealth != null) enemyHealth.DeathCooldown = deathCooldown;
        if (obstacle != null) obstacle.enabled = false;
        if (navMeshAgent != null) navMeshAgent.enabled = true;
    }

    public void Initialize(VeynarEnemy ownerRef, GameObject larvaPrefabRef)
    {
        owner = ownerRef;
        larvaPrefab = larvaPrefabRef;

        spawnedCount = 0;
        isProducing = true;

        StopAllCoroutines();
        StartCoroutine(ProduceRoutine());

        StartCoroutine(ActivateObstacleAfterDelay(1f));
    }

    private void OnEnable()
    {
        if (enemyHealth != null) enemyHealth.OnDeath += HandleEnemyDeath;
    }

    private void OnDisable()
    {
        if (enemyHealth != null) enemyHealth.OnDeath -= HandleEnemyDeath;
        if (obstacle != null) obstacle.enabled = false;
    }

    private void OnDestroy()
    {
        if (enemyHealth != null) enemyHealth.OnDeath -= HandleEnemyDeath;
    }

    private void HandleEnemyDeath(GameObject enemy)
    {
        if (enemy != gameObject) return;

        obstacle.enabled = false;
        StopAllCoroutines();
        owner?.OnHiveDestroyed(this);
    }

    private IEnumerator ProduceRoutine()
    {
        while (isProducing && spawnedCount < maxLarvas)
        {
            yield return new WaitForSeconds(larvaSpawnInterval);

            if (owner == null || owner.IsDead)
            {
                StopProducing();
                yield break;
            }

            SpawnLarva();
        }
    }

    /// <summary>
    /// Corrutina que activa el NavMeshObstacle después de un breve instante.
    /// Esto da tiempo a las larvas recién generadas a salir del área.
    /// </summary>
    private IEnumerator ActivateObstacleAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (navMeshAgent != null) navMeshAgent.enabled = false;
        if (obstacle != null) obstacle.enabled = true;
    }

    private void SpawnLarva()
    {
        spawnedCount++;

        if (larvaPrefab == null)
        {
            Debug.LogWarning("[Hive] larvaPrefab no asignado.");
            return;
        }

        Vector3 spawnPosition = transform.position + Vector3.up * 0.5f;
        NavMeshHit hit;

        if (NavMesh.SamplePosition(spawnPosition, out hit, 2f, NavMesh.AllAreas))
        {
            spawnPosition = hit.position + Vector3.up * 0.2f;
        }
        else
        {
            for (int i = 0; i < 5; i++)
            {
                Vector3 randomOffset = Random.insideUnitSphere * 0.8f;
                randomOffset.y = 0.5f;
                Vector3 testPos = transform.position + randomOffset;

                if (NavMesh.SamplePosition(testPos, out hit, 2f, NavMesh.AllAreas))
                {
                    spawnPosition = hit.position + Vector3.up * 0.2f;
                    break;
                }
            }
        }

        var larvaGameObject = Instantiate(larvaPrefab, spawnPosition, Quaternion.identity);

        if (larvaGameObject != null && larvaGameObject.activeInHierarchy)
        {
            var larva = larvaGameObject.GetComponent<Larva>();
            if (larva != null)
            {
                larva.Initialize(owner?.PlayerTransform);
            }
            else
            {
                Debug.LogWarning("[Hive] No se encontró componente Larva en el prefab.");
            }
        }
        else
        {
            Debug.LogWarning("[Hive] La larva no se pudo activar correctamente. Destruyéndola.");
            if (larvaGameObject != null) Destroy(larvaGameObject);
            spawnedCount--;
            return;
        }

        if (spawnVFX != null) spawnVFX.Play();

        owner?.PlaySFX(spawnLarvaSFX);

        if (spawnedCount >= maxLarvas)
        {
            StartCoroutine(SelfDestructAfterDelay());
        }
    }

    public void StopProducing()
    {
        isProducing = false;
        StopAllCoroutines();
        if (!enemyHealth.IsDead) enemyHealth.Die();
    }

    private IEnumerator SelfDestructAfterDelay()
    {
        yield return new WaitForSeconds(autoDestroyAfterMaxSeconds);
        if (!enemyHealth.IsDead) enemyHealth.Die();
    }
}