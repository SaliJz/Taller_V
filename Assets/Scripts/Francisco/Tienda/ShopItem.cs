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
/// Categor�as de �tems para el inventario
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
/// Rareza de los �tems
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

    [Header("Categorizaci�n")]
    public ItemCategory category = ItemCategory.Reliquia;
    public ItemRarity rarity = ItemRarity.Comun;

    [Header("Tipo de Item")]
    public bool isAmulet = false;
    public bool isTemporary = false;
    public bool isByRooms = false;
    [Tooltip("Duraci�n en segundos (si es temporal por tiempo)")]
    public float temporaryDuration = 0f;
    [Tooltip("Duraci�n en rooms (si es temporal por rooms)")]
    public int temporaryRooms = 0;

    [Header("Visual")]
    [Tooltip("Sprite del �tem para el inventario")]
    public Sprite itemIcon;

    [Header("Comportamientos/Efectos Eventuales")]
    public List<ItemEffectBase> behavioralEffects;

    /// <summary>
    /// Obtiene el color asociado a la rareza del �tem
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