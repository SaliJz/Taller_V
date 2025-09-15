using UnityEngine;

public class Room : MonoBehaviour
{
    public ConnectionPoint[] connectionPoints;
    public RoomType roomType = RoomType.Normal;
    public bool isStartRoom = false;
    public bool isEndRoom = false;
    public GameObject[] connectionDoors;
    public BoxCollider[] spawnAreas;
    private EnemyManager enemyManager;

    public void InitializeEnemyManager(EnemyManager manager)
    {
        this.enemyManager = manager;
    }

    public void LockAllDoors()
    {
        for (int i = 0; i < connectionDoors.Length; i++)
        {
            if (connectionDoors[i] != null)
            {
                connectionDoors[i].SetActive(true);
            }
        }
    }

    public void UnlockAllDoors()
    {
        foreach (var door in connectionDoors)
        {
            if (door != null)
            {
                door.SetActive(false);
            }
        }
    }

    public void UnlockExitDoors(ConnectionPoint entrancePoint)
    {
        for (int i = 0; i < connectionPoints.Length; i++)
        {
            if (connectionPoints[i] != entrancePoint && connectionPoints[i].isConnected)
            {
                if (connectionDoors[i] != null)
                {
                    connectionDoors[i].SetActive(false);
                }
            }
        }
    }
}