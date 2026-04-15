using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class KamikazeLarva : MonoBehaviour
{
    #region Settings
    [Header("Movement")]
    public float moveSpeed = 6f;
    public float lifeTime = 5f;
    public float detectionRadius = 10f;
    public float explosionTriggerRange = 1.2f;

    [Header("Explosion")]
    public GameObject explosionAreaPrefab;
    public LayerMask enemyLayer;

    [Header("Security")]
    public float invulnerabilityTime = 1f;
    #endregion

    #region Private Variables
    private NavMeshAgent agent;
    private Transform targetEnemy;
    private bool isDetonating = false;
    private bool isInvulnerable = true;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.speed = moveSpeed;
    }

    private void Start()
    {
        StartCoroutine(InvulnerabilityRoutine());
        StartCoroutine(LifeTimer());
        InvokeRepeating(nameof(SearchTarget), 0f, 0.5f);
    }

    private void Update()
    {
        if (isDetonating) return;

        if (targetEnemy != null)
        {
            MoveToTarget();
            CheckProximity();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isInvulnerable || isDetonating) return;

        if (((1 << other.gameObject.layer) & enemyLayer) != 0)
        {
            Explode();
        }
    }
    #endregion

    #region Logic
    private IEnumerator InvulnerabilityRoutine()
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(invulnerabilityTime);
        isInvulnerable = false;
    }

    private void SearchTarget()
    {
        if (targetEnemy != null || isDetonating) return;

        Collider[] cols = Physics.OverlapSphere(transform.position, detectionRadius, enemyLayer);
        float closestDist = Mathf.Infinity;

        foreach (var col in cols)
        {
            float dist = Vector3.Distance(transform.position, col.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                targetEnemy = col.transform;
            }
        }
    }

    private void MoveToTarget()
    {
        if (agent != null && agent.isOnNavMesh)
        {
            agent.SetDestination(targetEnemy.position);
        }
    }

    private void CheckProximity()
    {
        if (isInvulnerable) return;

        if (Vector3.Distance(transform.position, targetEnemy.position) <= explosionTriggerRange)
        {
            Explode();
        }
    }

    private void Explode()
    {
        if (isDetonating || isInvulnerable) return;
        isDetonating = true;

        if (explosionAreaPrefab != null)
        {
            Instantiate(explosionAreaPrefab, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }

    private IEnumerator LifeTimer()
    {
        yield return new WaitForSeconds(lifeTime);
        if (!isDetonating)
        {
            isInvulnerable = false;
            Explode();
        }
    }
    #endregion

    #region Debug
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = isInvulnerable ? Color.blue : Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionTriggerRange);
    }
    #endregion
}