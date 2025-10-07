using UnityEngine;
using UnityEngine.Events;

public class Room : MonoBehaviour
{
    [Header("Connection Points")]
    public ConnectionPoint[] connectionPoints;

    [Header("Room Properties")]
    public RoomType roomType = RoomType.Normal;
    public bool isStartRoom = false;
    public bool isEndRoom = false;

    [Header("Room Components")]
    public GameObject[] connectionDoors;
    public BoxCollider[] spawnAreas;

    public UnityEvent onFinsih;

    private EnemyManager enemyManager;

    public void InitializeEnemyManager(EnemyManager manager)
    {
        this.enemyManager = manager;
    }

    public void LockAllDoors()
    {
        if (connectionDoors == null || connectionDoors.Length == 0) return;

        for (int i = 0; i < connectionDoors.Length; i++)
        {
            if (connectionDoors[i] != null)
            {
                connectionDoors[i].SetActive(true);
            }
        }
    }

    public void UnlockExitDoors(ConnectionPoint entrancePoint)
    {
        if (connectionDoors != null && connectionDoors.Length > 0)
        {
            for (int i = 0; i < connectionPoints.Length; i++)
            {
                if (i < connectionDoors.Length)
                {
                    if (connectionPoints[i] != entrancePoint && connectionDoors[i] != null)
                    {
                        connectionDoors[i].SetActive(false);
                    }
                }
            }
        }
    }
    public void EventsOnFinsih()
    {
        onFinsih?.Invoke();
    }
}