using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static RoomProgressionLevel;

[System.Serializable]
public class EnemyWave
{
    public GameObject[] enemyPrefabs;
    public int enemyCount = 3;
}

[System.Serializable]
public class CombatContents
{
    public List<EnemyWave> waves = new List<EnemyWave>();
    public float timeBetweenWaves = 5f;
}

[System.Serializable]
public class RoomProgressionRule
{
    public RoomType roomType;

    [Header("Progression Range")]
    public int minRoomNumber = 1;
    public int maxRoomNumber = 10;

    [Header("Mandatory/Probability")]
    public bool isMandatory;
    public bool isProbableMandatory;
    public bool generateOnce;
    [Range(0f, 100f)]
    public float probability = 0;

    [Header("--- Content Rule ---")]
    public CombatContents combatContent;
}

[System.Serializable]
public class RoomGenerationRule
{
    public RoomType currentRoomType;
    public RoomType[] allowedNextRoomTypes;
}

[System.Serializable]
public class RoomTypeProbability
{
    public RoomType roomType;
    [Range(0f, 100f)]
    public float probability = 0;
    public Room[] roomPrefabs;
    [HideInInspector]
    public List<RoomData> roomDataList = new List<RoomData>();
}

[System.Serializable]
public class RoomData
{
    public Room prefab;
    [HideInInspector]
    public int repetitionCount = 0;
    [HideInInspector]
    public float weight = 1.0f;
}

public class RoomSelectionResult
{
    public RoomData RoomData { get; set; }
    public RoomProgressionRule ProgressionRule { get; set; }
}

public class DungeonGenerator : MonoBehaviour
{
    [Header("Room Prefabs")]
    public Room startRoomPrefab;
    public Room[] endRoomPrefabs;

    [Header("Generation Rules")]
    public List<RoomGenerationRule> roomGenerationRules;
    public List<RoomTypeProbability> roomTypeProbabilities;

    [Header("Room Generation Rules")]
    public RoomGenerationRule[] generationRules;

    [Header("Room Progression Rules")]
    public RoomProgressionRule[] progressionRules;

    [Header("Enemy Prefabs")]
    public GameObject[] enemyPrefabs;

    [Header("Visual Effects")]
    public GameObject spawnEffectPrefab;

    [Header("Generation Settings")]
    public int minRooms = 8;
    public int maxRooms = 12;
    public int maxRoomAttempts = 15;
    [Range(0.1f, 2.0f)]
    public float repetitionPenalty = 0.8f;
    [Range(0.1f, 1.0f)]
    public float weightDecay = 0.7f;
    [Range(5f, 100f)]
    public float roomDistance = 20f;

    [Header("Player Movement Settings")]
    [Range(0.1f, 2.0f)]
    public float playerMoveDuration = 0.5f;
    [Range(1f, 10f)]
    public float playerMoveDistance = 3f;

    [Header("Door Settings")]
    [Range(0.1f, 5.0f)]
    public float doorActivateDelay = 0.5f;

    [Header("Enemy Progressive System")]
    public ProgressiveEnemySystemConfig defaultEnemyConfig;

    [Header("Dependencies")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerStatsManager statsManager;
    private PlayerMovement playerMovement;

    [Header("Debug")]
    [SerializeField] private KeyCode _debugCompleteRoomKey = KeyCode.O;

    private List<Room> generatedRooms = new List<Room>();
    private int roomsGenerated = 0;
    private int targetRoomCount;

    private Dictionary<RoomType, List<RoomData>> roomDataDictionary = new Dictionary<RoomType, List<RoomData>>();
    private Dictionary<RoomType, RoomType[]> generationRuleDictionary = new Dictionary<RoomType, RoomType[]>();
    private Dictionary<RoomType, RoomProgressionRule> progressionRuleDictionary = new Dictionary<RoomType, RoomProgressionRule>();
    private List<RoomProgressionRule> usedOnceRules = new List<RoomProgressionRule>();

    public static event Action<RoomType> OnRoomEntered;
    public static event Action<RoomType, float> OnRoomCompleted;

    private RoomProgressionRule mandatoryRoomToPlace;
    private int mandatoryRoomNumber = -1;
    private RoomProgressionRule probableMandatoryToPlace;
    private bool hasProbableMandatoryBeenGenerated = false;
    private float currentProbableMandatoryProbability;

    private float roomStartTime = 0f; 
    public int CurrentRoomCount { get; private set; } = 0;
    public int currentRoomNumber { get; private set; } = 0;
    public Room CurrentRoom { get; private set; }
    public static DungeonGenerator Instance { get; private set; }

    private DevilManipulationManager devilManager;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }

        if (DevilManipulationManager.Instance != null)
        {
            devilManager = DevilManipulationManager.Instance;
        }
        else
        {
            Debug.LogError("[DungeonGenerator] DevilManipulationManager.Instance no encontrado. La lógica del Diablo no funcionará.");
        }
    }

    void Start()
    {
        foreach (var roomProb in roomTypeProbabilities)
        {
            roomDataDictionary[roomProb.roomType] = new List<RoomData>();
            foreach (var prefab in roomProb.roomPrefabs)
            {
                roomDataDictionary[roomProb.roomType].Add(new RoomData { prefab = prefab, weight = 1.0f });
            }
        }

        foreach (var rule in generationRules)
        {
            generationRuleDictionary[rule.currentRoomType] = rule.allowedNextRoomTypes;
        }

        foreach (var rule in progressionRules)
        {
            progressionRuleDictionary[rule.roomType] = rule;
        }

        targetRoomCount = UnityEngine.Random.Range(minRooms, maxRooms + 1);

        if (probableMandatoryToPlace != null)
        {
            currentProbableMandatoryProbability = probableMandatoryToPlace.probability;
        }

        PlanMandatoryRoom();
        GenerateInitialRoom();
        playerMovement = FindAnyObjectByType<PlayerMovement>();
        playerHealth = FindAnyObjectByType<PlayerHealth>();
        statsManager = FindAnyObjectByType<PlayerStatsManager>();
    }

    void Update()
    {
        if (Input.GetKeyDown(_debugCompleteRoomKey))
        {
            CompleteCurrentRoomShortcut();
        }
    }

    public void StartRoomTimer()
    {
        roomStartTime = Time.time;
    }

    public float EndRoomTimer()
    {
        float timeElapsed = Time.time - roomStartTime;

        roomStartTime = 0f;

        return timeElapsed;
    }

    public void CompleteCurrentRoomShortcut()
    {
        Room currentRoom = generatedRooms.LastOrDefault();
        int destroyedCount = 0;

        if (currentRoom == null || currentRoom.isEndRoom || currentRoom.isStartRoom)
        {
            return;
        }

        if (currentRoom.roomType != RoomType.Combat)
        {
            return;
        }

        EnemyManager enemyManager = currentRoom.GetComponent<EnemyManager>();

        if (enemyManager == null)
        {
            return;
        }

        float roomDetectionRadius = 30f;
        Vector3 roomPosition = currentRoom.transform.position;

        int enemyLayer = LayerMask.NameToLayer("Enemy");

        if (enemyLayer != -1)
        {
            Collider[] colliders = Physics.OverlapSphere(roomPosition, roomDetectionRadius);

            foreach (Collider col in colliders)
            {
                if (col != null && col.gameObject.layer == enemyLayer)
                {
                    Destroy(col.gameObject);
                    destroyedCount++;
                }
            }
        }
        else
        {
        }

        if (spawnEffectPrefab != null)
        {
            string effectName = spawnEffectPrefab.name;
            GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);

            foreach (GameObject obj in allObjects)
            {
                string cleanName = obj.name.Replace("(Clone)", "");

                if (cleanName == effectName && Vector3.Distance(obj.transform.position, roomPosition) < roomDetectionRadius)
                {
                    Destroy(obj);
                    destroyedCount++;
                }
            }
        }

        Destroy(enemyManager);

        ConnectionPoint entrancePoint = currentRoom.connectionPoints.FirstOrDefault(conn => conn.isConnected);

        if (entrancePoint == null)
        {
            return;
        }

        OnCombatRoomCleared(currentRoom, entrancePoint);
    }

    void PlanMandatoryRoom()
    {
        var mandatoryRules = progressionRules.Where(r => r.isMandatory).ToList();
        if (mandatoryRules.Any())
        {
            mandatoryRoomToPlace = mandatoryRules.First();
            mandatoryRoomNumber = UnityEngine.Random.Range(mandatoryRoomToPlace.minRoomNumber, mandatoryRoomToPlace.maxRoomNumber + 1);
        }

        var probableMandatoryRules = progressionRules.Where(r => r.isProbableMandatory).ToList();
        if (probableMandatoryRules.Any())
        {
            probableMandatoryToPlace = probableMandatoryRules.First();
        }
    }


    void GenerateInitialRoom()
    {
        if (startRoomPrefab == null) return;

        Room initialRoom = Instantiate(startRoomPrefab, Vector3.zero, Quaternion.identity);
        initialRoom.name = "StartRoom";
        initialRoom.isStartRoom = true;

        generatedRooms.Add(initialRoom);
        initialRoom.LockAllDoors();

        foreach (ConnectionPoint connectionPoint in initialRoom.connectionPoints)
        {
            if (connectionPoint.GetComponent<BoxCollider>() == null)
            {
                BoxCollider collider = connectionPoint.gameObject.AddComponent<BoxCollider>();
                collider.isTrigger = true;
                collider.size = Vector3.one * 1f;
                connectionPoint.gameObject.AddComponent<ConnectionTrigger>();
            }
        }
    }

    Vector3 GetDirectionFromConnectionType(ConnectionType connectionType)
    {
        switch (connectionType)
        {
            case ConnectionType.North: return Vector3.forward;
            case ConnectionType.South: return Vector3.back;
            case ConnectionType.East: return Vector3.left;
            case ConnectionType.West: return Vector3.right;
            default: return Vector3.forward;
        }
    }

    float GetEscalatingProbability(int currentRoomNumber)
    {
        if (probableMandatoryToPlace == null) return 0f;

        float roomRange = probableMandatoryToPlace.maxRoomNumber - probableMandatoryToPlace.minRoomNumber;
        if (roomRange <= 0) return probableMandatoryToPlace.probability;

        float roomProgress = (currentRoomNumber - probableMandatoryToPlace.minRoomNumber) / roomRange;
        float scalingFactor = 1f + (roomProgress * 2f);

        return Mathf.Min(100f, probableMandatoryToPlace.probability * scalingFactor);
    }

    public void GenerateNextRoom(ConnectionPoint entrancePoint)
    {
        if (entrancePoint.isConnected)
        {
            return;
        }

        if (roomsGenerated >= targetRoomCount)
        {
            PlaceEndRoom(entrancePoint);
            return;
        }

        int attempts = 0;
        bool roomPlaced = false;

        RoomType previousRoomType = entrancePoint.GetComponentInParent<Room>().roomType;

        if (mandatoryRoomToPlace != null && roomsGenerated + 1 == mandatoryRoomNumber)
        {
            List<RoomData> mandatoryRoomPrefabs = roomDataDictionary[mandatoryRoomToPlace.roomType];

            mandatoryRoomPrefabs = mandatoryRoomPrefabs.OrderBy(x => UnityEngine.Random.value).ToList();

            foreach (var roomData in mandatoryRoomPrefabs)
            {
                if (attempts >= maxRoomAttempts) break;

                if (PlaceRoom(roomData.prefab, roomData, entrancePoint, mandatoryRoomToPlace))
                {
                    roomPlaced = true;
                    UpdateRoomWeights(roomData);
                    break;
                }
                attempts++;
            }

            if (roomPlaced)
            {
                return;
            }
        }

        while (attempts < maxRoomAttempts && !roomPlaced)
        {
            if (entrancePoint.isConnected)
            {
                return;
            }

            RoomSelectionResult selectionResult = GetRandomRoomData(previousRoomType, roomsGenerated + 1);

            if (selectionResult == null || selectionResult.RoomData == null)
            {
                attempts++;
                continue;
            }

            roomPlaced = PlaceRoom(selectionResult.RoomData.prefab, selectionResult.RoomData, entrancePoint, selectionResult.ProgressionRule);

            if (roomPlaced)
            {
                UpdateRoomWeights(selectionResult.RoomData);
            }
            attempts++;
        }
    }

    bool PlaceRoom(Room newRoomPrefab, RoomData roomData, ConnectionPoint entrancePoint, RoomProgressionRule progressionRule)
    {
        Room newRoom = Instantiate(newRoomPrefab);

        List<ConnectionPoint> exitPoints = GetAllMatchingConnectionPoints(newRoom, entrancePoint);

        if (exitPoints.Count > 0)
        {
            ConnectionPoint exitPoint = exitPoints[UnityEngine.Random.Range(0, exitPoints.Count)];

            Vector3 offset = exitPoint.transform.position - newRoom.transform.position;

            Vector3 connectionDirection = GetDirectionFromConnectionType(entrancePoint.connectionType);
            Vector3 finalPosition = entrancePoint.transform.position - offset + (connectionDirection * roomDistance);

            if (IsPositionValid(finalPosition, newRoom))
            {
                newRoom.transform.position = finalPosition;
                newRoom.name = $"Room_{roomsGenerated + 1}_{newRoomPrefab.name}";

                exitPoint.isConnected = true;
                exitPoint.connectedTo = entrancePoint.transform;
                entrancePoint.isConnected = true;
                entrancePoint.connectedTo = exitPoint.transform;

                generatedRooms.Add(newRoom);
                roomsGenerated++;

                if (newRoom.roomType == RoomType.Combat)
                {
                    CombatContents contents = GetCombatContentsForRoom(newRoom.roomType, progressionRule);

                    if (contents.waves.Any() && newRoom.GetComponent<EnemyManager>() == null)
                    {
                        var enemyManager = newRoom.gameObject.AddComponent<EnemyManager>();

                        enemyManager.Initialize(
                            this,
                            newRoom,
                            contents,
                            this.spawnEffectPrefab,
                            this.enemyPrefabs
                        );
                    }
                }

                foreach (ConnectionPoint connectionPoint in newRoom.connectionPoints)
                {
                    if (!connectionPoint.isConnected && connectionPoint.GetComponent<BoxCollider>() == null)
                    {
                        BoxCollider collider = connectionPoint.gameObject.AddComponent<BoxCollider>();
                        collider.isTrigger = true;
                        collider.size = Vector3.one * 1f;
                        connectionPoint.gameObject.AddComponent<ConnectionTrigger>();
                    }
                }

                if (progressionRule != null && progressionRule.generateOnce)
                {
                    usedOnceRules.Add(progressionRule);
                }

                return true;
            }
            else
            {
                Destroy(newRoom.gameObject);
                return false;
            }
        }
        else
        {
            Destroy(newRoom.gameObject);
            return false;
        }
    }

    bool IsPositionValid(Vector3 position, Room newRoom)
    {
        float minDistance = 5f;

        foreach (Room existingRoom in generatedRooms)
        {
            if (existingRoom != null && Vector3.Distance(position, existingRoom.transform.position) < minDistance)
            {
                return false;
            }
        }

        return true;
    }

    void PlaceEndRoom(ConnectionPoint entrancePoint)
    {
        if (endRoomPrefabs.Length == 0) return;

        int attempts = 0;
        bool roomPlaced = false;

        while (attempts < maxRoomAttempts && !roomPlaced)
        {
            Room endRoomPrefab = endRoomPrefabs[UnityEngine.Random.Range(0, endRoomPrefabs.Length)];
            Room endRoom = Instantiate(endRoomPrefab);

            List<ConnectionPoint> exitPoints = GetAllMatchingConnectionPoints(endRoom, entrancePoint);
            if (exitPoints.Count > 0)
            {
                ConnectionPoint exitPoint = exitPoints[UnityEngine.Random.Range(0, exitPoints.Count)];

                Vector3 offset = exitPoint.transform.position - endRoom.transform.position;

                Vector3 connectionDirection = GetDirectionFromConnectionType(entrancePoint.connectionType);
                Vector3 finalPosition = entrancePoint.transform.position - offset + (connectionDirection * roomDistance);

                endRoom.transform.position = finalPosition;
                endRoom.name = "EndRoom";

                exitPoint.isConnected = true;
                exitPoint.connectedTo = entrancePoint.transform;
                entrancePoint.isConnected = true;
                entrancePoint.connectedTo = exitPoint.transform;

                endRoom.isEndRoom = true;
                generatedRooms.Add(endRoom);
                roomPlaced = true;
            }
            else
            {
                Destroy(endRoom.gameObject);
            }
            attempts++;
        }
    }

    RoomSelectionResult GetRandomRoomData(RoomType previousRoomType, int currentRoomNumber)
    {
        RoomProgressionRule[] currentRules = progressionRules ?? System.Array.Empty<RoomProgressionRule>();

        if (mandatoryRoomToPlace != null && roomsGenerated + 1 == mandatoryRoomNumber)
        {
            if (generationRuleDictionary.ContainsKey(previousRoomType) && !generationRuleDictionary[previousRoomType].Contains(mandatoryRoomToPlace.roomType))
            {
                return null;
            }
            var mandatoryRoomPrefabs = roomDataDictionary[mandatoryRoomToPlace.roomType];
            if (mandatoryRoomPrefabs != null && mandatoryRoomPrefabs.Count > 0)
            {
                return new RoomSelectionResult
                {
                    RoomData = mandatoryRoomPrefabs[UnityEngine.Random.Range(0, mandatoryRoomPrefabs.Count)],
                    ProgressionRule = mandatoryRoomToPlace
                };
            }
        }

        var probableMandatoryRules = currentRules
            .Where(r => r.isProbableMandatory &&
                        currentRoomNumber >= r.minRoomNumber &&
                        currentRoomNumber <= r.maxRoomNumber &&
                        (!r.generateOnce || !hasProbableMandatoryBeenGenerated))
            .ToList();

        foreach (var rule in probableMandatoryRules)
        {
            float escalatingProbability = GetEscalatingProbability(currentRoomNumber);
            if (UnityEngine.Random.Range(0f, 100f) < escalatingProbability)
            {
                if (generationRuleDictionary.ContainsKey(previousRoomType) && !generationRuleDictionary[previousRoomType].Contains(rule.roomType))
                    continue;

                var probableRoomPrefabs = roomDataDictionary[rule.roomType];
                if (probableRoomPrefabs != null && probableRoomPrefabs.Count > 0)
                {
                    if (rule.generateOnce)
                    {
                        hasProbableMandatoryBeenGenerated = true;
                    }
                    return new RoomSelectionResult
                    {
                        RoomData = probableRoomPrefabs[UnityEngine.Random.Range(0, probableRoomPrefabs.Count)],
                        ProgressionRule = rule
                    };
                }
            }
        }

        var progressionAllowedTypes = currentRules
            .Where(rule =>
                currentRoomNumber >= rule.minRoomNumber &&
                currentRoomNumber <= rule.maxRoomNumber
            ).Select(p => p.roomType).ToList();

        RoomType[] generationAllowedTypes;
        if (!generationRuleDictionary.TryGetValue(previousRoomType, out generationAllowedTypes))
        {
            generationAllowedTypes = roomDataDictionary.Keys.ToArray();
        }

        List<RoomType> validRoomTypes = new List<RoomType>();

        if (progressionAllowedTypes.Any())
        {
            validRoomTypes = progressionAllowedTypes.Intersect(generationAllowedTypes).ToList();
        }
        else
        {
            validRoomTypes = generationAllowedTypes.ToList();
        }

        if (!validRoomTypes.Any())
        {
            Debug.LogError($"Error: No hay tipos de sala válidos para la sala {currentRoomNumber}. Revise reglas de conexión y que 'roomTypeProbabilities' no esté vacío.");
            return null;
        }

        var filteredProbabilities = roomTypeProbabilities.Where(p => validRoomTypes.Contains(p.roomType)).ToList();

        float totalProbability = filteredProbabilities.Sum(p => p.probability);

        if (totalProbability <= 0)
        {
            Debug.LogWarning($"Las probabilidades de RoomType (suma = {totalProbability}) para los tipos válidos fallaron. Usando selección uniforme como fallback.");

            RoomType fallbackType = validRoomTypes[UnityEngine.Random.Range(0, validRoomTypes.Count)];

            if (!roomDataDictionary.ContainsKey(fallbackType) || roomDataDictionary[fallbackType].Count == 0)
            {
                Debug.LogError($"Error: El RoomType '{fallbackType}' seleccionado por fallback NO tiene prefabs en roomDataDictionary.");
                return null;
            }

            var fallbackRoomDataList = roomDataDictionary[fallbackType];
            RoomData fallbackRoomData = fallbackRoomDataList[UnityEngine.Random.Range(0, fallbackRoomDataList.Count)];

            return new RoomSelectionResult
            {
                RoomData = fallbackRoomData,
                ProgressionRule = null 
            };
        }

        float randomValue = UnityEngine.Random.Range(0f, totalProbability);
        float currentSum = 0f;
        RoomType selectedType = RoomType.Normal;

        foreach (var roomProb in filteredProbabilities)
        {
            currentSum += roomProb.probability;
            if (randomValue <= currentSum)
            {
                selectedType = roomProb.roomType;
                break;
            }
        }

        if (!roomDataDictionary.ContainsKey(selectedType) || roomDataDictionary[selectedType] == null || roomDataDictionary[selectedType].Count == 0)
        {
            Debug.LogError($"Error de configuración: El RoomType '{selectedType}' está seleccionado, pero no tiene prefabs asociados en 'roomDataDictionary'.");
            return null;
        }

        var roomDataList = roomDataDictionary[selectedType];
        float totalWeight = roomDataList.Sum(data => data.weight);

        if (totalWeight <= 0)
        {
            ResetRoomWeights(selectedType);
            totalWeight = roomDataList.Sum(data => data.weight);
            if (totalWeight <= 0) return null;
        }

        float randomWeightValue = UnityEngine.Random.Range(0f, totalWeight);
        float currentWeightSum = 0f;
        RoomData finalRoomData = null;

        foreach (var data in roomDataList)
        {
            currentWeightSum += data.weight;
            if (randomWeightValue <= currentWeightSum)
            {
                finalRoomData = data;
                break;
            }
        }

        if (finalRoomData == null) return null;

        RoomProgressionRule finalRule = currentRules.FirstOrDefault(r =>
            r.roomType == finalRoomData.prefab.roomType &&
            currentRoomNumber >= r.minRoomNumber &&
            currentRoomNumber <= r.maxRoomNumber);

        return new RoomSelectionResult
        {
            RoomData = finalRoomData,
            ProgressionRule = finalRule
        };
    }

    private CombatContents GenerateCombatContentFromProgression(RoomProgressionLevel progressiveLevel)
    {
        switch (progressiveLevel.GenerationMode)
        {
            case EnemyGenerationMode.ProceduralFromPool:
            default:
                {
                    Debug.Log($"Generando contenido SOLO PROCEDURAL (Pool) para la Sala {roomsGenerated}.");
                    int wavesToGenerate = UnityEngine.Random.Range(progressiveLevel.minWaves, progressiveLevel.maxWaves + 1);

                    CombatContents proceduralContents = new CombatContents
                    {
                        timeBetweenWaves = progressiveLevel.timeBetweenWaves,
                        waves = new List<EnemyWave>()
                    };

                    for (int i = 0; i < wavesToGenerate; i++)
                    {
                        proceduralContents.waves.Add(GenerateSingleProceduralWave(progressiveLevel));
                    }
                    return proceduralContents;
                }

            case EnemyGenerationMode.PredefinedCombination:
                return GetFullPredefinedContents(progressiveLevel);

            case EnemyGenerationMode.CombinationAndProcedural:
                {
                    if (progressiveLevel.predefinedCombinations == null || progressiveLevel.predefinedCombinations.Count == 0)
                    {
                        Debug.LogWarning($"El modo es CombinationAndProcedural, pero no hay combinaciones predefinidas. Recurriendo a ProceduralFromPool.");
                        goto case EnemyGenerationMode.ProceduralFromPool;
                    }

                    int comboIndex = UnityEngine.Random.Range(0, progressiveLevel.predefinedCombinations.Count);
                    PredefinedCombatCombination selectedCombo = progressiveLevel.predefinedCombinations[comboIndex];

                    if (selectedCombo.waves == null || selectedCombo.waves.Count == 0)
                    {
                        Debug.LogWarning("La combinación seleccionada no tiene oleadas. Recurriendo a Procedural puro.");
                        goto case EnemyGenerationMode.ProceduralFromPool;
                    }

                    int totalWaves = UnityEngine.Random.Range(progressiveLevel.minWaves, progressiveLevel.maxWaves + 1);
                    CombatContents mixedContents = new CombatContents
                    {
                        timeBetweenWaves = progressiveLevel.timeBetweenWaves,
                        waves = new List<EnemyWave>()
                    };

                    for (int i = 0; i < totalWaves; i++)
                    {
                        if (UnityEngine.Random.value < 0.5f)
                        {
                            PredefinedWave predefinedWave = selectedCombo.waves[UnityEngine.Random.Range(0, selectedCombo.waves.Count)];
                            mixedContents.waves.Add(ConvertPredefinedWaveToEnemyWave(predefinedWave));
                            Debug.Log($"Oleada {i + 1}: Predefinida (de '{selectedCombo.CombinationName}').");
                        }
                        else
                        {
                            mixedContents.waves.Add(GenerateSingleProceduralWave(progressiveLevel));
                            Debug.Log($"Oleada {i + 1}: Procedural (del Pool).");
                        }
                    }

                    return mixedContents;
                }
        }
    }

    public RoomProgressionRule SelectProgressionRule(RoomType roomType)
    {
        int roomNum = roomsGenerated + 1;

        var availableRules = progressionRules 
            .Where(r => r != null) 
            .Where(r => roomNum >= r.minRoomNumber && roomNum <= r.maxRoomNumber)
            .Where(r => r.roomType == roomType)
            .Where(r => !r.generateOnce || !usedOnceRules.Contains(r))
            .ToList();

        if (!availableRules.Any()) return null;

        RoomProgressionRule selectedRule = null;

        var mandatoryRules = availableRules.Where(r => r.isMandatory).ToList();
        if (mandatoryRules.Any())
        {
            selectedRule = mandatoryRules[UnityEngine.Random.Range(0, mandatoryRules.Count)];
        }
        else
        {
            float totalProbability = availableRules.Where(r => r.probability > 0).Sum(r => r.probability);
            if (totalProbability > 0)
            {
                float randomPoint = UnityEngine.Random.Range(0f, totalProbability);
                float currentSum = 0f;

                foreach (var rule in availableRules.Where(r => r.probability > 0))
                {
                    currentSum += rule.probability;
                    if (randomPoint < currentSum)
                    {
                        selectedRule = rule;
                        break;
                    }
                }
            }
        }

        if (selectedRule != null)
        {
            bool isCombatRoom = selectedRule.roomType == RoomType.Combat;

            if (isCombatRoom && (selectedRule.combatContent == null || !selectedRule.combatContent.waves.Any()))
            {
                return null;
            }

            if (selectedRule.generateOnce)
            {
                usedOnceRules.Add(selectedRule);
            }
        }

        return selectedRule;
    }

    private List<GameObject> SelectEnemiesByWeight(List<EnemyTierConfig> pool, int count)
    {
        List<GameObject> selectedEnemies = new List<GameObject>();
        if (pool == null || !pool.Any()) return selectedEnemies;

        float totalWeight = pool.Sum(e => e.SpawnWeight);

        for (int i = 0; i < count; i++)
        {
            float randomPoint = UnityEngine.Random.Range(0f, totalWeight);
            float currentWeight = 0f;

            foreach (var enemyTier in pool)
            {
                currentWeight += enemyTier.SpawnWeight;
                if (randomPoint < currentWeight)
                {
                    selectedEnemies.Add(enemyTier.EnemyPrefab);
                    break;
                }
            }
        }
        return selectedEnemies;
    }

    private EnemyWave GenerateSingleProceduralWave(RoomProgressionLevel level)
    {
        EnemyWave newWave = new EnemyWave();

        int roomsIntoLevel = currentRoomNumber - level.startRoomNumber;
        float progressionFactor = Mathf.Clamp01(roomsIntoLevel / 10f);

        int targetEnemyCount = Mathf.RoundToInt(
            Mathf.Lerp(level.minEnemyCountPerWave, level.maxEnemyCountPerWave, progressionFactor)
        );
        newWave.enemyCount = Mathf.Max(1, targetEnemyCount);

        if (level.availableEnemyPool != null && level.availableEnemyPool.Count > 0)
        {
            newWave.enemyPrefabs = SelectEnemiesByWeight(level.availableEnemyPool, newWave.enemyCount).ToArray();
        }
        else
        {
            Debug.LogWarning("El Pool de Enemigos para este nivel está vacío. No se generaron enemigos.");
            newWave.enemyPrefabs = System.Array.Empty<GameObject>();
        }

        return newWave;
    }

    private EnemyWave ConvertPredefinedWaveToEnemyWave(PredefinedWave predefinedWave)
    {
        EnemyWave newWave = new EnemyWave();
        List<GameObject> waveEnemies = new List<GameObject>();
        int totalCount = 0;

        if (predefinedWave.enemiesInWave != null)
        {
            foreach (var detail in predefinedWave.enemiesInWave)
            {
                if (detail.EnemyPrefab != null)
                {
                    for (int i = 0; i < detail.Count; i++)
                    {
                        waveEnemies.Add(detail.EnemyPrefab);
                    }
                    totalCount += detail.Count;
                }
            }
        }

        newWave.enemyPrefabs = waveEnemies.ToArray();
        newWave.enemyCount = totalCount;
        return newWave;
    }

    private CombatContents GetFullPredefinedContents(RoomProgressionLevel level)
    {
        if (level.predefinedCombinations == null || level.predefinedCombinations.Count == 0)
        {
            Debug.LogWarning("El modo es PredefinedCombination, pero la lista de combinaciones está vacía.");
            return new CombatContents();
        }

        int randomIndex = UnityEngine.Random.Range(0, level.predefinedCombinations.Count);
        PredefinedCombatCombination selectedCombo = level.predefinedCombinations[randomIndex];

        Debug.Log($"Usando contenido PREDEFINIDO '{selectedCombo.CombinationName}' para la Sala {roomsGenerated}.");

        CombatContents contents = new CombatContents
        {
            timeBetweenWaves = selectedCombo.timeBetweenWaves,
            waves = new List<EnemyWave>()
        };

        if (selectedCombo.waves != null)
        {
            foreach (var predefinedWave in selectedCombo.waves)
            {
                contents.waves.Add(ConvertPredefinedWaveToEnemyWave(predefinedWave));
            }
        }

        return contents;
    }

    public CombatContents GetCombatContentsForRoom(RoomType roomType, RoomProgressionRule usedProgressionRule)
    {
        if (roomType != RoomType.Combat)
        {
            return new CombatContents();
        }

        if (usedProgressionRule != null &&
            usedProgressionRule.combatContent != null &&
            usedProgressionRule.combatContent.waves.Any())
        {
            Debug.Log($"Usando contenido de combate DEFINIDO por la regla de progresión específica (Sala {roomsGenerated}).");
            return usedProgressionRule.combatContent;
        }

        int roomNum = roomsGenerated;

        if (defaultEnemyConfig != null)
        {
            RoomProgressionLevel progressiveLevel = defaultEnemyConfig.GetConfigForRoom(roomNum);

            if (progressiveLevel != null)
            {
                Debug.Log($"Generando contenido de combate PROCEDURAL (Nivel {progressiveLevel.startRoomNumber}).");
                return GenerateCombatContentFromProgression(progressiveLevel);
            }
        }

        Debug.LogWarning($"No se encontró contenido de combate para la Sala {roomNum}. Devolviendo contenido vacío.");
        return new CombatContents();
    }

    void UpdateRoomWeights(RoomData usedRoom)
    {
        usedRoom.repetitionCount++;
        usedRoom.weight = 1.0f / (1.0f + usedRoom.repetitionCount * repetitionPenalty);
        RoomType usedRoomType = usedRoom.prefab.roomType;

        if (roomDataDictionary.ContainsKey(usedRoomType))
        {
            foreach (var data in roomDataDictionary[usedRoomType])
            {
                if (data != usedRoom)
                {
                    data.weight = Mathf.Min(1.0f, data.weight * (1.0f + weightDecay * 0.1f));
                }
            }
        }
    }

    void ResetRoomWeights(RoomType type)
    {
        if (roomDataDictionary.ContainsKey(type))
        {
            foreach (var data in roomDataDictionary[type])
            {
                data.weight = 1.0f / (1.0f + data.repetitionCount * repetitionPenalty * 0.5f);
            }
        }
    }

    List<ConnectionPoint> GetAllMatchingConnectionPoints(Room room, ConnectionPoint targetPoint)
    {
        List<ConnectionPoint> matchingConnections = new List<ConnectionPoint>();
        ConnectionType requiredType = GetOppositeConnectionType(targetPoint.connectionType);

        foreach (ConnectionPoint conn in room.connectionPoints)
        {
            if (conn.connectionType == requiredType && !conn.isConnected)
            {
                matchingConnections.Add(conn);
            }
        }
        return matchingConnections;
    }

    ConnectionType GetOppositeConnectionType(ConnectionType type)
    {
        switch (type)
        {
            case ConnectionType.North: return ConnectionType.South;
            case ConnectionType.South: return ConnectionType.North;
            case ConnectionType.East: return ConnectionType.West;
            case ConnectionType.West: return ConnectionType.East;
            default: return ConnectionType.North;
        }
    }

    private string GetDistortionName(DevilDistortionType distortion)
    {
        return distortion switch
        {
            DevilDistortionType.AbyssalConfusion => "Confusión del Abismo",
            DevilDistortionType.FloorOfTheDamned => "Piso de Condenados",
            DevilDistortionType.DeceptiveDarkness => "Oscuridad Engañosa",
            DevilDistortionType.SealedLuck => "Suerte Sellada",
            DevilDistortionType.WitheredBloodthirst => "Sed de Sangre Marchita",
            DevilDistortionType.InfernalJudgement => "Juicio Infernal",
            _ => "Efecto Desconocido"
        };
    }

    public IEnumerator TransitionToNextRoom(ConnectionPoint entrancePoint, Transform playerTransform)
    {
        if (playerMovement == null)
        {
            playerMovement = FindAnyObjectByType<PlayerMovement>();
        }

        CurrentRoomCount++;

        float timeToCleanRoom = EndRoomTimer();

        float originalPlayerY = playerTransform.position.y;
        if (playerMovement != null)
        {
            playerMovement.SetCanMove(false);
        }

        Room oldRoom = entrancePoint.GetComponentInParent<Room>();

        int oldRoomDoorIndex = System.Array.IndexOf(oldRoom.connectionPoints, entrancePoint);
        if (oldRoomDoorIndex != -1 && oldRoom.connectionDoors.Length > oldRoomDoorIndex && oldRoom.connectionDoors[oldRoomDoorIndex] != null)
        {
            oldRoom.connectionDoors[oldRoomDoorIndex].SetActive(true);
        }

        GenerateNextRoom(entrancePoint);

        if (entrancePoint.connectedTo == null)
        {
            Debug.LogError($"Error Crítico: Falló la generación de la Sala {CurrentRoomCount}. Revise los logs de 'GetRandomRoomData' para errores de configuración (reglas, probabilidades o prefabs). Deteniendo transición.");

            if (oldRoom != null && oldRoomDoorIndex != -1 && oldRoom.connectionDoors.Length > oldRoomDoorIndex && oldRoom.connectionDoors[oldRoomDoorIndex] != null)
            {
                oldRoom.connectionDoors[oldRoomDoorIndex].SetActive(false);
            }

            if (playerMovement != null) playerMovement.SetCanMove(true);
            yield break; 
        }

        Room newRoom = entrancePoint.connectedTo.GetComponentInParent<Room>();

        ConnectionPoint exitPoint = null;
        if (newRoom != null)
        {
            exitPoint = newRoom.connectionPoints.FirstOrDefault(conn => conn.isConnected && conn.connectedTo == entrancePoint.transform);
        }

        if (newRoom == null || exitPoint == null)
        {
            Debug.LogError("Error crítico: La nueva sala o su punto de entrada no se encontraron correctamente después de la conexión. Deteniendo transición.");
            if (playerMovement != null) playerMovement.SetCanMove(true);
            yield break;
        }

        if (newRoom != null)
        {
            for (int i = 0; i < newRoom.connectionPoints.Length; i++)
            {
                BoxCollider boxCollider = newRoom.connectionPoints[i].GetComponent<BoxCollider>();
                if (boxCollider != null)
                {
                    boxCollider.enabled = false;
                }
            }
            newRoom.LockAllDoors();

            int newRoomEntranceDoorIndex = System.Array.IndexOf(newRoom.connectionPoints, exitPoint);
            if (newRoomEntranceDoorIndex != -1 && newRoom.connectionDoors.Length > newRoomEntranceDoorIndex && newRoom.connectionDoors[newRoomEntranceDoorIndex] != null)
            {
                newRoom.connectionDoors[newRoomEntranceDoorIndex].SetActive(false);
            }
        }

        Vector3 entranceDirection = GetDirectionFromConnectionType(entrancePoint.connectionType);

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ClearManipulationText();
        }

        yield return FadeController.Instance.FadeOut(
            onStart: () =>
            {
                Vector3 targetPosition = playerTransform.position + new Vector3(entranceDirection.x * playerMoveDistance, 0f, entranceDirection.z * playerMoveDistance);
                targetPosition.y = originalPlayerY;
                StartCoroutine(MovePlayerWithController(playerTransform, targetPosition, playerMoveDuration));
            },
            onComplete: () =>
            {
                if (exitPoint != null)
                {
                    Vector3 spawnPosition = new Vector3(exitPoint.transform.position.x, originalPlayerY, exitPoint.transform.position.z);
                    if (playerMovement != null)
                    {
                        playerMovement.TeleportTo(spawnPosition);
                    }
                    else
                    {
                        playerTransform.position = spawnPosition;
                    }
                }
            }
        );

        yield return FadeController.Instance.FadeIn(
            onStart: () =>
            {
                if (exitPoint != null)
                {
                    Vector3 exitDirection = GetDirectionFromConnectionType(exitPoint.connectionType);
                    Vector3 oppositeDirection = -exitDirection;
                    Vector3 targetPosition = playerTransform.position + new Vector3(oppositeDirection.x * playerMoveDistance, 0f, oppositeDirection.z * playerMoveDistance);
                    targetPosition.y = originalPlayerY;
                    StartCoroutine(MovePlayerWithController(playerTransform, targetPosition, playerMoveDuration));
                }
            },
            onComplete: () =>
            {
                int newRoomEntranceDoorIndex = System.Array.IndexOf(newRoom.connectionPoints, exitPoint);
                if (newRoomEntranceDoorIndex != -1 && newRoom.connectionDoors.Length > newRoomEntranceDoorIndex && newRoom.connectionDoors[newRoomEntranceDoorIndex] != null)
                {
                    StartCoroutine(ActivateEntranceDoorDelayed(newRoom.connectionDoors[newRoomEntranceDoorIndex]));
                }

                StartRoomTimer();

                if (DevilManipulationManager.Instance != null)
                {
                    if (newRoom != null && newRoom.roomType == RoomType.Combat)
                    {
                        DevilManipulationManager.Instance.TryActivatePendingManipulation();
                    }
                    else
                    {
                        DevilManipulationManager.Instance.NotifyNonCombatRoom();
                    }
                }

                if (newRoom != null && (newRoom.roomType == RoomType.Combat))
                {
                    var enemyManager = newRoom.GetComponent<EnemyManager>();
                    if (enemyManager != null)
                    {
                        StartCoroutine(enemyManager.StartCombatEncounter(exitPoint));
                    }
                }
                else if (newRoom != null && (newRoom.roomType == RoomType.Shop))
                {
                    if (DevilManipulationManager.Instance.PendingDistortion == DevilDistortionType.SealedLuck)
                    {
                        ShopManager.Instance.SetDistortionActive(DevilDistortionType.SealedLuck, true);
                    }
                    else 
                    {
                        DevilManipulationManager.Instance.TryActivatePendingManipulation(); 
                    }
                }
                else
                {
                    newRoom.EventsOnFinsih();
                    newRoom.UnlockExitDoors(exitPoint);
                }

                for (int i = 1; i < newRoom.connectionPoints.Length; i++)
                {
                    BoxCollider boxCollider = newRoom.connectionPoints[i].GetComponent<BoxCollider>();
                    if (boxCollider != null)
                    {
                        boxCollider.enabled = true;
                    }
                }

                if (playerMovement != null)
                {
                    playerMovement.SetCanMove(true);
                    if (newRoom != null) 
                    {
                        OnRoomEntered?.Invoke(newRoom.roomType);
                    }
                }
            }
        );
    }

    public void OnCombatRoomCleared(Room clearedRoom, ConnectionPoint entrancePoint) 
    {
        float roomCompletionTime = 0f;

        if (clearedRoom.roomType == RoomType.Combat && UIManager.Instance != null)
        {
            roomCompletionTime = UIManager.Instance.StopAndReportTimer();
        }

        OnRoomCompleted?.Invoke(clearedRoom.roomType, roomCompletionTime);

        clearedRoom.EventsOnFinsih();
        clearedRoom.UnlockExitDoors(entrancePoint);
    }

    private IEnumerator ActivateEntranceDoorDelayed(GameObject door)
    {
        yield return new WaitForSeconds(doorActivateDelay);
        door.SetActive(true);
    }

    private IEnumerator MovePlayerWithController(Transform playerTransform, Vector3 targetPosition, float duration)
    {
        PlayerEffectDistortion.Instance?.ClearAbyssalConfusion(); 
        PlayerHealth.Instance?.BlockKillHeal(false);             
        //VisibilityController.Instance?.SetVisibility(0f);     
        ShopManager.Instance?.SetDistortionActive(DevilDistortionType.SealedLuck, false); 

        Vector3 startPosition = playerTransform.position;

        float originalY = startPosition.y;
        targetPosition.y = originalY;

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            t = Mathf.SmoothStep(0f, 1f, t);

            Vector3 newPosition = Vector3.Lerp(startPosition, targetPosition, t);
            newPosition.y = originalY;

            if (playerMovement != null)
            {
                playerMovement.TeleportTo(newPosition);
            }
            else
            {
                playerTransform.position = newPosition;
            }

            yield return null;
        }

        Vector3 finalPos = targetPosition;
        finalPos.y = originalY;

        if (playerMovement != null)
        {
            playerMovement.TeleportTo(finalPos);
        }

        if (playerHealth != null && statsManager != null)
        {
            float healAmount = statsManager.GetStat(StatType.HealthPerRoomRegen);

            if (healAmount > 0)
            {
                playerHealth.Heal(healAmount);
            }
        }
    }
}