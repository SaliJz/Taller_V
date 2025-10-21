using UnityEngine;
using System;

[Serializable]
public class EnemyTierConfig
{
    public string TierName; 
    public GameObject EnemyPrefab;

    [Header("Pesos y Proporci�n")]
    public float SpawnWeight = 1f;
}