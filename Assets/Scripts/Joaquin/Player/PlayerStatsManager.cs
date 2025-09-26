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

    [SerializeField] private Dictionary<StatType, float> baseStats = new();
    [SerializeField] private Dictionary<StatType, float> currentStats = new();

    public static event Action<StatType, float> OnStatChanged;

    private PlayerHealth playerHealth;

    private void Awake()
    {
        InitializeStats();
        if (playerStats != null) ResetStats();
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
        if (playerStats == null)
        {
            return;
        }

        baseStats[StatType.MaxHealth] = playerStats.maxHealth;
        baseStats[StatType.MoveSpeed] = playerStats.moveSpeed;
        baseStats[StatType.Gravity] = playerStats.gravity;
        baseStats[StatType.MeleeAttackDamage] = playerStats.meleeAttackDamage;
        baseStats[StatType.MeleeRadius] = playerStats.meleeRadius;
        baseStats[StatType.ShieldAttackDamage] = playerStats.shieldAttackDamage;
        baseStats[StatType.ShieldSpeed] = playerStats.shieldSpeed;
        baseStats[StatType.ShieldMaxDistance] = playerStats.shieldMaxDistance;
        baseStats[StatType.ShieldMaxRebounds] = playerStats.shieldMaxRebounds;
        baseStats[StatType.ShieldReboundRadius] = playerStats.shieldReboundRadius;
        baseStats[StatType.AttackDamage] = playerStats.attackDamage;
        baseStats[StatType.AttackSpeed] = playerStats.attackSpeed;
        baseStats[StatType.MeleeAttackSpeed] = playerStats.meleeSpeed;
        baseStats[StatType.HealthDrainAmount] = playerStats.HealthDrainAmount;
        baseStats[StatType.DamageTaken] = 0f;
        baseStats[StatType.ShieldBlockUpgrade] = 0f; // Valor booleano como flotante: 0f = false, 1f = true
    }

    /// <summary>
    /// Función que reinicia todas las estadísticas a sus valores base definidos en PlayerStats.
    /// </summary> 
    public void ResetStats()
    {
        foreach (var kvp in baseStats)
        {
            currentStats[kvp.Key] = kvp.Value;
            OnStatChanged?.Invoke(kvp.Key, kvp.Value);
        }
    }

    public float GetStat(StatType type) => currentStats.TryGetValue(type, out var value) ? value : 0;

    /// <summary>
    /// Aplica un buff/debuff al stat especificado.
    /// </summary>
    /// <param name="type">Stat a modificar.</param>
    /// <param name="amount">Cantidad (positiva o negativa).</param>
    /// <param name="duration">Duración en segundos (solo si no es permanente ni por salas).</param>
    /// <param name="isPercentage">Si es true, el buff es proporcional al valor base.</param>
    /// <param name="isTemporary">Si es false, el buff es permanente hasta morir.</param>
    /// <param name="isByRooms">Si es true, la duración se mide por salas/habitaciones/enfrentamientos.</param>
    /// <param name="roomsDuration">Cantidad de salas/habitaciones/enfrentamientos que debe durar.</param>

    public void ApplyModifier(StatType type, float amount, float duration = 0f, bool isPercentage = false, bool isTemporary = false, bool isByRooms = false, int roomsDuration = 0)
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
        OnStatChanged?.Invoke(type, currentStats[type]);

        if (!isTemporary) return;

        if (isByRooms) StartCoroutine(RemoveModifierAfterRooms(type, modifierValue, roomsDuration));
        else if (duration > 0f) StartCoroutine(RemoveModifierAfterTime(type, modifierValue, duration));
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