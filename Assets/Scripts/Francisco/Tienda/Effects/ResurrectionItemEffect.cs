using UnityEngine;

[CreateAssetMenu(fileName = "ResurrectionEffect", menuName = "Item Effects/Utility/Resurrection")]
public class ResurrectionItemEffect : ItemEffectBase
{
    [Header("Configuración de Resurrección")]
    public float resurrectionChance = 0.20f;
    public bool hasPriority = true;
    
    [Header("Larvas Resucitadas")]
    public GameObject larvaPrefab;
    public int numberOfLarvasToInstantiate = 3;

    public override void ApplyEffect(PlayerStatsManager statsManager)
    {
        CombatEventsManager.OnEnemyKilled += HandleEnemyResurrection;
        Debug.Log($"[ResurrectionItemEffect] Aplicado. Probabilidad de resurrección: {resurrectionChance * 100}% de probabilidad.");
    }

    public override void RemoveEffect(PlayerStatsManager statsManager)
    {
        CombatEventsManager.OnEnemyKilled -= HandleEnemyResurrection;
        Debug.Log("[ResurrectionItemEffect] Removido.");
    }

    private void HandleEnemyResurrection(GameObject killedEnemy, float enemyBaseHealth)
    {
        if (Random.value < resurrectionChance)
        {
            Debug.Log($"[ResurrectionItemEffect] Activando resurrección para {killedEnemy.name}... Instanciando Larvas.");

            if (larvaPrefab != null)
            {
                for (int i = 0; i < numberOfLarvasToInstantiate; i++)
                {
                    GameObject larva = Instantiate(larvaPrefab, killedEnemy.transform.position, Quaternion.identity);

                    larva.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

                    if (larva.TryGetComponent<ResurrectedLarva>(out var larvaScript))
                    {
                        larvaScript.Initialize(enemyBaseHealth);
                    }
                }
            }
        }
    }
}