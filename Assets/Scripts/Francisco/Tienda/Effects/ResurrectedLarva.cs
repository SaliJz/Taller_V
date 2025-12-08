using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Linq;

public class ResurrectedLarva : MonoBehaviour
{
    [Header("Configuración de Movimiento")]
    public float moveSpeed = 5f;
    public float lifetime = 5f;

    [Header("Configuración de Daño")]
    public float damagePercentOfEnemyBase = 0.5f;
    public float explosionRadius = 0.5f;
    public float baseDamage = 10f;

    [Header("Configuración de Invulnerabilidad")]
    public float invulnerabilityTime = 1f; 

    private Transform targetEnemy;
    private NavMeshAgent agent;
    private float calculatedDamage;
    private bool hasFoundTarget = false;
    private bool isInvulnerable = true; 
    private float targetSearchInterval = 0.5f;

    public void Initialize(float originalEnemyBaseHealth)
    {
        calculatedDamage = originalEnemyBaseHealth * damagePercentOfEnemyBase;
        if (calculatedDamage < baseDamage) calculatedDamage = baseDamage;

        Debug.Log($"[Larva] Daño configurado a {calculatedDamage:F2}.");
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.speed = moveSpeed;
        }

        if (calculatedDamage == 0) calculatedDamage = baseDamage;

        StartCoroutine(InvulnerabilityRoutine());
        StartCoroutine(LifeTimerRoutine());
        StartCoroutine(TargetSearchRoutine());
    }

    private IEnumerator InvulnerabilityRoutine()
    {
        isInvulnerable = true;
        Debug.Log($"[Larva] Invulnerabilidad activada por {invulnerabilityTime} segundos.");

        yield return new WaitForSeconds(invulnerabilityTime);

        isInvulnerable = false;
        Debug.Log($"[Larva] Invulnerabilidad desactivada. Ahora puede hacer daño.");
    }

    private void Update()
    {
        if (hasFoundTarget)
        {
            return;
        }

        if (targetEnemy != null && agent != null && agent.enabled)
        {
            agent.SetDestination(targetEnemy.position);

            if (!isInvulnerable)
            {
                float distanceToTarget = Vector3.Distance(transform.position, targetEnemy.position);
                if (distanceToTarget <= explosionRadius)
                {
                    hasFoundTarget = true;
                    Debug.Log($"[Larva] Objetivo ({targetEnemy.name}) dentro del radio de explosión. Explotando...");
                    StopAllCoroutines();
                    DealDamageAndDie();
                }
            }
        }
        else if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
        }
    }

    private IEnumerator TargetSearchRoutine()
    {
        while (!hasFoundTarget)
        {
            FindNearestEnemyInScene();
            yield return new WaitForSeconds(targetSearchInterval);
        }
    }

    private void FindNearestEnemyInScene()
    {
        GameObject[] allEnemies = GameObject.FindGameObjectsWithTag("Enemy");

        if (allEnemies.Length > 0)
        {
            Transform nearest = allEnemies
                .Select(g => g.transform)
                .OrderBy(t => Vector3.Distance(transform.position, t.position))
                .FirstOrDefault();

            if (nearest != null && nearest != targetEnemy)
            {
                targetEnemy = nearest;
                Debug.Log($"[Larva] Nuevo objetivo encontrado en la escena: {targetEnemy.name}.");
                if (agent != null) agent.isStopped = false;
            }
            else if (targetEnemy == null && nearest != null)
            {
                targetEnemy = nearest;
                if (agent != null) agent.isStopped = false;
            }
        }
        else
        {
            targetEnemy = null;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isInvulnerable)
        {
            Debug.Log($"[Larva] Colisión ignorada con ({other.name}) - Invulnerable.");
            return;
        }

        if (other.CompareTag("Enemy"))
        {
            if (!hasFoundTarget)
            {
                hasFoundTarget = true;
                StopAllCoroutines();
                Debug.Log($"[Larva] Colisión con enemigo ({other.name}). Explotando...");
            }
            DealDamageAndDie();
        }
    }

    private IEnumerator LifeTimerRoutine()
    {
        yield return new WaitForSeconds(lifetime);

        if (!hasFoundTarget)
        {
            Debug.Log($"[Larva] Tiempo de vida expirado. Autodestrucción.");
            DealDamageAndDie();
        }
    }

    private void DealDamageAndDie()
    {
        if (isInvulnerable)
        {
            Debug.Log($"[Larva] Intento de explosión durante invulnerabilidad. Ignorado.");
            return;
        }

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, explosionRadius);

        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.gameObject == gameObject) continue;

            Transform root = hitCollider.transform.root;
            if (root != null && root.CompareTag("Player"))
            {
                Debug.Log($"[Larva] Ignorando al jugador principal o componente ({hitCollider.name}) en la explosión.");
                continue;
            }

            IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(baseDamage, false);
                Debug.Log($"[Larva] Daño por explosión de {baseDamage:F2} aplicado a {hitCollider.gameObject.name}.");
            }
        }

        Destroy(gameObject);
        if (agent != null) agent.enabled = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);

        if (targetEnemy != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, targetEnemy.position);
        }

        if (isInvulnerable && Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, explosionRadius * 1.5f);
        }
    }
}