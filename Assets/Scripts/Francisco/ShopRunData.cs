using System.Collections.Generic;
using UnityEngine;

public static class ShopRunData
{
    private static readonly List<ShopItem> currentRunItems = new List<ShopItem>();

    public static List<ShopItem> CurrentRunItems
    {
        get { return currentRunItems; }
    }

    public static void ResetRunItems()
    {
        currentRunItems.Clear();
        Debug.Log("[ShopRunData] Inventario de la carrera actual reiniciado.");
    }
}