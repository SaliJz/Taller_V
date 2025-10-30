using UnityEngine;

/// <summary>
/// Categor�as de efectos para organizaci�n
/// </summary>
public enum EffectCategory
{
    Combat,      // Efectos de combate
    Defense,     // Efectos defensivos
    Utility,     // Efectos de utilidad
    Healing,     // Efectos de curaci�n
    Damage,      // Efectos de da�o adicional
    Special      // Efectos especiales/�nicos
}

public abstract class ItemEffectBase : ScriptableObject
{
    [Header("Identificaci�n")]
    public string EffectID;

    [TextArea(2, 4)]
    [Tooltip("Descripci�n detallada del efecto para mostrar en el inventario")]
    public string effectDescription = "Descripci�n del efecto.";

    [Tooltip("Categor�a del efecto (para organizaci�n visual)")]
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
    /// Genera una descripci�n formateada del efecto con valores espec�ficos
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