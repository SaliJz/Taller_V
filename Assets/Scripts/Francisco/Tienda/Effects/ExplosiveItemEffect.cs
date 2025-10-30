using UnityEngine;

[CreateAssetMenu(fileName = "ExplosiveEffect", menuName = "Item Effects/Combat/Explosive")]
public class ExplosiveItemEffect : ItemEffectBase
{
    [Header("Configuración de Explosión")]
    public float explosionDamagePercentage = 0.20f;
    public float explosionRadius = 10f;

    [Header("Modificadores de Enemigos Explosivos")]
    public float explosiveEnemyDetonationDelayBonus = 1.5f;

    [Header("Visualización")]
    public GameObject explosionVisualizerPrefab;

    private Vector3 lastExplosionPosition = Vector3.zero;

    private void OnEnable()
    {
        EffectID = "Explosión al Morir";
        category = EffectCategory.Combat;
        if (string.IsNullOrEmpty(effectDescription))
        {
            effectDescription = "Los enemigos derrotados explotan causando daño en área.";
        }
    }

    public override void ApplyEffect(PlayerStatsManager statsManager)
    {
        CombatEventsManager.OnEnemyKilled += HandleEnemyExplosion;

        Debug.Log($"[ExplosiveItemEffect] Aplicado. Radio de explosión: {explosionRadius}.");
    }

    public override void RemoveEffect(PlayerStatsManager statsManager)
    {
        CombatEventsManager.OnEnemyKilled -= HandleEnemyExplosion;

        Debug.Log("[ExplosiveItemEffect] Removido.");
    }

    public override string GetFormattedDescription()
    {
        return $"Los enemigos derrotados explotan causando daño en área igual al <b>{explosionDamagePercentage * 100:F0}%</b> de su salud base " +
               $"dentro de un radio de <b>{explosionRadius}</b> unidades.";
    }

    private void HandleEnemyExplosion(GameObject killedEnemy, float enemyBaseHealth)
    {
        EnemyHealth enemyHealthComponent = killedEnemy.GetComponent<EnemyHealth>();

        if (enemyHealthComponent != null && enemyHealthComponent.ItemEffectHandledDeath)
        {
            return;
        }

        ExplosionDelayHandler handler = killedEnemy.AddComponent<ExplosionDelayHandler>();

        handler.StartExplosion(
            explosionDamagePercentage,
            explosionRadius,
            explosionVisualizerPrefab,
            enemyBaseHealth,
            explosiveEnemyDetonationDelayBonus);

        if (enemyHealthComponent != null)
        {
            enemyHealthComponent.ItemEffectHandledDeath = true;
        }
    }
}