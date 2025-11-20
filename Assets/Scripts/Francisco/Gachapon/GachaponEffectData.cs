using UnityEngine;
using System.Collections.Generic;

[System.Serializable] public enum EffectRarity { Comun, Raro, Epico, Legendario }
[System.Serializable] public enum EffectDurationType { Permanent, Rounds, Time }

[System.Serializable]
public class GachaponModifier
{
    public StatType statType;
    public float modifierValue;
    public bool isPercentage = false;
    public EffectDurationType durationType = EffectDurationType.Permanent;
    public float durationValue = 0;
}

[CreateAssetMenu(fileName = "NewGachaponEffect", menuName = "Gachapon/Effect Data", order = 1)]
public class GachaponEffectData : ScriptableObject
{
    [Header("Identificación y Probabilidad")]
    public string effectName = "Nuevo Par de Efectos";
    public EffectRarity rarity = EffectRarity.Comun;
    public float poolProbability = 1f;

    [Header("Efectos (Ventaja)")]
    public List<GachaponModifier> advantageModifiers = new List<GachaponModifier>();

    [Header("Efectos (Desventaja)")]
    public List<GachaponModifier> disadvantageModifiers = new List<GachaponModifier>();

    public bool HasAdvantage => advantageModifiers.Count > 0;
    public bool HasDisadvantage => disadvantageModifiers.Count > 0;
}