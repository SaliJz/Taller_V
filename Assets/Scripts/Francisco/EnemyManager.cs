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
    public int maxWaves = 1;
    public int enemiesPerWave = 3;
    public float timeBetweenWaves = 5f;

    private int currentWave = 0;
    private List<GameObject> activeEnemies = new List<GameObject>();

    void Start()
    {
        if (parentRoom == null)
        {
            parentRoom = GetComponentInParent<Room>();
        }
    }

    public IEnumerator StartCombatEncounter(ConnectionPoint entrancePoint, DungeonGenerator dg)
    {
        dungeonGenerator = dg;

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
            SpawnEnemiesInCurrentWave();
            yield return new WaitForSeconds(timeBetweenWaves);

            while (activeEnemies.Count > 0)
            {
                activeEnemies.RemoveAll(enemy => enemy == null);
                yield return null;
            }
            currentWave++;
        }
    }

    void SpawnEnemiesInCurrentWave()
    {
        if (parentRoom != null && parentRoom.spawnAreas.Length > 0 && enemyPrefabs.Length > 0)
        {
            for (int i = 0; i < enemiesPerWave; i++)
            {
                GameObject enemyPrefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
                BoxCollider spawnArea = parentRoom.spawnAreas[Random.Range(0, parentRoom.spawnAreas.Length)];

                Vector3 spawnPosition = new Vector3(
                    Random.Range(spawnArea.bounds.min.x, spawnArea.bounds.max.x),
                    spawnArea.transform.position.y,
                    Random.Range(spawnArea.bounds.min.z, spawnArea.bounds.max.z)
                );

                GameObject newEnemy = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
                activeEnemies.Add(newEnemy);
            }
        }
        else
        {
            Debug.LogWarning("No se pueden generar enemigos. Faltan prefabs o puntos de aparición en la sala de combate.");
        }
    }
}