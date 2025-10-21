using UnityEngine;
using System.Collections.Generic;
using System;

[Serializable]
public class EnemyWaveDetail
{
    public GameObject EnemyPrefab;
    [Range(1, 20)] public int Count = 1;
}

[Serializable]
public class PredefinedWave
{
    public List<EnemyWaveDetail> enemiesInWave;
}


[Serializable]
public class PredefinedCombatCombination
{
    public string CombinationName = "Combinacion #";

    [Header("Configuraci�n de Oleadas")]
    public List<PredefinedWave> waves;
    public float timeBetweenWaves = 5f;
}