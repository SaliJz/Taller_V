using UnityEngine;

/// <summary>
/// Tipo de ranura mecánica que puede ocupar un (ItemCategory que modifique mecánicas)
/// </summary>
public enum MechanicSlotType
{
    Melee = 0,
    Ranged = 1,
    Dash = 2
}

/// <summary>
/// Asocia un ShopItem de tipo (ItemCategory que modifique mecánicas) con su ranura mecánica.
/// </summary>
[System.Serializable]
public struct MechanicItemEntry
{
    public ShopItem item;
    public MechanicSlotType slotType;
}