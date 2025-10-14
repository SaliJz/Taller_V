using UnityEngine;

public abstract class ItemEffectBase : ScriptableObject
{
    [Header("Identificaci�n")]
    public string EffectID;

    public abstract void ApplyEffect(PlayerStatsManager statsManager);

    public abstract void RemoveEffect(PlayerStatsManager statsManager);
}