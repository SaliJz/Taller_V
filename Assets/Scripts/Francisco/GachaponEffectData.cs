using UnityEngine;

[System.Serializable]
public enum EffectRarity { Comun, Raro, Epico, Legendario }

[System.Serializable]
public enum EffectDurationType { Permanent, Rounds, Time }

[CreateAssetMenu(fileName = "NewGachaponEffect", menuName = "Gachapon/Effect Data", order = 1)]
public class GachaponEffectData : ScriptableObject
{
    [Header("Identificación y Tipo")]
    public string effectName = "Nuevo Efecto";
    public bool isAdvantage = true; 
    public EffectRarity rarity = EffectRarity.Comun; 

    [Header("Probabilidad (Pool Interno)")]
    public float poolProbability = 1f;

    [Header("Efecto")]
    public StatType statType;
    public float modifierValue;

    [Header("Duración")]
    public EffectDurationType durationType = EffectDurationType.Permanent;
    public float durationValue = 0; 
}