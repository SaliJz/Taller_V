using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public struct GachaponResult
{
    public GachaponEffectData effect;
    public EffectRarity rarity;
}

public class GachaponSystem : MonoBehaviour
{
    private PlayerStatsManager playerStatsManager;

    [Header("Configuración y Pool de Efectos")]
    public List<GachaponEffectData> allEffects;

    private const float ADVANTAGE_CHANCE = 70f;

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
        if (playerStatsManager == null) return new GachaponResult { effect = null, rarity = EffectRarity.Comun };

        bool isAdvantage = Random.Range(0f, 100f) < ADVANTAGE_CHANCE;

        List<GachaponEffectData> filteredPool = allEffects
            .Where(e => e.isAdvantage == isAdvantage)
            .ToList();

        if (!filteredPool.Any()) return new GachaponResult { effect = null, rarity = EffectRarity.Comun };

        GachaponEffectData selectedEffect;

        if (isAdvantage)
        {
            selectedEffect = SelectAdvantageWithRarity(filteredPool);
        }
        else
        {
            selectedEffect = SelectEffectFromPool(filteredPool);
        }

        if (selectedEffect != null)
        {
            return new GachaponResult
            {
                effect = selectedEffect,
                rarity = selectedEffect.rarity
            };
        }

        return new GachaponResult { effect = null, rarity = EffectRarity.Comun };
    }

    private GachaponEffectData SelectAdvantageWithRarity(List<GachaponEffectData> advantagePool)
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

        List<GachaponEffectData> rarityPool = advantagePool
            .Where(e => e.rarity == selectedRarity)
            .ToList();

        if (!rarityPool.Any()) return SelectEffectFromPool(advantagePool);

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

    public void ApplyEffect(GachaponEffectData effect)
    {
        if (effect.durationType == EffectDurationType.Permanent)
        {
            playerStatsManager.ModifyPermanentStat(effect.statType, effect.modifierValue);
        }
        else if (effect.durationType == EffectDurationType.Rounds)
        {
            playerStatsManager.ApplyTemporaryStatByRooms(effect.statType, effect.modifierValue, (int)effect.durationValue);
        }
        else if (effect.durationType == EffectDurationType.Time)
        {
            playerStatsManager.ApplyTemporaryStatByTime(effect.statType, effect.modifierValue, effect.durationValue);
        }

        Debug.Log($"Gachapon aplicado: {effect.effectName}. Valor: {effect.modifierValue}. Duración: {effect.durationType} por {effect.durationValue}");
    }
}