using UnityEngine;

[CreateAssetMenu(fileName = "ResurrectionEffect", menuName = "Item Effects/Utility/Resurrection")]
public class ResurrectionItemEffect : ItemEffectBase
{
    [Header("Configuración de Resurrección")]
    public float resurrectionChance = 0.20f;
    public bool hasPriority = true;

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
            Debug.Log($"[ResurrectionItemEffect] Activando resurrección para {killedEnemy.name}...");
        }
    }
}