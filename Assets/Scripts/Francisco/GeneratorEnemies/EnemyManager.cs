using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    public DungeonGenerator dungeonGenerator;
    public Room parentRoom;

    [Header("Enemy Prefabs")]
    public GameObject[] enemyPrefabs;

    [Header("Wave Settings")]
    public int maxWaves = 3;
    public int enemiesPerWave = 3;
    public float timeBetweenWaves = 5f;

    [Header("Visual Effects")]
    public GameObject spawnEffectPrefab;

    private int currentWave = 0;
    private List<GameObject> activeEnemies = new List<GameObject>();

    void Start()
    {
        if (parentRoom == null)
        {
            parentRoom = GetComponentInParent<Room>();
        }
    }

    public IEnumerator StartCombatEncounter(ConnectionPoint entrancePoint, DungeonGenerator dg, GameObject effectPrefab)
    {
        dungeonGenerator = dg;
        this.spawnEffectPrefab = effectPrefab;

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

        Debug.Log("Combate finalizado. Notificando al DungeonGenerator.");
        dungeonGenerator.OnCombatEnded(parentRoom, entrancePoint);
    }

    IEnumerator SpawnWaves()
    {
        while (currentWave < maxWaves)
        {
            yield return StartCoroutine(SpawnEnemiesWithEffect());
            yield return new WaitForSeconds(timeBetweenWaves);

            while (activeEnemies.Count > 0)
            {
                activeEnemies.RemoveAll(enemy => enemy == null);
                yield return null;
            }
            currentWave++;
        }
    }

    IEnumerator SpawnEnemiesWithEffect()
    {
        if (parentRoom == null || parentRoom.spawnAreas.Length == 0 || enemyPrefabs.Length == 0)
        {
            Debug.LogWarning("No se pueden generar enemigos. Faltan prefabs o puntos de aparición en la sala de combate.");
            yield break;
        }

        List<GameObject> instantiatedEffects = new List<GameObject>();
        List<Vector3> spawnPositions = new List<Vector3>();

        for (int i = 0; i < enemiesPerWave; i++)
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

        for (int i = 0; i < enemiesPerWave; i++)
        {
            if (i < instantiatedEffects.Count && instantiatedEffects[i] != null)
            {
                Destroy(instantiatedEffects[i]);
            }

            GameObject enemyPrefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
            GameObject newEnemy = Instantiate(enemyPrefab, spawnPositions[i], Quaternion.identity);
            activeEnemies.Add(newEnemy);
        }
    }
}