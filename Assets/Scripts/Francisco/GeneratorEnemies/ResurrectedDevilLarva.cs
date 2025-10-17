using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Linq;

public class ResurrectedDevilLarva : MonoBehaviour
{
    [Header("Configuración de Movimiento")]
    public float moveSpeed = 5f;
    public float lifetime = 5f;

    [Header("Configuración de Daño")]
    public float damagePercentOfEnemyBase = 0.5f;
    public float explosionRadius = 0.5f;
    public float baseDamage = 10f;

    [Header("Visuales")]
    [SerializeField] private Renderer larvaRenderer;

    private Transform targetEnemy;
    private NavMeshAgent agent;
    private float calculatedDamage;
    private bool hasFoundTarget = false;
    private float targetSearchInterval = 0.5f;

    private float _speedMultiplier = 1.0f;
    private float _damageMultiplier = 1.0f;
    private Color _levelColor = Color.white;

    public void Initialize(float originalEnemyBaseHealth, float speedMult, float damageMult, Color levelColor)
    {
        _speedMultiplier = speedMult;
        _damageMultiplier = damageMult;
        _levelColor = levelColor;

        calculatedDamage = originalEnemyBaseHealth * damagePercentOfEnemyBase;
        if (calculatedDamage < baseDamage) calculatedDamage = baseDamage;

        calculatedDamage *= _damageMultiplier;

        ApplyLarvaColor(_levelColor);
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.speed = moveSpeed * _speedMultiplier;
        }

        if (calculatedDamage == 0) calculatedDamage = baseDamage;

        StartCoroutine(LifeTimerRoutine());
        StartCoroutine(TargetSearchRoutine());
    }

    private void Start()
    {
        if (agent != null)
        {
            agent.speed = moveSpeed * _speedMultiplier;
        }
    }

    private void Update()
    {
        if (agent != null && agent.enabled && targetEnemy != null && hasFoundTarget)
        {
            if (Vector3.Distance(transform.position, targetEnemy.position) <= explosionRadius)
            {
                DealDamageAndDie();
            }
            else
            {
                agent.SetDestination(targetEnemy.position);
            }
        }
    }

    private void ApplyLarvaColor(Color color)
    {
        if (larvaRenderer != null)
        {
            larvaRenderer.material.color = color;
        }
    }

    private IEnumerator TargetSearchRoutine()
    {
        while (targetEnemy == null)
        {
            var closest = FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None)
                          .Where(e => e.gameObject != gameObject && !e.IsDead)
                          .OrderBy(e => Vector3.Distance(transform.position, e.transform.position))
                          .FirstOrDefault();

            if (closest != null)
            {
                targetEnemy = closest.transform;
                hasFoundTarget = true;
            }

            yield return new WaitForSeconds(targetSearchInterval);
        }
    }

    private IEnumerator LifeTimerRoutine()
    {
        yield return new WaitForSeconds(lifetime);

        if (!hasFoundTarget)
        {
            DealDamageAndDie();
        }
    }

    private void DealDamageAndDie()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.gameObject == gameObject) continue;

            if (hitCollider.CompareTag("Player")) continue;

            IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(calculatedDamage, false);
            }
        }

        if (agent != null) agent.enabled = false;
        Destroy(gameObject);
    }
}