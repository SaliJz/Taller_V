using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class ResurrectedDevilLarva : MonoBehaviour
{
    #region Headers
    [Header("Configuración")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private float baseDamage = 10f;
    [SerializeField] private float explosionRadius = 1.2f;
    [SerializeField] private Renderer larvaRenderer;
    #endregion

    private Transform playerTarget;
    private NavMeshAgent agent;
    private bool isExploding = false;
    private EnemyHealth health; 

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

        StartCoroutine(LifeTimerRoutine());
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
        {
            DealDamageAndDie();
        }
    }

    private void HandleDeath(GameObject obj)
    {
        isExploding = true; 
        StopAllCoroutines();
        if (agent) agent.enabled = false;
    }

    private IEnumerator LifeTimerRoutine()
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
            {
                pHealth.TakeDamage(baseDamage);
            }
        }

        if (agent) agent.enabled = false;

        Destroy(gameObject);
    }
}