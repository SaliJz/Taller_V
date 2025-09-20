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
    MeleeRadius,
    ShieldAttackDamage,
    ShieldSpeed,
    ShieldMaxDistance,
    ShieldMaxRebounds,
    ShieldReboundRadius,
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

    private void Start()
    {
        if (playerStats != null) ResetStats(); 
        else ReportDebug("PlayerStats no está asignado en PlayerStatsManager.", 3);
    }

    /// <summary>
    /// Función que reinicia todas las estadísticas a sus valores base definidos en PlayerStats.
    /// </summary> 
    public void ResetStats()
    {
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
        baseStats[StatType.HealthDrainAmount] = playerStats.HealthDrainAmount;

        foreach (var kvp in baseStats) currentStats[kvp.Key] = kvp.Value;

        ReportDebug("Estadísticas del jugador reiniciadas a los valores base.", 1);
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
        float baseValue = baseStats[type];
        float modifierValue = isPercentage ? baseValue * amount : amount;

        currentStats[type] += modifierValue;
        OnStatChanged?.Invoke(type, currentStats[type]);

        ReportDebug($"[{type}] {(amount >= 0 ? "Buff" : "Debuff")} aplicado: " +
                  $"{(isPercentage ? amount * 100 + "%" : amount.ToString())}. " +
                  $"Nuevo valor: {currentStats[type]}", 1);

        if (!isTemporary) return; // Buff permanente => no se remueve automáticamente

        if (isByRooms) StartCoroutine(RemoveModifierAfterRooms(type, modifierValue, roomsDuration));
        else if (duration > 0f) StartCoroutine(RemoveModifierAfterTime(type, modifierValue, duration));
    }

    private IEnumerator RemoveModifierAfterTime(StatType type, float modifierValue, float duration)
    {
        yield return new WaitForSeconds(duration);

        currentStats[type] -= modifierValue;
        OnStatChanged?.Invoke(type, currentStats[type]);

        ReportDebug($"[{type}] buff/debuff temporal terminó después de {duration} segundos. Valor actual: {currentStats[type]}", 1);
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

        ReportDebug($"[{type}] buff/debuff terminó tras {rooms} salas. Valor actual: {currentStats[type]}", 1);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    /// <summary> 
    /// Función de depuración para reportar mensajes en la consola de Unity. 
    /// </summary> 
    /// <<param name="message">Mensaje a reportar.</param> >
    /// <param name="reportPriorityLevel">Nivel de prioridad: Debug, Warning, Error.</param>
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1: Debug.Log($"[PlayerStatsManager] {message}"); 
                break;
            case 2: Debug.LogWarning($"[PlayerStatsManager] {message}"); 
                break;
            case 3: Debug.LogError($"[PlayerStatsManager] {message}"); 
                break;
            default: Debug.Log($"[PlayerStatsManager] {message}");
                break;
        }
    }
}