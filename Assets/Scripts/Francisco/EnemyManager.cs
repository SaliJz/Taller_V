using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class EnemyManager : MonoBehaviour
{
    public DungeonGenerator dungeonGenerator;
    public Room parentRoom;
    public GameObject[] enemyPrefabs;
    public int enemiesPerWave = 3;
    public int maxWaves = 3;
    public float timeBetweenWaves = 3.0f;
    private int currentWave = 0;
    private List<GameObject> activeEnemies = new List<GameObject>();
    private ConnectionPoint entrancePoint;

    public IEnumerator StartCombatEncounter(ConnectionPoint entrance)
    {
        this.entrancePoint = entrance;

        parentRoom.LockAllDoors();

        while (currentWave < maxWaves)
        {
            yield return StartCoroutine(SpawnWave());

            yield return new WaitUntil(() => activeEnemies.All(e => e == null));

            currentWave++;

            if (currentWave < maxWaves)
            {
                yield return new WaitForSeconds(timeBetweenWaves);
            }
        }

        parentRoom.UnlockExitDoors(entrancePoint);
    }

    private IEnumerator SpawnWave()
    {
        activeEnemies.Clear();

        if (parentRoom.spawnAreas == null || parentRoom.spawnAreas.Length == 0)
        {
            yield break;
        }

        BoxCollider spawnArea = parentRoom.spawnAreas[Random.Range(0, parentRoom.spawnAreas.Length)];

        for (int i = 0; i < enemiesPerWave; i++)
        {
            GameObject enemyPrefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];

            Vector3 center = spawnArea.bounds.center;
            Vector3 size = spawnArea.bounds.size;

            float x = Random.Range(center.x - size.x / 2, center.x + size.x / 2);
            float y = center.y;
            float z = Random.Range(center.z - size.z / 2, center.z + size.z / 2);

            Vector3 spawnPosition = new Vector3(x, y, z);

            GameObject newEnemyObj = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);

            var healthController = newEnemyObj.GetComponent<HealthController>();
            if (healthController != null)
            {
                healthController.GetComponent<EnemyController>().OnDeath += OnEnemyDefeated;
            }

            activeEnemies.Add(newEnemyObj);

            yield return null;
        }
    }

    public void OnEnemyDefeated()
    {
    }
}