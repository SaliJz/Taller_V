using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "HardeningEffect", menuName = "Item Effects/Defense/Hardening")]
public class HardeningItemEffect : ItemEffectBase
{
    [Header("Reducci�n de Da�o")]
    public float damageReductionAmount = 0.15f;

    [Header("Vida Temporal por Muerte")]
    public float temporaryHealthPerKill = 10f;
    public float maxTemporaryHealth = 30f;

    private const string DR_MODIFIER_KEY = "Hardening_DR";
    private readonly List<float> temporaryHealthStacks = new List<float>();

    public override void ApplyEffect(PlayerStatsManager statsManager)
    {
        statsManager.ApplyNamedModifier(DR_MODIFIER_KEY, StatType.DamageTaken, -damageReductionAmount, isPercentage: true);
        CombatEventsManager.OnEnemyKilled += HandleEnemyKilled;

        Debug.Log("[HardeningItemEffect] Aplicado: Reducci�n de Da�o y vida temporal activada.");
    }

    public override void RemoveEffect(PlayerStatsManager statsManager)
    {
        statsManager.RemoveNamedModifier(DR_MODIFIER_KEY);
        CombatEventsManager.OnEnemyKilled -= HandleEnemyKilled;

        temporaryHealthStacks.Clear();

        Debug.Log("[HardeningItemEffect] Removido.");
    }

    private void HandleEnemyKilled(GameObject killedEnemy, float enemyBaseHealth)
    {
        PlayerStatsManager statsManager = FindAnyObjectByType<PlayerStatsManager>();

        if (statsManager != null)
        {
            PlayerHealth playerHealth = statsManager.GetComponent<PlayerHealth>();

            if (playerHealth != null)
            {
                if (!playerHealth.HasAmuletOfEndurance) playerHealth.AcquireAmuletOfEndurance();
                playerHealth.AddTemporaryHealth(temporaryHealthPerKill, maxTemporaryHealth);
                Debug.Log($"[HardeningItemEffect] {killedEnemy.name} eliminado. Se intenta a�adir {temporaryHealthPerKill} de vida temporal.");
            }
        }
    }
}