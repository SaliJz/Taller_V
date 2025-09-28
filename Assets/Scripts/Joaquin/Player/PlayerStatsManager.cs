using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enumeración de los diferentes tipos de estadísticas del jugador.
/// </summary>
public enum StatType
{
    MaxHealth,
    MoveSpeed,
    Gravity,
    MeleeAttackDamage,
    MeleeAttackSpeed,
    MeleeRadius,
    ShieldAttackDamage,
    ShieldSpeed,
    ShieldMaxDistance,
    ShieldMaxRebounds,
    ShieldReboundRadius,
    AttackDamage,
    AttackSpeed,
    ShieldBlockUpgrade,
    DamageTaken,
    HealthDrainAmount,
}

/// <summary>
/// Clase que maneja las estadísticas del jugador, incluyendo buffs y debuffs temporales (por tiempo o salas/habitaciones/enfrentamientos) o permanentes.
/// </summary>
public class PlayerStatsManager : MonoBehaviour
{
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private PlayerStats currentStatSO;

    public PlayerStats _currentStatSO => currentStatSO;

    [SerializeField] private Dictionary<StatType, float> baseStats = new();
    [SerializeField] private Dictionary<StatType, float> currentStats = new();

    public static event Action<StatType, float> OnStatChanged;

    private PlayerHealth playerHealth;

    private void Awake()
    {
        InitializeStats();
        ResetCurrentStatsToBase();
    }

    private void Start()
    {
        playerHealth = GetComponent<PlayerHealth>();
    }

    /// <summary>
    /// Inicializa las estadísticas base del jugador a partir de un ScriptableObject.
    /// Se llama en Awake para asegurar que siempre haya valores.
    /// </summary>
    private void InitializeStats()
    {
        if (currentStatSO == null)
        {
            return;
        }

        baseStats[StatType.MaxHealth] = currentStatSO.maxHealth;
        baseStats[StatType.MoveSpeed] = currentStatSO.moveSpeed;
        baseStats[StatType.Gravity] = currentStatSO.gravity;
        baseStats[StatType.MeleeAttackDamage] = currentStatSO.meleeAttackDamage;
        baseStats[StatType.MeleeRadius] = currentStatSO.meleeRadius;
        baseStats[StatType.ShieldAttackDamage] = currentStatSO.shieldAttackDamage;
        baseStats[StatType.ShieldSpeed] = currentStatSO.shieldSpeed;
        baseStats[StatType.ShieldMaxDistance] = currentStatSO.shieldMaxDistance;
        baseStats[StatType.ShieldMaxRebounds] = currentStatSO.shieldMaxRebounds;
        baseStats[StatType.ShieldReboundRadius] = currentStatSO.shieldReboundRadius;
        baseStats[StatType.AttackDamage] = currentStatSO.attackDamage;
        baseStats[StatType.AttackSpeed] = currentStatSO.attackSpeed;
        baseStats[StatType.MeleeAttackSpeed] = currentStatSO.meleeSpeed;
        baseStats[StatType.HealthDrainAmount] = currentStatSO.HealthDrainAmount;
        baseStats[StatType.DamageTaken] = 0f;
        baseStats[StatType.ShieldBlockUpgrade] = 0f;
    }

    /// <summary>
    /// Función que reinicia las estadísticas actuales a sus valores base.
    /// Esto ocurre en cada carga de escena para la nueva instancia.
    /// </summary>
    private void ResetCurrentStatsToBase()
    {
        foreach (var kvp in baseStats)
        {
            currentStats[kvp.Key] = kvp.Value;
            OnStatChanged?.Invoke(kvp.Key, kvp.Value);
        }
    }

    public void ResetStatsOnDeath()
    {
        if (playerStats != null && currentStatSO != null)
        {
            CopyStatsToSO(playerStats, currentStatSO);
        }

        InitializeStats();
        ResetCurrentStatsToBase();
    }

    public void ResetRunStatsToDefaults()
    {
        if (playerStats != null && _currentStatSO != null)
        {
            CopyStatsToSO(playerStats, _currentStatSO);

            _currentStatSO.currentHealth = _currentStatSO.maxHealth;

            _currentStatSO.isShieldBlockUpgradeActive = false;

            Debug.Log("[PlayerStatsManager] Reset completo de stats para nueva Run ejecutado. Vida Maxima forzada.");

            InitializeStats();
        }
    }

    private void CopyStatsToSO(PlayerStats source, PlayerStats target)
    {
        target.maxHealth = source.maxHealth;
        target.moveSpeed = source.moveSpeed;
        target.moveSpeed = source.moveSpeed;
        target.gravity = source.gravity;
        target.meleeAttackDamage = source.meleeAttackDamage;
        target.meleeRadius = source.meleeRadius;
        target.shieldAttackDamage = source.shieldAttackDamage;
        target.shieldSpeed = source.shieldSpeed;
        target.shieldMaxDistance = source.shieldMaxDistance;
        target.shieldMaxRebounds = source.shieldMaxRebounds;
        target.shieldReboundRadius = source.shieldReboundRadius;
        target.attackDamage = source.attackDamage;
        target.attackSpeed = source.attackSpeed;
        target.meleeSpeed = source.meleeSpeed;
        target.HealthDrainAmount = source.HealthDrainAmount;
    }

    public float GetStat(StatType type) => currentStats.TryGetValue(type, out var value) ? value : 0;

    /// <summary>
    /// Aplica un buff/debuff al stat especificado.
    /// </summary>
    /// <param name="type">Stat a modificar.</param>
    /// <param name="amount">Cantidad (positiva o negativa).</param>
    /// <param name="isPercentage">Si es true, el buff es proporcional al valor base.</param>
    /// <param name="isTemporary">Si es false, el buff es permanente hasta morir.</param>
    /// <param name="duration">Duración en segundos (solo si es temporal por tiempo).</param>
    /// <param name="isByRooms">Si es true, la duración se mide por salas/habitaciones/enfrentamientos.</param>
    /// <param name="roomsDuration">Cantidad de salas/habitaciones/enfrentamientos que debe durar.</param>
    public void ApplyModifier(StatType type, float amount, bool isPercentage = false, bool isTemporary = false, float duration = 0f, bool isByRooms = false, int roomsDuration = 0)
    {
        if (!baseStats.ContainsKey(type))
        {
            return;
        }

        float modifierValue = amount;

        if (isPercentage && type != StatType.ShieldBlockUpgrade)
        {
            float percentageFactor = amount / 100f;
            modifierValue = baseStats[type] * percentageFactor;
        }

        currentStats[type] += modifierValue;

        if (float.IsNaN(currentStats[type]) || float.IsInfinity(currentStats[type]))
        {
            Debug.LogError($"[PlayerStatsManager] Stat '{type}' resultó en un valor inválido ({currentStats[type]}). Se ha reseteado al valor base.");
            currentStats[type] = baseStats.ContainsKey(type) ? baseStats[type] : 0f;
        }

        OnStatChanged?.Invoke(type, currentStats[type]);

        if (!isTemporary)
        {
            baseStats[type] = currentStats[type];
            SetStatOnSO(currentStatSO, type, currentStats[type]);

            return;
        }

        if (isByRooms)
        {
            StartCoroutine(RemoveModifierAfterRooms(type, modifierValue, roomsDuration));
        }
        else if (duration > 0f)
        {
            StartCoroutine(RemoveModifierAfterTime(type, modifierValue, duration));
        }
    }

    private void SetStatOnSO(PlayerStats so, StatType type, float value)
    {
        switch (type)
        {
            case StatType.MaxHealth: so.maxHealth = value; break;
            case StatType.MoveSpeed: so.moveSpeed = value; break;
            case StatType.Gravity: so.gravity = value; break;
            case StatType.MeleeAttackDamage: so.meleeAttackDamage = (int)value; break;
            case StatType.MeleeAttackSpeed: so.meleeSpeed = value; break;
            case StatType.MeleeRadius: so.meleeRadius = value; break;
            case StatType.ShieldAttackDamage: so.shieldAttackDamage = (int)value; break;
            case StatType.ShieldSpeed: so.shieldSpeed = value; break;
            case StatType.ShieldMaxDistance: so.shieldMaxDistance = value; break;
            case StatType.ShieldMaxRebounds: so.shieldMaxRebounds = (int)value; break;
            case StatType.ShieldReboundRadius: so.shieldReboundRadius = value; break;
            case StatType.AttackDamage: so.attackDamage = value; break;
            case StatType.AttackSpeed: so.attackSpeed = value; break;
            case StatType.HealthDrainAmount: so.HealthDrainAmount = value; break;

            case StatType.DamageTaken:
            case StatType.ShieldBlockUpgrade:
                break;
            default:
                Debug.LogWarning($"El StatType {type} no está mapeado para la modificación directa del SO.");
                break;
        }
    }

    private IEnumerator RemoveModifierAfterTime(StatType type, float modifierValue, float duration)
    {
        yield return new WaitForSeconds(duration);

        currentStats[type] -= modifierValue;
        OnStatChanged?.Invoke(type, currentStats[type]);
    }

    private IEnumerator RemoveModifierAfterRooms(StatType type, float modifierValue, int rooms)
    {
        int completedRooms = 0;

        // Aquí debería suscribirse a un evento de "sala completada".
        // Espera frames hasta que "completedRooms" alcance el valor.
        while (completedRooms < rooms)
        {
            yield return null;
            // Futura linea de codigo para incrementar completedRooms con un evento.
        }

        currentStats[type] -= modifierValue;
        OnStatChanged?.Invoke(type, currentStats[type]);
    }

    public float GetCurrentStat(StatType type)
    {
        if (currentStats.ContainsKey(type))
        {
            return currentStats[type];
        }

        return 0f;
    }
}