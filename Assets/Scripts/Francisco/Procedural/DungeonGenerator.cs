using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
    public Room[] normalRoomPrefabs;

    [Header("Enemy Prefabs")]
    public GameObject[] enemyPrefabs;

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

    private List<RoomData> normalRoomData = new List<RoomData>();

    public PlayerMovement playerMovement;

    void Start()
    {
        foreach (var prefab in normalRoomPrefabs)
        {
            normalRoomData.Add(new RoomData { prefab = prefab, weight = 1.0f });
        }

        targetRoomCount = Random.Range(minRooms, maxRooms + 1);

        GenerateInitialRoom();
        playerMovement = FindAnyObjectByType<PlayerMovement>();
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

        while (attempts < maxRoomAttempts && !roomPlaced)
        {
            if (entrancePoint.isConnected)
            {
                return;
            }

            RoomData newRoomData = GetRandomRoomData();
            Room newRoomPrefab = newRoomData.prefab;
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
                    roomPlaced = true;

                    UpdateRoomWeights(newRoomData);

                    if (newRoom.roomType == RoomType.Combat)
                    {
                        var enemyManager = newRoom.gameObject.AddComponent<EnemyManager>();
                        enemyManager.dungeonGenerator = this;
                        enemyManager.parentRoom = newRoom;
                        enemyManager.enemyPrefabs = this.enemyPrefabs;
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
                }
                else
                {
                    Destroy(newRoom.gameObject);
                }
            }
            else
            {
                Destroy(newRoom.gameObject);
            }
            attempts++;
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

    RoomData GetRandomRoomData()
    {
        float totalWeight = normalRoomData.Sum(data => data.weight);

        if (totalWeight <= 0)
        {
            ResetAllWeights();
            totalWeight = normalRoomData.Sum(data => data.weight);
        }

        float randomValue = Random.Range(0f, totalWeight);
        float currentSum = 0f;

        foreach (var data in normalRoomData)
        {
            currentSum += data.weight;
            if (randomValue <= currentSum)
            {
                return data;
            }
        }

        return normalRoomData[Random.Range(0, normalRoomData.Count)];
    }

    void UpdateRoomWeights(RoomData usedRoom)
    {
        usedRoom.repetitionCount++;

        usedRoom.weight = 1.0f / (1.0f + usedRoom.repetitionCount * repetitionPenalty);

        foreach (var data in normalRoomData)
        {
            if (data != usedRoom)
            {
                data.weight = Mathf.Min(1.0f, data.weight * (1.0f + weightDecay * 0.1f));
            }
        }
    }

    void ResetAllWeights()
    {
        foreach (var data in normalRoomData)
        {
            data.weight = 1.0f / (1.0f + data.repetitionCount * repetitionPenalty * 0.5f);
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
                        StartCoroutine(enemyManager.StartCombatEncounter(exitPoint, this));
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