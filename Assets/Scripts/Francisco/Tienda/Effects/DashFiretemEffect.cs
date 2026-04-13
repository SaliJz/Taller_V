using UnityEngine;

[CreateAssetMenu(fileName = "DashFireEffect", menuName = "Item Effects/Combat/DashFire")]
public class DashFiretemEffect : ItemEffectBase
{
    #region Inspector Fields

    [Header("Daño")]
    [Range(0f, 100f)] public float damagePercent = 40f;

    [Header("Expansion")]
    public float expandDuration = 0.4f;
    public float maxRadius = 3f;

    [Header("Permanencia")]
    public float stayDuration = 2.5f;
    public float tickInterval = 0.3f;

    [Header("Compartido")]
    public LayerMask enemyLayer;

    #endregion

    #region Private Fields

    private PlayerStatsManager cachedStats;

    #endregion

    #region ItemEffectBase

    private void OnEnable()
    {
        EffectID = "Tierra Ardiente";
        category = EffectCategory.Combat;
        if (string.IsNullOrEmpty(effectDescription))
            effectDescription = "Al dashear deja un circulo de tierra ardiente que se expande y quema a los enemigos que permanecen en el.";
    }

    public override void ApplyEffect(PlayerStatsManager statsManager)
    {
        cachedStats = statsManager;
        PlayerCombatEvents.OnDashStarted += HandleDash;
    }

    public override void RemoveEffect(PlayerStatsManager statsManager)
    {
        PlayerCombatEvents.OnDashStarted -= HandleDash;
        cachedStats = null;
    }

    public override string GetFormattedDescription()
    {
        return $"Al dashear genera un circulo de tierra ardiente que se expande hasta <b>{maxRadius}</b> unidades en <b>{expandDuration}s</b>, " +
               $"permanece <b>{stayDuration}s</b> y causa <b>{damagePercent:F0}%</b> del daño base cada <b>{tickInterval}s</b>.";
    }

    #endregion

    #region Private Methods

    private void HandleDash(Vector3 playerPosition)
    {
        if (ItemEffectPool.Instance == null) return;

        float baseDamage = cachedStats != null
            ? cachedStats.GetStat(StatType.AttackDamage) * cachedStats.GetStat(StatType.MeleeAttackDamage)
            : 10f;

        float tickDamage = baseDamage * (damagePercent / 100f);

        ItemEffectPool.Instance.SpawnDashFire(playerPosition, tickDamage, expandDuration,
                                      maxRadius, stayDuration, tickInterval, enemyLayer);
    }

    #endregion
}
