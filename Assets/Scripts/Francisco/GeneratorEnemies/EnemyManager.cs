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
    public event System.Action onWavesStart;

    public int ExtraWavesCount { get; private set; } = 0;

    private bool _isLastWave = false;

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

            onWavesStart?.Invoke();

            ReportDebug($"--- Spawn de la Oleada {i + 1} de {combatConfig.waves.Count} ---", 1);

            yield return StartCoroutine(SpawnEnemiesInWave(wave));

            bool isLastWave = (i == combatConfig.waves.Count - 1);
            _isLastWave = isLastWave;

            yield return StartCoroutine(WaitForAllEnemiesToDie());
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
                        enemyCount = baseWave.enemyCount,
                        spawnModes = baseWave.spawnModes,
                        spawnPointCodes = baseWave.spawnPointCodes
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

        var specificEntries = new List<(GameObject prefab, string code)>();
        var generalPrefabs = new List<GameObject>();

        for (int i = 0; i < enemyCount; i++)
        {
            GameObject prefab = prefabsToUse[i];
            EnemySpawnMode mode = (wave.spawnModes != null && i < wave.spawnModes.Length)
                ? wave.spawnModes[i]
                : EnemySpawnMode.General;
            string code = (wave.spawnPointCodes != null && i < wave.spawnPointCodes.Length)
                ? wave.spawnPointCodes[i]
                : "";

            if (mode == EnemySpawnMode.Specific && !string.IsNullOrEmpty(code))
                specificEntries.Add((prefab, code));
            else
                generalPrefabs.Add(prefab);
        }

        var usedCodes = new HashSet<string>();
        var specificSpawnList = new List<(GameObject prefab, Vector3 position)>();

        foreach (var (prefab, code) in specificEntries)
        {
            if (usedCodes.Contains(code))
            {
                ReportDebug($"SpawnPoint '{code}' ya ocupado por otro enemigo en esta wave. Se descarta uno de los específicos.", 2);
                continue;
            }

            Transform point = parentRoom != null ? parentRoom.GetSpecificSpawnPoint(code) : null;
            if (point == null)
            {
                ReportDebug($"No se encontró SpecificSpawnPoint con código '{code}' en la sala. Pasando a spawn general.", 2);
                generalPrefabs.Add(prefab);
                continue;
            }

            specificSpawnList.Add((prefab, point.position));
            usedCodes.Add(code);
        }

        var generalSpawnPositions = new List<Vector3>();
        foreach (var _ in generalPrefabs)
        {
            BoxCollider spawnArea = parentRoom.spawnAreas[Random.Range(0, parentRoom.spawnAreas.Length)];
            generalSpawnPositions.Add(new Vector3(
                Random.Range(spawnArea.bounds.min.x, spawnArea.bounds.max.x),
                spawnArea.transform.position.y,
                Random.Range(spawnArea.bounds.min.z, spawnArea.bounds.max.z)
            ));
        }

        List<(GameObject effect, Vector3 pos)> allEffects = new List<(GameObject, Vector3)>();

        foreach (var (prefab, pos) in specificSpawnList)
        {
            GameObject effect = spawnEffectPrefab != null ? Instantiate(spawnEffectPrefab, pos, Quaternion.identity) : null;
            allEffects.Add((effect, pos));
        }

        for (int i = 0; i < generalPrefabs.Count; i++)
        {
            Vector3 pos = generalSpawnPositions[i];
            GameObject effect = spawnEffectPrefab != null ? Instantiate(spawnEffectPrefab, pos, Quaternion.identity) : null;
            allEffects.Add((effect, pos));
        }

        yield return new WaitForSeconds(2.0f);

        var finalSpawnList = new List<(GameObject prefab, Vector3 position)>();
        finalSpawnList.AddRange(specificSpawnList);
        for (int i = 0; i < generalPrefabs.Count; i++)
            finalSpawnList.Add((generalPrefabs[i], generalSpawnPositions[i]));

        int totalFinal = finalSpawnList.Count;

        List<int> auraIndices = new List<int>();
        if (isAuraActiveInThisRoom && activeAura != DevilAuraType.None)
        {
            int numAuraEnemies = Mathf.CeilToInt(totalFinal * auraCoveragePercent);
            ReportDebug($"Intentando aplicar {activeAura} a {numAuraEnemies} de {totalFinal} enemigos (Cobertura: {auraCoveragePercent}).", 1);
            auraIndices = Enumerable.Range(0, totalFinal).OrderBy(x => Random.value).Take(numAuraEnemies).ToList();
        }

        for (int i = 0; i < totalFinal; i++)
        {
            if (i < allEffects.Count && allEffects[i].effect != null)
            {
                Destroy(allEffects[i].effect);
            }

            var (enemyPrefab, spawnPos) = finalSpawnList[i];
            GameObject newEnemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);

            ReportDebug($"Spawneado enemigo {i + 1}/{totalFinal}: {enemyPrefab.name}", 1);

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

        if (activeEnemies.Count == 0 && _isLastWave)
        {
            SlowMotion.Instance?.TriggerSlowMotion();
        }
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