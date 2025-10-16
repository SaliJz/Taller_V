using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Linq; 

public class ResurrectedLarva : MonoBehaviour
{
    [Header("Configuraci�n de Movimiento")]
    public float moveSpeed = 5f;
    public float lifetime = 5f;

    [Header("Configuraci�n de Da�o")]
    public float damagePercentOfEnemyBase = 0.5f;
    public float explosionRadius = 0.5f;
    public float baseDamage = 10f;

    private Transform targetEnemy;
    private NavMeshAgent agent;
    private float calculatedDamage;
    private bool hasFoundTarget = false;
    private float targetSearchInterval = 0.5f; 

    public void Initialize(float originalEnemyBaseHealth)
    {
        calculatedDamage = originalEnemyBaseHealth * damagePercentOfEnemyBase;
        if (calculatedDamage < baseDamage) calculatedDamage = baseDamage;

        Debug.Log($"[Larva] Da�o configurado a {calculatedDamage:F2}.");
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.speed = moveSpeed;
        }

        if (calculatedDamage == 0) calculatedDamage = baseDamage;

        StartCoroutine(LifeTimerRoutine());
        StartCoroutine(TargetSearchRoutine()); 
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

            float distanceToTarget = Vector3.Distance(transform.position, targetEnemy.position);
            if (distanceToTarget <= explosionRadius)
            {
                hasFoundTarget = true;
                Debug.Log($"[Larva] Objetivo ({targetEnemy.name}) dentro del radio de explosi�n. Explotando...");
                StopAllCoroutines();
                DealDamageAndDie();
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
        if (other.CompareTag("Enemy"))
        {
            if (!hasFoundTarget)
            {
                hasFoundTarget = true;
                StopAllCoroutines();
                Debug.Log($"[Larva] Colisi�n con enemigo ({other.name}). Explotando...");
            }
            DealDamageAndDie();
        }
    }

    private IEnumerator LifeTimerRoutine()
    {
        yield return new WaitForSeconds(lifetime);

        if (!hasFoundTarget)
        {
            Debug.Log($"[Larva] Tiempo de vida expirado. Autodestrucci�n.");
            DealDamageAndDie();
        }
    }

    private void DealDamageAndDie()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.gameObject == gameObject) continue;

            if (hitCollider.CompareTag("Player"))
            {
                Debug.Log($"[Larva] Ignorando al jugador en la explosi�n.");
                continue;
            }

            IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(calculatedDamage, false);
                Debug.Log($"[Larva] Da�o por explosi�n de {calculatedDamage:F2} aplicado a {hitCollider.gameObject.name}.");
            }
        }

        if (agent != null) agent.enabled = false;
        Destroy(gameObject);
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
    }
}