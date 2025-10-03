using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    private DungeonGenerator dungeonGenerator;
    private Room parentRoom;
    private CombatContents combatConfig;

    private GameObject[] defaultEnemyPrefabs;
    private GameObject spawnEffectPrefab;

    private List<GameObject> activeEnemies = new List<GameObject>();

    public void Initialize(DungeonGenerator dg, Room parent, CombatContents rules, GameObject effectPrefab, GameObject[] defaultEnemies)
    {
        dungeonGenerator = dg;
        parentRoom = parent;
        combatConfig = rules;
        spawnEffectPrefab = effectPrefab;
        defaultEnemyPrefabs = defaultEnemies;
    }

    public IEnumerator StartCombatEncounter(ConnectionPoint entrancePoint)
    {
        if (combatConfig == null || combatConfig.waves == null || combatConfig.waves.Count == 0)
        {
            dungeonGenerator.OnCombatEnded(parentRoom, entrancePoint);
            yield break;
        }

        if (parentRoom != null)
        {
            parentRoom.LockAllDoors();
        }

        int entranceDoorIndex = System.Array.IndexOf(parentRoom.connectionPoints, entrancePoint);
        if (entranceDoorIndex != -1 && parentRoom.connectionDoors.Length > entranceDoorIndex && parentRoom.connectionDoors[entranceDoorIndex] != null)
        {
            parentRoom.connectionDoors[entranceDoorIndex].SetActive(true);
        }

        yield return StartCoroutine(SpawnWaves());

        dungeonGenerator.OnCombatEnded(parentRoom, entrancePoint);
    }

    IEnumerator SpawnWaves()
    {
        for (int currentWave = 0; currentWave < combatConfig.waves.Count; currentWave++)
        {
            EnemyWave wave = combatConfig.waves[currentWave];

            yield return StartCoroutine(SpawnEnemiesWithEffect(wave));

            while (activeEnemies.Count > 0)
            {
                activeEnemies.RemoveAll(enemy => enemy == null);
                yield return null;
            }

            if (currentWave < combatConfig.waves.Count - 1)
            {
                yield return new WaitForSeconds(combatConfig.timeBetweenWaves);
            }
        }
    }

    IEnumerator SpawnEnemiesWithEffect(EnemyWave wave)
    {
        if (parentRoom == null || parentRoom.spawnAreas.Length == 0)
        {
            yield break;
        }

        GameObject[] prefabsToUse;
        int enemyCount = wave.enemyCount;

        if (wave.enemyPrefabs != null && wave.enemyPrefabs.Length > 0)
        {
            prefabsToUse = wave.enemyPrefabs;
        }
        else if (defaultEnemyPrefabs != null && defaultEnemyPrefabs.Length > 0)
        {
            prefabsToUse = defaultEnemyPrefabs;
        }
        else
        {
            yield break;
        }

        List<GameObject> instantiatedEffects = new List<GameObject>();
        List<Vector3> spawnPositions = new List<Vector3>();

        for (int i = 0; i < enemyCount; i++)
        {
            BoxCollider spawnArea = parentRoom.spawnAreas[Random.Range(0, parentRoom.spawnAreas.Length)];
            Vector3 spawnPosition = new Vector3(
                Random.Range(spawnArea.bounds.min.x, spawnArea.bounds.max.x),
                spawnArea.transform.position.y,
                Random.Range(spawnArea.bounds.min.z, spawnArea.bounds.max.z)
            );
            spawnPositions.Add(spawnPosition);

            if (spawnEffectPrefab != null)
            {
                GameObject effectInstance = Instantiate(spawnEffectPrefab, spawnPosition, Quaternion.identity);
                instantiatedEffects.Add(effectInstance);
            }
        }

        yield return new WaitForSeconds(2.0f);

        for (int i = 0; i < enemyCount; i++)
        {
            if (i < instantiatedEffects.Count && instantiatedEffects[i] != null)
            {
                Destroy(instantiatedEffects[i]);
            }

            GameObject enemyPrefab = prefabsToUse[Random.Range(0, prefabsToUse.Length)];
            GameObject newEnemy = Instantiate(enemyPrefab, spawnPositions[i], Quaternion.identity);

            if (newEnemy != null)
            {
                activeEnemies.Add(newEnemy);
            }
        }
    }
}