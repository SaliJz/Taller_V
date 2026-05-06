using System;
using UnityEngine;

[RequireComponent(typeof(EnemyHealth))]
public class EntropyChargeSystem : MonoBehaviour
{
    #region Constants

    public const int MaxCharges = 3;

    #endregion

    #region Inspector Fields

    [Header("Damage")]
    [Range(0f, 100f)] private float damagePercent = 5f;

    [Header("Charge Settings")]
    private float chargeDuration = 1f;
    private float tickInterval = 0.2f;

    [Header("VFX")]
    [SerializeField] private GameObject chargeVFX;

    #endregion

    #region Events

    public event Action<int, float, float> OnChargesChanged;
    public event Action OnChargesExpired;

    public float GetChargeDuration() => chargeDuration;

    #endregion

    #region Private Fields

    private EnemyHealth enemyHealth;

    private int currentCharges = 0;
    private float timeRemaining = 0f;
    private float totalTime = 0f;
    private float tickTimer = 0f;
    private float pendingDamagePerTick = 0f;

    private PlayerStatsManager cachedStats;

    private bool isActive = false;

    #endregion

    #region Properties

    public int CurrentCharges => currentCharges;
    public float TimeRemaining => timeRemaining;
    public float TotalTime => totalTime;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        cachedStats = FindAnyObjectByType<PlayerStatsManager>();

        SetVFX(false);
    }

    private void Update()
    {
        if (!isActive) return;
        if (enemyHealth.IsDead)
        {
            ClearCharges();
            return;
        }

        tickTimer -= Time.deltaTime;
        if (tickTimer <= 0f)
        {
            tickTimer = tickInterval;
            enemyHealth.TakeDamage(pendingDamagePerTick);
        }

        timeRemaining -= Time.deltaTime;
        NotifyChanged();

        if (timeRemaining <= 0f)
        {
            ExpireCharges();
        }
    }

    #endregion

    #region Public API

    public void ApplyCharge()
    {
        if (enemyHealth.IsDead) return;

        float baseDamage = cachedStats != null
            ? cachedStats.GetStat(StatType.AttackDamage) * cachedStats.GetStat(StatType.MeleeAttackDamage)
            : 10f;

        pendingDamagePerTick = baseDamage * (damagePercent / 100f);

        if (currentCharges < MaxCharges)
            currentCharges++;

        float maxTotalTime = MaxCharges * chargeDuration;
        timeRemaining = Mathf.Min(timeRemaining + chargeDuration, maxTotalTime);

        isActive = true;
        tickTimer = tickInterval;

        SetVFX(true);
        NotifyChanged();
    }

    public void ClearCharges()
    {
        currentCharges = 0;
        timeRemaining = 0f;
        totalTime = 0f;
        tickTimer = 0f;
        isActive = false;

        SetVFX(false);
        OnChargesExpired?.Invoke();
        NotifyChanged();
    }

    #endregion

    #region Private Methods

    private void ExpireCharges()
    {
        currentCharges = 0;
        timeRemaining = 0f;
        totalTime = 0f;
        tickTimer = 0f;
        isActive = false;

        SetVFX(false);
        OnChargesExpired?.Invoke();
        NotifyChanged();
    }

    private void SetVFX(bool active)
    {
        if (chargeVFX != null)
            chargeVFX.SetActive(active);
    }

    private void NotifyChanged()
    {
        OnChargesChanged?.Invoke(currentCharges, timeRemaining, totalTime);
    }

    #endregion
}