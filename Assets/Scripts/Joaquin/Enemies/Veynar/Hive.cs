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
    [SerializeField] private NavMeshObstacle obstacle;

    [Header("References")]
    [SerializeField] private AudioClip spawnLarvaSFX;
    [SerializeField] private ParticleSystem spawnVFX;

    private int spawnedCount = 0;
    private bool isProducing = true;
    private VeynarEnemy owner;
    private SimplePool myPool;
    private SimplePool larvaPool;
    private EnemyHealth enemyHealth;

    public VeynarEnemy Owner => owner;
    public int SpawnedCount => spawnedCount;

    private void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        obstacle = GetComponent<NavMeshObstacle>();

        if (enemyHealth != null) enemyHealth.CanDestroy = false;
        if (obstacle != null) obstacle.enabled = false;
    }

    public void Initialize(VeynarEnemy ownerRef, SimplePool hivePoolRef, SimplePool larvaPoolRef)
    {
        owner = ownerRef;
        myPool = hivePoolRef;
        larvaPool = larvaPoolRef;

        spawnedCount = 0;
        isProducing = true;

        StopAllCoroutines();
        StartCoroutine(ProduceRoutine());

        StartCoroutine(ActivateObstacleAfterDelay(0.2f));
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
        StopProducing();
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
        if (obstacle != null)
        {
            obstacle.enabled = true;
        }
    }

    private void SpawnLarva()
    {
        spawnedCount++;

        if (larvaPool == null) return;

        var larvaGameObject = larvaPool.Get();
        larvaGameObject.transform.position = transform.position + Vector3.up * 0.5f + Random.insideUnitSphere * 0.3f;
        larvaGameObject.transform.rotation = Quaternion.identity;

        var larva = larvaGameObject.GetComponent<Larva>();
        if (larva != null) larva.Initialize(larvaPool, owner?.PlayerTransform);

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
        SelfDestruct();
    }

    private IEnumerator SelfDestructAfterDelay()
    {
        yield return new WaitForSeconds(autoDestroyAfterMaxSeconds);
        SelfDestruct();
    }

    private void SelfDestruct()
    {
        StopAllCoroutines();
        owner?.OnHiveDestroyed(this);

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