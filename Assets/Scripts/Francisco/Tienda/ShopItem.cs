using System.Collections.Generic;
using UnityEngine;
using System.Text;

[System.Serializable]
public struct ItemEffect
{
    public StatType type;
    public float amount;
    public bool isPercentage;
}

public enum ItemCategory
{
    AttributeModifiers,
    SkillEnhancers,
    CounterDistortions
}

public enum ItemRarity
{
    Normal,
    Raro,
    SuperRaro
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
    public ItemCategory category = ItemCategory.AttributeModifiers;
    public ItemRarity rarity = ItemRarity.Normal;

    [Tooltip("Probabilidad individual de ser seleccionado DENTRO de su rareza. Mayor número = más probable.")]
    public float individualRarityWeight = 1.0f; 

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

    public Color GetRarityColor()
    {
        switch (rarity)
        {
            case ItemRarity.SuperRaro:
                return new Color(1f, 0.84f, 0f); // Dorado
            case ItemRarity.Raro:
                return new Color(0.25f, 0.5f, 1f); // Azul
            case ItemRarity.Normal:
            default:
                return new Color(0.6f, 0.6f, 0.6f); // Gris
        }
    }

    public string GetFormattedDescriptionAndStats()
    {
        StringBuilder sb = new StringBuilder();

        if (!string.IsNullOrEmpty(description))
        {
            sb.AppendLine(description);
        }

        sb.AppendLine();

        if (benefits != null && benefits.Count > 0)
        {
            foreach (var effect in benefits)
            {
                sb.AppendLine(FormatStatEffect(effect, true));
            }
        }

        if (drawbacks != null && drawbacks.Count > 0)
        {
            foreach (var effect in drawbacks)
            {
                sb.AppendLine(FormatStatEffect(effect, false));
            }
        }

        return sb.ToString();
    }

    private string FormatStatEffect(ItemEffect effect, bool isBenefit)
    {
        string colorTag = isBenefit ? "<color=#00FF00>" : "<color=#FF0000>";

        string amountString = (isBenefit ? "+" : "-") +
                              Mathf.Abs(effect.amount).ToString("F0") +
                              (effect.isPercentage ? "%" : "");

        string statName = effect.type.ToString().Replace("StatType.", "");

        return $"{colorTag}{amountString} a {statName}</color>";
    }
}