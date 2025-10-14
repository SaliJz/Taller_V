using UnityEngine;

[CreateAssetMenu(fileName = "ExplosiveEffect", menuName = "Item Effects/Combat/Explosive")]
public class ExplosiveItemEffect : ItemEffectBase
{
    [Header("Configuraci�n de Explosi�n")]
    public float explosionDamagePercentage = 0.20f;
    public float explosionRadius = 10f;

    [Header("Modificadores de Enemigos Explosivos")]
    public float explosiveEnemyDetonationDelayBonus = 1.5f;

    [Header("Visualizaci�n")]
    public GameObject explosionVisualizerPrefab;

    private Vector3 lastExplosionPosition = Vector3.zero;

    public override void ApplyEffect(PlayerStatsManager statsManager)
    {
        CombatEventsManager.OnEnemyKilled += HandleEnemyExplosion;

        Debug.Log($"[ExplosiveItemEffect] Aplicado. Radio de explosi�n: {explosionRadius}.");
    }

    public override void RemoveEffect(PlayerStatsManager statsManager)
    {
        CombatEventsManager.OnEnemyKilled -= HandleEnemyExplosion;

        Debug.Log("[ExplosiveItemEffect] Removido.");
    }

    private void HandleEnemyExplosion(GameObject killedEnemy, float enemyBaseHealth)
    {
        ExplosionDelayHandler handler = killedEnemy.AddComponent<ExplosionDelayHandler>();

        handler.StartExplosion(
            explosionDamagePercentage,
            explosionRadius,
            explosionVisualizerPrefab,
            enemyBaseHealth,
            explosiveEnemyDetonationDelayBonus 
        );
    }
}