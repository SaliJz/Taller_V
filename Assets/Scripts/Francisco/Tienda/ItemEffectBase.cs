using UnityEngine;

/// <summary>
/// Categorías de efectos para organización
/// </summary>
public enum EffectCategory
{
    Combat,      // Efectos de combate
    Defense,     // Efectos defensivos
    Utility,     // Efectos de utilidad
    Healing,     // Efectos de curación
    Damage,      // Efectos de daño adicional
    Special      // Efectos especiales/únicos
}

public abstract class ItemEffectBase : ScriptableObject
{
    [Header("Identificación")]
    public string EffectID;

    [TextArea(2, 4)]
    [Tooltip("Descripción detallada del efecto para mostrar en el inventario")]
    public string effectDescription = "Descripción del efecto.";

    [Tooltip("Categoría del efecto (para organización visual)")]
    public EffectCategory category = EffectCategory.Combat;

    /// <summary>
    /// Aplica el efecto al jugador
    /// </summary>
    public abstract void ApplyEffect(PlayerStatsManager statsManager);

    /// <summary>
    /// Remueve el efecto del jugador
    /// </summary>
    public abstract void RemoveEffect(PlayerStatsManager statsManager);

    /// <summary>
    /// Genera una descripción formateada del efecto con valores específicos
    /// </summary>
    public virtual string GetFormattedDescription()
    {
        return effectDescription;
    }

    /// <summary>
    /// Obtiene un resumen corto del efecto
    /// </summary>
    public virtual string GetShortSummary()
    {
        return $"<b>{EffectID}:</b> {effectDescription}";
    }
}