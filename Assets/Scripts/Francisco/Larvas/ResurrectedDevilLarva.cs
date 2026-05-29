using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class ResurrectedDevilLarva : MonoBehaviour
{
    #region Settings

    [Header("Configuration")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private float baseDamage = 10f;
    [SerializeField] private float explosionRadius = 1.2f;
    [SerializeField] private Renderer larvaRenderer;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float rotationOffset = 0f;

    #endregion

    #region Private

    private Transform playerTarget;
    private NavMeshAgent agent;
    private bool isExploding = false;
    private EnemyHealth health;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<EnemyHealth>();
    }

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player) playerTarget = player.transform;

        if (agent != null) agent.speed = moveSpeed;

        StartCoroutine(LifetimeRoutine());
    }

    private void OnEnable()
    {
        if (health) health.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        if (health) health.OnDeath -= HandleDeath;
    }

    private void Update()
    {
        if (isExploding || (health != null && health.IsDead) || playerTarget == null || agent == null || !agent.enabled) return;

        agent.SetDestination(playerTarget.position);

        if (Vector3.Distance(transform.position, playerTarget.position) <= explosionRadius)
            DealDamageAndDie();

        RotateTowardsMovement();
    }

    private void RotateTowardsMovement()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        Vector3 velocity = agent.velocity;
        if (velocity.sqrMagnitude < 0.01f) return;

        Vector3 flatVelocity = new Vector3(velocity.x, 0f, velocity.z);
        Quaternion targetRotation = Quaternion.LookRotation(flatVelocity) * Quaternion.Euler(0f, rotationOffset, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
    }

    #endregion

    #region Death

    private void HandleDeath(GameObject obj)
    {
        isExploding = true;
        StopAllCoroutines();
        if (agent) agent.enabled = false;
    }

    private IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(lifetime);
        if (!isExploding && (health == null || !health.IsDead)) DealDamageAndDie();
    }

    private void DealDamageAndDie()
    {
        if (isExploding) return;
        isExploding = true;

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Player") && hitCollider.TryGetComponent<PlayerHealth>(out var pHealth))
                pHealth.TakeDamage(baseDamage);
        }

        if (agent) agent.enabled = false;
        Destroy(gameObject);
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);

        if (playerTarget != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, playerTarget.position);
        }
    }

    #endregion
}