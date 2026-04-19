using UnityEngine;

[CreateAssetMenu(fileName = "VulkanShieldEffect", menuName = "Item Effects/Combat/VulkanShield")]
public class VulkanShieldEffect : ItemEffectBase
{
    public override void ApplyEffect(PlayerStatsManager statsManager)
    {
        CombatEventsManager.OnPlayerHitEnemy += HandleHit;
    }

    public override void RemoveEffect(PlayerStatsManager statsManager)
    {
        CombatEventsManager.OnPlayerHitEnemy -= HandleHit;
    }

    private void HandleHit(GameObject enemy, bool isMelee)
    {
        EntropyChargeSystem chargeSystem = enemy.GetComponentInParent<EntropyChargeSystem>();

        if (!isMelee)
            if (chargeSystem != null) chargeSystem.ApplyCharge();
    }
}
