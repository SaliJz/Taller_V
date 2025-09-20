using UnityEngine;
using System.Collections.Generic;

public class ShopRoomManager : MonoBehaviour
{
    [Header("Shop Item Spawn Locations")]
    public List<Transform> spawnLocations;

    private ShopManager shopManager;

    private void Awake()
    {
        shopManager = FindAnyObjectByType<ShopManager>();
        if (shopManager == null)
        {
            Debug.LogError("ShopManager no encontrado. El cuarto de la tienda no funcionará.");
        }
    }

    private void Start()
    {
        if (shopManager != null)
        {
            shopManager.GenerateShopItems(spawnLocations);
        }
    }
}