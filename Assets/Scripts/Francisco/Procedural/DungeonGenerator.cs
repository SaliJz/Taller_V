using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class RoomProgressionRule
{
    public RoomType roomType;
    public int minRoomNumber = 1;
    public int maxRoomNumber = 10;
    public bool isMandatory;
    public bool isProbableMandatory;
    public bool generateOnce;
    [Range(0f, 100f)]
    public float probability = 0;
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

public class DungeonGenerator : MonoBehaviour
{
    [Header("Room Prefabs")]
    public Room startRoomPrefab;
    public Room[] endRoomPrefabs;

    [Header("Room Type Probabilities")]
    public RoomTypeProbability[] roomTypeProbabilities;

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

    private List<Room> generatedRooms = new List<Room>();
    private int roomsGenerated = 0;
    private int targetRoomCount;

    private Dictionary<RoomType, List<RoomData>> roomDataDictionary = new Dictionary<RoomType, List<RoomData>>();
    private Dictionary<RoomType, RoomType[]> generationRuleDictionary = new Dictionary<RoomType, RoomType[]>();
    private Dictionary<RoomType, RoomProgressionRule> progressionRuleDictionary = new Dictionary<RoomType, RoomProgressionRule>();

    private RoomProgressionRule mandatoryRoomToPlace;
    private int mandatoryRoomNumber = -1;
    private RoomProgressionRule probableMandatoryToPlace;
    private bool hasProbableMandatoryBeenGenerated = false;
    private float currentProbableMandatoryProbability;

    public PlayerMovement playerMovement;

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

        targetRoomCount = Random.Range(minRooms, maxRooms + 1);

        if (probableMandatoryToPlace != null)
        {
            currentProbableMandatoryProbability = probableMandatoryToPlace.probability;
        }

        PlanMandatoryRoom();
        GenerateInitialRoom();
        playerMovement = FindAnyObjectByType<PlayerMovement>();
    }

    void PlanMandatoryRoom()
    {
        var mandatoryRules = progressionRules.Where(r => r.isMandatory).ToList();
        if (mandatoryRules.Any())
        {
            mandatoryRoomToPlace = mandatoryRules.First();
            mandatoryRoomNumber = Random.Range(mandatoryRoomToPlace.minRoomNumber, mandatoryRoomToPlace.maxRoomNumber + 1);
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

            mandatoryRoomPrefabs = mandatoryRoomPrefabs.OrderBy(x => Random.value).ToList();

            foreach (var roomData in mandatoryRoomPrefabs)
            {
                if (attempts >= maxRoomAttempts) break;

                if (PlaceRoom(roomData.prefab, entrancePoint))
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

            RoomData newRoomData = GetRandomRoomData(previousRoomType, roomsGenerated + 1);
            if (newRoomData == null)
            {
                attempts++;
                continue;
            }

            roomPlaced = PlaceRoom(newRoomData.prefab, entrancePoint);
            if (roomPlaced)
            {
                UpdateRoomWeights(newRoomData);
                if (probableMandatoryToPlace != null && newRoomData.prefab.roomType == probableMandatoryToPlace.roomType)
                {
                    hasProbableMandatoryBeenGenerated = true;
                }
            }
            attempts++;
        }
    }

    bool PlaceRoom(Room newRoomPrefab, ConnectionPoint entrancePoint)
    {
        Room newRoom = Instantiate(newRoomPrefab);

        List<ConnectionPoint> exitPoints = GetAllMatchingConnectionPoints(newRoom, entrancePoint);

        if (exitPoints.Count > 0)
        {
            ConnectionPoint exitPoint = exitPoints[Random.Range(0, exitPoints.Count)];

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
                    var enemyManager = newRoom.gameObject.AddComponent<EnemyManager>();
                    enemyManager.dungeonGenerator = this;
                    enemyManager.parentRoom = newRoom;
                    enemyManager.enemyPrefabs = this.enemyPrefabs;
                    enemyManager.spawnEffectPrefab = this.spawnEffectPrefab; 
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
            Room endRoomPrefab = endRoomPrefabs[Random.Range(0, endRoomPrefabs.Length)];
            Room endRoom = Instantiate(endRoomPrefab);

            List<ConnectionPoint> exitPoints = GetAllMatchingConnectionPoints(endRoom, entrancePoint);
            if (exitPoints.Count > 0)
            {
                ConnectionPoint exitPoint = exitPoints[Random.Range(0, exitPoints.Count)];

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

    RoomData GetRandomRoomData(RoomType previousRoomType, int currentRoomNumber)
    {
        if (mandatoryRoomToPlace != null && roomsGenerated + 1 == mandatoryRoomNumber)
        {
            if (generationRuleDictionary.ContainsKey(previousRoomType) && !generationRuleDictionary[previousRoomType].Contains(mandatoryRoomToPlace.roomType))
            {
                return null;
            }
            var mandatoryRoomPrefabs = roomDataDictionary[mandatoryRoomToPlace.roomType];
            if (mandatoryRoomPrefabs != null && mandatoryRoomPrefabs.Count > 0)
            {
                return mandatoryRoomPrefabs[Random.Range(0, mandatoryRoomPrefabs.Count)];
            }
        }

        var probableMandatoryRules = progressionRules
            .Where(r => r.isProbableMandatory &&
                        currentRoomNumber >= r.minRoomNumber &&
                        currentRoomNumber <= r.maxRoomNumber &&
                        (!r.generateOnce || !hasProbableMandatoryBeenGenerated))
            .ToList();

        foreach (var rule in probableMandatoryRules)
        {
            float escalatingProbability = GetEscalatingProbability(currentRoomNumber);
            if (Random.Range(0f, 100f) < escalatingProbability)
            {
                if (generationRuleDictionary.ContainsKey(previousRoomType) && !generationRuleDictionary[previousRoomType].Contains(rule.roomType))
                {
                    continue;
                }
                var probableRoomPrefabs = roomDataDictionary[rule.roomType];
                if (probableRoomPrefabs != null && probableRoomPrefabs.Count > 0)
                {
                    if (rule.generateOnce)
                    {
                        hasProbableMandatoryBeenGenerated = true;
                    }
                    return probableRoomPrefabs[Random.Range(0, probableRoomPrefabs.Count)];
                }
            }
        }


        var progressionAllowedTypes = progressionRules.Where(rule =>
                currentRoomNumber >= rule.minRoomNumber &&
                currentRoomNumber <= rule.maxRoomNumber
            ).Select(p => p.roomType).ToList();

        RoomType[] generationAllowedTypes;
        if (!generationRuleDictionary.TryGetValue(previousRoomType, out generationAllowedTypes))
        {
            generationAllowedTypes = roomDataDictionary.Keys.ToArray();
        }

        var validRoomTypes = new List<RoomType>();
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
            return null;
        }

        var filteredProbabilities = roomTypeProbabilities.Where(p => validRoomTypes.Contains(p.roomType)).ToList();

        float totalProbability = filteredProbabilities.Sum(p => p.probability);
        if (totalProbability <= 0)
        {
            return GetRandomRoomDataFromList(roomDataDictionary, validRoomTypes);
        }

        float randomValue = Random.Range(0f, totalProbability);
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

        var roomDataList = roomDataDictionary[selectedType];
        float totalWeight = roomDataList.Sum(data => data.weight);

        if (totalWeight <= 0)
        {
            ResetRoomWeights(selectedType);
            totalWeight = roomDataList.Sum(data => data.weight);
            if (totalWeight <= 0) return null;
        }

        float randomWeightValue = Random.Range(0f, totalWeight);
        float currentWeightSum = 0f;

        foreach (var data in roomDataList)
        {
            currentWeightSum += data.weight;
            if (randomWeightValue <= currentWeightSum)
            {
                return data;
            }
        }

        return roomDataList[Random.Range(0, roomDataList.Count)];
    }

    private RoomData GetRandomRoomDataFromList(Dictionary<RoomType, List<RoomData>> roomDict, List<RoomType> validTypes)
    {
        if (!validTypes.Any()) return null;
        RoomType randomType = validTypes[Random.Range(0, validTypes.Count)];
        var roomDataList = roomDict[randomType];
        return roomDataList[Random.Range(0, roomDataList.Count)];
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

    public IEnumerator TransitionToNextRoom(ConnectionPoint entrancePoint, Transform playerTransform)
    {
        if (playerMovement == null)
        {
            playerMovement = FindAnyObjectByType<PlayerMovement>();
        }

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
        Room newRoom = entrancePoint.connectedTo.GetComponentInParent<Room>();
        ConnectionPoint exitPoint = newRoom.connectionPoints.FirstOrDefault(conn => conn.isConnected && conn.connectedTo == entrancePoint.transform);

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

                if (newRoom != null && newRoom.roomType == RoomType.Combat)
                {
                    var enemyManager = newRoom.GetComponent<EnemyManager>();
                    if (enemyManager != null)
                    {
                        StartCoroutine(enemyManager.StartCombatEncounter(exitPoint, this, spawnEffectPrefab));
                    }
                }
                else
                {
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
                }
            }
        );
    }

    public void OnCombatEnded(Room combatRoom, ConnectionPoint entrancePoint)
    {
        Debug.Log("Combate terminado. Abriendo puertas de salida...");
        combatRoom.UnlockExitDoors(entrancePoint);
    }

    private IEnumerator ActivateEntranceDoorDelayed(GameObject door)
    {
        yield return new WaitForSeconds(doorActivateDelay);
        door.SetActive(true);
    }

    private IEnumerator MovePlayerWithController(Transform playerTransform, Vector3 targetPosition, float duration)
    {
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
        else
        {
            playerTransform.position = finalPos;
        }
    }
}