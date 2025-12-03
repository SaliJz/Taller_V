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

    public int ExtraWavesCount { get; private set; } = 0;

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

        if (aura == DevilAuraType.PartialResurrection)
        {
            initialHealthMultiplier = 0.8f; 
        }
        else
        {
            initialHealthMultiplier = 1.0f; 
        }

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
            ReportDebug("No hay configuración de combate o no hay oleadas definidas. Saltando el combate.", 2);
            if (dungeonGenerator != null)
            {
                dungeonGenerator.OnCombatRoomCleared(parentRoom, entrancePoint);
            }
            yield break;
        }

        ReportDebug($"Iniciando encuentro de combate con {combatConfig.waves.Count} oleadas. El tiempo entre olas es {combatConfig.timeBetweenWaves}s", 1);

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

            ReportDebug($"--- Spawn de la Oleada {i + 1} de {combatConfig.waves.Count} ---", 1);

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

        if (dungeonGenerator != null)
        {
            dungeonGenerator.OnCombatRoomCleared(parentRoom, entrancePoint);
        }
    }

    public void AddExtraWaves(int count)
    {
        if (parentRoom.currentRoomType != RoomType.Combat && parentRoom.currentRoomType != RoomType.Boss)
        {
            ReportDebug("Intentando añadir oleadas extra en una sala que no es de combate.", 2);
            return;
        }

        if (combatConfig != null)
        {
            ExtraWavesCount += count;

            if (combatConfig.waves.Count > 0)
            {
                EnemyWave baseWave = combatConfig.waves.Last();

                for (int i = 0; i < count; i++)
                {
                    EnemyWave extraWave = new EnemyWave
                    {
                        enemyPrefabs = baseWave.enemyPrefabs,
                        enemyCount = baseWave.enemyCount
                    };

                    combatConfig.waves.Insert(combatConfig.waves.Count - 1, extraWave);
                }

                ReportDebug($"Añadidas {count} oleadas extra. El total de oleadas es ahora: {combatConfig.waves.Count}", 1);
            }
        }
        else
        {
            ReportDebug("combatConfig es nulo. No se pudieron añadir oleadas extra.", 3);
        }
    }

    public void ClearExtraWaves()
    {
        ExtraWavesCount = 0;
    }

    private IEnumerator SpawnEnemiesInWave(EnemyWave wave)
    {
        GameObject[] prefabsToUse = wave.enemyPrefabs.Length > 0 ? wave.enemyPrefabs : defaultEnemyPrefabs;
        int enemyCount = wave.enemyCount;

        if (wave.enemyPrefabs.Length > 0)
        {
            enemyCount = wave.enemyPrefabs.Length;
            ReportDebug($"Wave predefinida detectada: spawneando exactamente {enemyCount} enemigos.", 1);
        }

        if (prefabsToUse == null || prefabsToUse.Length == 0 || enemyCount == 0)
        {
            ReportDebug("No hay prefabs válidos o enemyCount es 0. Saltando wave.", 2);
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
            ReportDebug($"Intentando aplicar {activeAura} a {numAuraEnemies} de {enemyCount} enemigos (Cobertura: {auraCoveragePercent}).", 1);
            List<int> allIndices = Enumerable.Range(0, enemyCount).ToList();
            auraIndices = allIndices.OrderBy(x => Random.value).Take(numAuraEnemies).ToList();
        }

        for (int i = 0; i < enemyCount; i++)
        {
            if (i < instantiatedEffects.Count && instantiatedEffects[i] != null)
            {
                Destroy(instantiatedEffects[i]);
            }

            GameObject enemyPrefab = prefabsToUse[i]; 
            GameObject newEnemy = Instantiate(enemyPrefab, spawnPositions[i], Quaternion.identity);

            ReportDebug($"Spawneado enemigo {i + 1}/{enemyCount}: {enemyPrefab.name}", 1);

            if (newEnemy != null)
            {
                if (!newEnemy.activeSelf)
                {
                    newEnemy.SetActive(true);
                }

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
                    if (!healthComponent.enabled)
                    {
                        ReportDebug($"El enemigo '{newEnemy.name}' tiene EnemyHealth, ¡pero está deshabilitado! Habilitándolo.", 2);
                        healthComponent.enabled = true;
                    }

                    activeEnemies.Add(newEnemy);
                    healthComponent.OnDeath += (enemyGO) => OnEnemyDeath(enemyGO);
                }
                else
                {
                    ReportDebug($"El enemigo '{newEnemy.name}' del prefab '{enemyPrefab.name}' no tiene componente 'EnemyHealth'. NO será contado para la limpieza de sala.", 3);
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

    public void ResetAuraStatus()
    {
        isAuraActiveInThisRoom = false;
        activeAura = DevilAuraType.None;
        activeResurrectionLevel = ResurrectionLevel.None;
        auraCoveragePercent = 0f;
        initialHealthMultiplier = 1.0f;
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

    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[EnemyManager] {message}");
                break;
            case 2:
                Debug.LogWarning($"[EnemyManager] {message}");
                break;
            case 3:
                Debug.LogError($"[EnemyManager] {message}");
                break;
            default:
                Debug.Log($"[EnemyManager] {message}");
                break;
        }
    }
}