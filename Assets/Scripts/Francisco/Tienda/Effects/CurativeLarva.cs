using UnityEngine;
using UnityEngine.AI; 
using System.Collections;

public class CurativeLarva : MonoBehaviour
{
    [Header("Configuración de Movimiento")]
    public float moveSpeed = 5f;
    public float lifetime = 5f;
    public float stopDistance = 0.5f; 

    [Header("Configuración de Curación")]
    public float baseHealAmount = 15f;
    public float healingRadius = 0.75f; 

    private float calculatedHealAmount;
    private PlayerHealth playerHealth;
    private Transform playerTransform; 
    private NavMeshAgent agent;
    private bool hasHealed = false;

    public void Initialize(float originalEnemyBaseHealth)
    {
        calculatedHealAmount = baseHealAmount;
        Debug.Log($"[CurativeLarva] Curación configurada a {calculatedHealAmount:F2}.");
    }

    private void Awake()
    {
        playerHealth = FindAnyObjectByType<PlayerHealth>();

        if (playerHealth != null)
        {
            playerTransform = playerHealth.transform;
        }

        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.stoppingDistance = stopDistance;
        }
        else
        {
            Debug.LogError("[CurativeLarva] No se encontró el componente NavMeshAgent.");
        }

        if (calculatedHealAmount == 0) calculatedHealAmount = baseHealAmount;

        StartCoroutine(LifeTimerRoutine());
    }

    private void Update()
    {
        if (hasHealed || playerTransform == null || agent == null || !agent.isActiveAndEnabled) return;

        agent.SetDestination(playerTransform.position);

        if (Vector3.Distance(transform.position, playerTransform.position) <= healingRadius)
        {
            HealPlayerAndDie();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player") && !hasHealed)
        {
            HealPlayerAndDie();
        }
    }

    private IEnumerator LifeTimerRoutine()
    {
        yield return new WaitForSeconds(lifetime);
        if (!hasHealed)
        {
            Debug.Log($"[CurativeLarva] Tiempo de vida expirado. Autodestrucción sin curar.");
            Destroy(gameObject);
        }
    }

    private void HealPlayerAndDie()
    {
        if (hasHealed) return; 

        if (playerHealth != null)
        {
            playerHealth.Heal(calculatedHealAmount);
            Debug.Log($"[CurativeLarva] Curación de {calculatedHealAmount:F2} aplicada al jugador.");
        }

        hasHealed = true;

        if (agent != null) agent.enabled = false;

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, healingRadius);
    }
}