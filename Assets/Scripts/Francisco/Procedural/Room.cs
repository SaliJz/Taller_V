using UnityEngine;

public class Room : MonoBehaviour
{
    [Header("Connection Points")]
    public ConnectionPoint[] connectionPoints;

    [Header("Room Properties")]
    public RoomType roomType = RoomType.Normal;
    public bool isStartRoom = false;
    public bool isEndRoom = false;
}