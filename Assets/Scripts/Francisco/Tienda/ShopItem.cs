using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct ItemEffect
{
    public StatType type;
    public float amount;
    public bool isPercentage;
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
}