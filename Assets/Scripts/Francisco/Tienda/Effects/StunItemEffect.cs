using UnityEngine;

[CreateAssetMenu(fileName = "StunEffect", menuName = "Item Effects/Combat/Stun")]
public class StunItemEffect : ItemEffectBase
{
    [Header("Configuración de Aturdimiento")]
    public float stunChance = 0.25f;
    public float stunDuration = 1.0f;

    public override void ApplyEffect(PlayerStatsManager statsManager)
    {
        CombatEventsManager.OnPlayerHitEnemy += HandleStunCheck;
        Debug.Log($"[StunItemEffect] Aplicado. Aturdimiento: {stunChance * 100}% de probabilidad por {stunDuration}s.");
    }

    public override void RemoveEffect(PlayerStatsManager statsManager)
    {
        CombatEventsManager.OnPlayerHitEnemy -= HandleStunCheck;
        Debug.Log("[StunItemEffect] Removido.");
    }

    private void HandleStunCheck(GameObject enemyObject, bool isMelee)
    {
        if (Random.value < stunChance) /*isMelee &&*/
        {
            EnemyHealth enemyHealth = enemyObject.GetComponent<EnemyHealth>();

            if (enemyHealth != null)
            {
                enemyHealth.ApplyStun(stunDuration);
                Debug.Log($"[StunItemEffect] Enemigo {enemyObject.name} aturdido por {stunDuration}s.");
            }
        }
    }
}