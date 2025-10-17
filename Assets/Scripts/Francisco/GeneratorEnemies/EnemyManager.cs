using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class EnemyManager : MonoBehaviour
{
    private DungeonGenerator dungeonGenerator;
    private Room parentRoom;
    private CombatContents combatConfig;

    private GameObject[] defaultEnemyPrefabs;
    private GameObject spawnEffectPrefab;

    private List<GameObject> activeEnemies = new List<GameObject>();

    private bool isAuraActiveInThisRoom = false;
    private DevilAuraType activeAura = DevilAuraType.None;
    private ResurrectionLevel activeResurrectionLevel = ResurrectionLevel.None;
    private float auraCoveragePercent = 0f;
    private float initialHealthMultiplier = 1f;

    private struct EnemyMultipliers
    {
        public float HealthMultiplier { get; set; }
        public float DamageMultiplier { get; set; }
        public float SpeedMultiplier { get; set; }
    }

    private void Awake()
    {
        parentRoom = GetComponentInParent<Room>();
        dungeonGenerator = FindAnyObjectByType<DungeonGenerator>();
    }

    private void OnEnable()
    {
        if (DevilManipulationManager.Instance != null)
        {
            DevilManipulationManager.Instance.OnAuraManipulationActivated += HandleAuraManipulation;
        }
    }

    private void OnDisable()
    {
        if (DevilManipulationManager.Instance != null)
        {
            DevilManipulationManager.Instance.OnAuraManipulationActivated -= HandleAuraManipulation;
        }
    }

    private void HandleAuraManipulation(DevilAuraType aura, ResurrectionLevel level, float coverage)
    {
        isAuraActiveInThisRoom = true;
        activeAura = aura;
        activeResurrectionLevel = level;
        auraCoveragePercent = coverage;
        initialHealthMultiplier = 0.8f;

        if (DevilManipulationManager.Instance != null)
        {
            DevilManipulationManager.Instance.ResetCleanRoomsCounter();
        }
    }

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
            if (dungeonGenerator != null)
            {
                dungeonGenerator.OnCombatEnded(parentRoom, entrancePoint);
            }
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

        if (dungeonGenerator != null)
        {
            dungeonGenerator.StartRoomTimer();
        }

        for (int i = 0; i < combatConfig.waves.Count; i++)
        {
            EnemyWave wave = combatConfig.waves[i];

            yield return StartCoroutine(SpawnEnemiesInWave(wave));

            yield return StartCoroutine(WaitForAllEnemiesToDie());

            bool isLastWave = (i == combatConfig.waves.Count - 1);
            if (!isLastWave)
            {
                yield return new WaitForSeconds(combatConfig.timeBetweenWaves);
            }
        }

        if (dungeonGenerator != null)
        {
            dungeonGenerator.EndRoomTimer();
        }

        if (parentRoom != null)
        {
            parentRoom.UnlockExitDoors(entrancePoint);
        }

        if (dungeonGenerator != null)
        {
            dungeonGenerator.OnCombatEnded(parentRoom, entrancePoint);
        }
    }

    private IEnumerator SpawnEnemiesInWave(EnemyWave wave)
    {
        GameObject[] prefabsToUse = wave.enemyPrefabs.Length > 0 ? wave.enemyPrefabs : defaultEnemyPrefabs;
        int enemyCount = wave.enemyCount;

        if (prefabsToUse == null || prefabsToUse.Length == 0 || enemyCount == 0)
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

        List<int> auraIndices = new List<int>();
        if (isAuraActiveInThisRoom && activeAura != DevilAuraType.None)
        {
            int numAuraEnemies = Mathf.CeilToInt(enemyCount * auraCoveragePercent);

            List<int> allIndices = Enumerable.Range(0, enemyCount).ToList();

            auraIndices = allIndices.OrderBy(x => Random.value).Take(numAuraEnemies).ToList();
        }

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

                EnemyHealth healthComponent = null;
                bool hasHealthComponent = newEnemy.TryGetComponent<EnemyHealth>(out healthComponent);

                if (auraIndices.Contains(i))
                {
                    EnemyAuraManager auraManager = newEnemy.AddComponent<EnemyAuraManager>();
                    auraManager.ApplyAura(activeAura, activeResurrectionLevel);

                    if (hasHealthComponent)
                    {
                        healthComponent.SetInitialHealthMultiplier(initialHealthMultiplier);
                    }
                }

                if (hasHealthComponent)
                {
                    healthComponent.OnDeath += (enemyGO) => OnEnemyDeath(enemyGO);
                }
            }
        }
    }

    private void OnEnemyDeath(GameObject enemyGO)
    {
        activeEnemies.Remove(enemyGO);
    }

    private IEnumerator WaitForAllEnemiesToDie()
    {
        while (activeEnemies.Count > 0)
        {
            yield return null;
        }
    }

    private void OnDrawGizmos()
    {
        if (parentRoom != null && parentRoom.spawnAreas != null)
        {
            Gizmos.color = Color.red;
            foreach (var area in parentRoom.spawnAreas)
            {
                if (area != null)
                {
                    Gizmos.DrawWireCube(area.bounds.center, area.bounds.size);
                }
            }
        }
    }
}