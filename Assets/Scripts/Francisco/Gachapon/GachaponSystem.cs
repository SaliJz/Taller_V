using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public struct GachaponResult
{
    public GachaponEffectData effectPair;
    public EffectRarity rarity;
}

public class GachaponSystem : MonoBehaviour
{
    private PlayerStatsManager playerStatsManager;

    [Header("Configuración y Pool de Efectos")]
    public List<GachaponEffectData> allEffects;


    private Dictionary<EffectRarity, float> baseRarityChances = new Dictionary<EffectRarity, float>()
    {
        { EffectRarity.Comun, 60f },
        { EffectRarity.Raro, 25f },
        { EffectRarity.Epico, 12f },
        { EffectRarity.Legendario, 3f }
    };

    private const float LUCK_BONUS_PER_STACK = 6f;
    private const int MAX_LUCK_STACKS = 3;

    private void Awake()
    {
        playerStatsManager = FindAnyObjectByType<PlayerStatsManager>();

        if (playerStatsManager == null)
        {
            Debug.LogError("GachaponSystem no pudo encontrar el PlayerStatsManager en la escena.");
        }
    }

    public GachaponResult PullGachapon()
    {
        if (playerStatsManager == null) return new GachaponResult { effectPair = null, rarity = EffectRarity.Comun };

        GachaponEffectData selectedEffectPair = SelectEffectPairWithRarity(allEffects);

        if (selectedEffectPair != null)
        {
            return new GachaponResult
            {
                effectPair = selectedEffectPair,
                rarity = selectedEffectPair.rarity
            };
        }

        return new GachaponResult { effectPair = null, rarity = EffectRarity.Comun };
    }

    private GachaponEffectData SelectEffectPairWithRarity(List<GachaponEffectData> effectPool)
    {
        float luckStacks = playerStatsManager.GetCurrentStat(StatType.LuckStack);
        luckStacks = Mathf.Min(luckStacks, MAX_LUCK_STACKS);
        float luckBonus = luckStacks * LUCK_BONUS_PER_STACK;

        Dictionary<EffectRarity, float> currentRarityChances = new Dictionary<EffectRarity, float>(baseRarityChances);

        if (luckBonus > 0)
        {
            float bonus = luckBonus;

            float transfer1 = Mathf.Min(bonus, currentRarityChances[EffectRarity.Comun] - 0.01f);
            currentRarityChances[EffectRarity.Comun] -= transfer1;
            currentRarityChances[EffectRarity.Raro] += transfer1;
            bonus -= transfer1;

            if (bonus > 0)
            {
                float transfer2 = Mathf.Min(bonus, currentRarityChances[EffectRarity.Raro] - 0.01f);
                currentRarityChances[EffectRarity.Raro] -= transfer2;
                currentRarityChances[EffectRarity.Epico] += transfer2;
                bonus -= transfer2;
            }

            if (bonus > 0)
            {
                float transfer3 = Mathf.Min(bonus, currentRarityChances[EffectRarity.Epico] - 0.01f);
                currentRarityChances[EffectRarity.Epico] -= transfer3;
                currentRarityChances[EffectRarity.Legendario] += transfer3;
            }
        }

        EffectRarity selectedRarity = SelectRarity(currentRarityChances);

        List<GachaponEffectData> rarityPool = effectPool
            .Where(e => e.rarity == selectedRarity)
            .ToList();

        if (!rarityPool.Any()) return SelectEffectFromPool(effectPool);

        return SelectEffectFromPool(rarityPool);
    }

    private GachaponEffectData SelectEffectFromPool(List<GachaponEffectData> pool)
    {
        float totalWeight = pool.Sum(e => e.poolProbability);
        float roll = Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        foreach (GachaponEffectData effect in pool)
        {
            currentWeight += effect.poolProbability;
            if (roll <= currentWeight)
            {
                return effect;
            }
        }
        return pool.Last();
    }

    private EffectRarity SelectRarity(Dictionary<EffectRarity, float> rarityChances)
    {
        float totalWeight = rarityChances.Sum(kvp => kvp.Value);
        float roll = Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        List<EffectRarity> orderedRarities = new List<EffectRarity>
            { EffectRarity.Comun, EffectRarity.Raro, EffectRarity.Epico, EffectRarity.Legendario };

        foreach (EffectRarity rarity in orderedRarities)
        {
            if (rarityChances.ContainsKey(rarity))
            {
                currentWeight += rarityChances[rarity];
                if (roll <= currentWeight)
                {
                    return rarity;
                }
            }
        }
        return EffectRarity.Comun;
    }

    public void ApplyEffect(GachaponResult result)
    {
        GachaponEffectData effectPair = result.effectPair;

        if (effectPair == null) return;

        foreach (var modifier in effectPair.advantageModifiers)
        {
            ApplySingleModifier(modifier, true);
        }

        foreach (var modifier in effectPair.disadvantageModifiers)
        {
            ApplySingleModifier(modifier, false);
        }

        Debug.Log($"Gachapon aplicado: {effectPair.effectName}. Rareza: {effectPair.rarity}. Total Ventajas: {effectPair.advantageModifiers.Count}. Total Desventajas: {effectPair.disadvantageModifiers.Count}");
    }

    private void ApplySingleModifier(GachaponModifier modifier, bool isAdvantage)
    {
        float finalModifierValue = modifier.modifierValue;

        if (modifier.isPercentage)
        {
            float currentStatValue = playerStatsManager.GetCurrentStat(modifier.statType);
            finalModifierValue = currentStatValue * (modifier.modifierValue / 100f);
        }

        string type = isAdvantage ? "Ventaja" : "Desventaja";

        if (modifier.durationType == EffectDurationType.Permanent)
        {
            playerStatsManager.ModifyPermanentStat(modifier.statType, finalModifierValue);
        }
        else if (modifier.durationType == EffectDurationType.Rounds)
        {
            playerStatsManager.ApplyTemporaryStatByRooms(modifier.statType, finalModifierValue, (int)modifier.durationValue);
        }
        else if (modifier.durationType == EffectDurationType.Time)
        {
            playerStatsManager.ApplyTemporaryStatByTime(modifier.statType, finalModifierValue, modifier.durationValue);
        }

        Debug.Log($"   -> {type} aplicado: {modifier.statType}. Valor: {finalModifierValue} (Original: {modifier.modifierValue}{(modifier.isPercentage ? "%" : "")}). Duración: {modifier.durationType} por {modifier.durationValue}");
    }
}