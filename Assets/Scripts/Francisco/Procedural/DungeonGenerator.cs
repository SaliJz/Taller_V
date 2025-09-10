using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DungeonGenerator : MonoBehaviour
{
    [Header("Room Prefabs")]
    public Room startRoomPrefab;
    public Room[] endRoomPrefabs;
    public Room[] normalRoomPrefabs;

    [Header("Generation Settings")]
    [Tooltip("El número mínimo de habitaciones normales antes del final.")]
    public int minRooms = 4;
    [Tooltip("El número máximo de habitaciones normales antes del final.")]
    public int maxRooms = 10;
    public int maxRoomAttempts = 10;

    private List<Room> generatedRooms = new List<Room>();
    private int roomsGenerated = 0;
    private string lastRoomPrefabName;
    private int finalRoomCount;

    public PlayerMovement playerMovement;

    void Start()
    {
        finalRoomCount = Random.Range(minRooms, maxRooms + 1);
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
        lastRoomPrefabName = initialRoom.name;
    }

    public void GenerateNextRoom(ConnectionPoint entrancePoint)
    {
        if (roomsGenerated >= finalRoomCount)
        {
            PlaceEndRoom(entrancePoint);
            return;
        }

        int attempts = 0;
        bool roomPlaced = false;

        while (attempts < maxRoomAttempts && !roomPlaced)
        {
            Room newRoomPrefab = GetRandomRoomPrefab();
            Room newRoom = Instantiate(newRoomPrefab);

            ConnectionPoint exitPoint = GetMatchingConnectionPoint(newRoom, entrancePoint);

            if (exitPoint != null)
            {
                Vector3 offset = exitPoint.transform.position - newRoom.transform.position;
                Vector3 finalPosition = entrancePoint.transform.position - offset;

                newRoom.transform.position = finalPosition;

                exitPoint.isConnected = true;
                exitPoint.connectedTo = entrancePoint.transform;
                entrancePoint.isConnected = true;
                entrancePoint.connectedTo = exitPoint.transform;

                generatedRooms.Add(newRoom);
                roomsGenerated++;
                lastRoomPrefabName = newRoomPrefab.name;

                foreach (ConnectionPoint connectionPoint in newRoom.connectionPoints)
                {
                    if (connectionPoint.GetComponent<BoxCollider>() == null)
                    {
                        BoxCollider collider = connectionPoint.gameObject.AddComponent<BoxCollider>();
                        collider.isTrigger = true;
                        collider.size = Vector3.one * 1f;
                        connectionPoint.gameObject.AddComponent<ConnectionTrigger>();
                    }
                }
                roomPlaced = true;
            }
            else
            {
                Destroy(newRoom.gameObject);
            }
            attempts++;
        }
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

            ConnectionPoint exitPoint = GetMatchingConnectionPoint(endRoom, entrancePoint);
            if (exitPoint != null)
            {
                Vector3 offset = exitPoint.transform.position - endRoom.transform.position;
                Vector3 finalPosition = entrancePoint.transform.position - offset;

                endRoom.transform.position = finalPosition;

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

    Room GetRandomRoomPrefab()
    {
        Room newPrefab = null;
        int maxRetries = 10;
        int retries = 0;

        while (retries < maxRetries)
        {
            newPrefab = normalRoomPrefabs[Random.Range(0, normalRoomPrefabs.Length)];
            if (newPrefab.name != lastRoomPrefabName)
            {
                return newPrefab;
            }
            retries++;
        }
        return newPrefab;
    }

    ConnectionPoint GetMatchingConnectionPoint(Room room, ConnectionPoint targetPoint)
    {
        foreach (ConnectionPoint conn in room.connectionPoints)
        {
            ConnectionType requiredType = ConnectionType.North;
            switch (targetPoint.connectionType)
            {
                case ConnectionType.North:
                    requiredType = ConnectionType.South;
                    break;
                case ConnectionType.South:
                    requiredType = ConnectionType.North;
                    break;
                case ConnectionType.East:
                    requiredType = ConnectionType.West;
                    break;
                case ConnectionType.West:
                    requiredType = ConnectionType.East;
                    break;
            }

            if (conn.connectionType == requiredType && !conn.isConnected)
            {
                return conn;
            }
        }
        return null;
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
                    Vector3 movePosition = exitPoint.transform.position;
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