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

    [Header("Generation Settings")]
    public int minRooms = 8;
    public int maxRooms = 12;
    public int maxRoomAttempts = 15;
    [Range(0.1f, 2.0f)]
    public float repetitionPenalty = 0.8f;
    [Range(0.1f, 1.0f)]
    public float weightDecay = 0.7f;

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
                Vector3 finalPosition = entrancePoint.transform.position - offset;

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
                Vector3 finalPosition = entrancePoint.transform.position - offset;

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
        if (playerMovement != null)
        {
            playerMovement.SetCanMove(false);
        }

        yield return FadeController.Instance.FadeOut(() =>
        {
            GenerateNextRoom(entrancePoint);

            Room newRoom = entrancePoint.connectedTo.GetComponentInParent<Room>();
            if (newRoom != null)
            {
                ConnectionPoint exitPoint = null;
                foreach (ConnectionPoint conn in newRoom.connectionPoints)
                {
                    if (conn.isConnected && conn.connectedTo == entrancePoint.transform)
                    {
                        exitPoint = conn;
                        break;
                    }
                }
                if (exitPoint != null)
                {
                    Vector3 movePosition = exitPoint.transform.position + exitPoint.transform.forward * 2f;
                    playerTransform.position = movePosition;
                }
            }
        });

        yield return new WaitForSeconds(0.5f);
        yield return FadeController.Instance.FadeIn();

        if (playerMovement != null)
        {
            playerMovement.SetCanMove(true);
        }
    }
}