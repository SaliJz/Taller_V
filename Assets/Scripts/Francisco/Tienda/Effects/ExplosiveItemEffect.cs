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
        float explosionDamage = enemyBaseHealth * explosionDamagePercentage;

        Collider[] hitColliders = Physics.OverlapSphere(killedEnemy.transform.position, explosionRadius);

        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.gameObject == killedEnemy) continue;

            IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(explosionDamage, false);
                Debug.Log($"[ExplosiveItemEffect] Da�o por explosi�n de {explosionDamage:F2} aplicado a {hitCollider.gameObject.name}.");
            }
        }

        CreateExplosionVisualizer(killedEnemy.transform.position);
    }

    private void CreateExplosionVisualizer(Vector3 position)
    {
        if (explosionVisualizerPrefab == null)
        {
            Debug.LogWarning("[ExplosiveItemEffect] No hay prefab de visualizador de explosi�n asignado. Asigna el prefab ExplosionVisualizerPrefab.");
            return;
        }

        GameObject visualizer = Instantiate(explosionVisualizerPrefab, position, Quaternion.identity);

        lastExplosionPosition = position;
    }
}