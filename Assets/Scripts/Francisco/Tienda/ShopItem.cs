using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct ItemEffect
{
    public StatType type;
    public float amount;
    public bool isPercentage;
}

/// <summary>
/// Categorías de ítems para el inventario
/// </summary>
public enum ItemCategory
{
    Reliquia,      
    Ganga,         
    Acondicionador,
    Potenciador,   
    Debilitador,   
    Maldicion,
    Amuleto
}

/// <summary>
/// Rareza de los ítems
/// </summary>
public enum ItemRarity
{
    Comun,
    Raro,
    Epico,
    Legendario
}

[CreateAssetMenu(fileName = "NewShopItem", menuName = "Shop/Shop Item")]
public class ShopItem : ScriptableObject
{
    [Header("Item Info")]
    public string itemName;
    [TextArea] public string description;

    [Header("Stats")]
    public float cost;
    public List<ItemEffect> benefits;
    public List<ItemEffect> drawbacks;

    [Header("Categorización")]
    public ItemCategory category = ItemCategory.Reliquia;
    public ItemRarity rarity = ItemRarity.Comun;

    [Header("Tipo de Item")]
    public bool isAmulet = false;
    public bool isTemporary = false;
    public bool isByRooms = false;
    [Tooltip("Duración en segundos (si es temporal por tiempo)")]
    public float temporaryDuration = 0f;
    [Tooltip("Duración en rooms (si es temporal por rooms)")]
    public int temporaryRooms = 0;

    [Header("Visual")]
    [Tooltip("Sprite del ítem para el inventario")]
    public Sprite itemIcon;

    [Header("Comportamientos/Efectos Eventuales")]
    public List<ItemEffectBase> behavioralEffects;

    /// <summary>
    /// Obtiene el color asociado a la rareza del ítem
    /// </summary>
    public Color GetRarityColor()
    {
        switch (rarity)
        {
            case ItemRarity.Legendario:
                return new Color(1f, 0.84f, 0f); // Dorado
            case ItemRarity.Epico:
                return new Color(0.64f, 0.21f, 0.93f); // Morado
            case ItemRarity.Raro:
                return new Color(0.25f, 0.5f, 1f); // Azul
            case ItemRarity.Comun:
            default:
                return new Color(0.6f, 0.6f, 0.6f); // Gris
        }
    }
}