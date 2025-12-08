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

    private bool IsInverseStat(StatType statType)
    {
        switch (statType)
        {
            case StatType.DamageTaken:
            case StatType.HealthDrainAmount:
            case StatType.Gravity:
            case StatType.DashCooldownPost:
            case StatType.KnockbackReceived:
            case StatType.StaminaConsumption:
                return true;
            default:
                return false;
        }
    }

    private string FormatStatEffect(ItemEffect effect, bool isOriginalBenefit)
    {
        string colorTag = isOriginalBenefit ? "<color=#00FF00>" : "<color=#FF0000>";

        if (effect.amount == 0f)
        {
            return $"<color=#A0A0A0>0.0 a {GetStatTranslation(effect.type)}</color>";
        }

        bool isInverse = IsInverseStat(effect.type);
        string sign;

        bool shouldBePositive;

        if (isInverse)
        {
            shouldBePositive = (isOriginalBenefit && effect.amount < 0) ||
                              (!isOriginalBenefit && effect.amount > 0);
        }
        else
        {
            shouldBePositive = (isOriginalBenefit && effect.amount > 0) ||
                              (!isOriginalBenefit && effect.amount < 0);
        }

        sign = shouldBePositive ? "+" : "-";

        float displayAmount = Mathf.Abs(effect.amount);

        string amountString = sign +
                              displayAmount.ToString("F1").Replace(",", ".") +
                              (effect.isPercentage ? "%" : "");

        string statName = GetStatTranslation(effect.type);

        return $"{colorTag}{amountString} a {statName}</color>";
    }

    public string GetStatTranslation(StatType statType)
    {
        switch (statType)
        {
            case StatType.MaxHealth:
                return "Salud Máxima";
            case StatType.DamageTaken:
                return "Daño Recibido";
            case StatType.HealthDrainAmount:
                return "Drenaje de Vida (PS)";

            case StatType.MoveSpeed:
                return "Velocidad de Movimiento";
            case StatType.Gravity:
                return "Gravedad";
            case StatType.DashRangeMultiplier:
                return "Alcance de Dash";
            case StatType.DashCooldownPost:
                return "Enfriamiento de Dash";
            case StatType.KnockbackReceived:
                return "Empuje Recibido";
            case StatType.StaminaConsumption:
                return "Consumo de Aguante/Estamina";

            case StatType.AttackDamage:
                return "Daño General de Ataque";
            case StatType.AttackSpeed:
                return "Velocidad de Ataque General";
            case StatType.MeleeAttackDamage:
                return "Daño C/Cuerpo";
            case StatType.MeleeAttackSpeed:
                return "Velocidad de Ataque C/Cuerpo";
            case StatType.MeleeRadius:
                return "Radio de Ataque C/Cuerpo";
            case StatType.MeleeComboDisplacement:
                return "Desplazamiento de Combo C/Cuerpo";
            case StatType.CriticalChance:
                return "Probabilidad Crítica";
            case StatType.CriticalDamageMultiplier:
                return "Multiplicador de Daño Crítico";
            case StatType.LifestealOnKill:
                return "Robo de Vida por Eliminación";

            case StatType.ShieldAttackDamage:
                return "Daño de Ataque de Escudo";
            case StatType.ShieldSpeed:
                return "Velocidad de Lanzamiento de Escudo";
            case StatType.ShieldMaxDistance:
                return "Distancia Máxima de Escudo";
            case StatType.ShieldMaxRebounds:
                return "Rebotes Máximos de Escudo";
            case StatType.ShieldReboundRadius:
                return "Radio de Rebote de Escudo";
            case StatType.ShieldBlockUpgrade:
                return "Mejora de Bloqueo de Escudo";
            case StatType.ShieldPushForce:
                return "Fuerza de Empuje de Escudo";
            case StatType.ShieldReturnSpeed:
                return "Velocidad de Retorno de Escudo";

            case StatType.LuckStack:
                return "Suerte Acumulada";
            case StatType.EssenceCostReduction:
                return "Reducción de Costo de Esencia";
            case StatType.ShopPriceReduction:
                return "Reducción de Precio de Tienda";
            case StatType.HealthPerRoomRegen:
                return "Regen. de Vida por Sala";

            default:
                return statType.ToString();
        }
    }
}