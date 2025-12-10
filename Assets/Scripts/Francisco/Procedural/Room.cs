using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class DoorColliders
{
    [Tooltip("Piezas que se activan cuando la puerta se abre")]
    public GameObject[] activateOnOpen;

    [Tooltip("Piezas que se desactivan cuando la puerta se abre")]
    public GameObject[] deactivateOnOpen;
}

public class Room : MonoBehaviour
{
    [Header("Connection Points")]
    public ConnectionPoint[] connectionPoints;

    public AudioSource roomAudioSource;
    public AudioClip openDoorClip;

    [Header("Room Properties")]
    public RoomType roomType = RoomType.Normal;
    public bool isStartRoom = false;
    public bool isEndRoom = false;

    [Header("Room Components")]
    public GameObject[] connectionDoors;
    public BoxCollider[] spawnAreas;

    [Header("Animated Doors")]
    public Animator[] animatedDoors;

    [Header("Door Colliders")]
    public DoorColliders[] doorColliders;

    [Header("Events")]
    public UnityEvent onFinsih;
    public RoomType currentRoomType { get; set; }

    private ConnectionPoint cachedEntrancePoint;
    private EnemyManager enemyManager;

    private void Awake()
    {
        if (roomAudioSource == null) roomAudioSource = GetComponentInChildren<AudioSource>();
        if (roomAudioSource == null)
        {
            Debug.LogWarning("No se encontró AudioSource en Room ni en sus elementos hijos.");
        }
    }

    private void Start()
    {
        InitializeDoorStates();
    }

    public void InitializeEnemyManager(EnemyManager manager)
    {
        this.enemyManager = manager;
    }

    public void Initialize(RoomType type)
    {
        currentRoomType = type; 
    }

    private void InitializeDoorStates()
    {
        if (animatedDoors == null || animatedDoors.Length == 0) return;

        for (int i = 0; i < animatedDoors.Length; i++)
        {
            if (animatedDoors[i] != null)
            {
                animatedDoors[i].SetBool("Open", false);

                if (doorColliders != null && i < doorColliders.Length)
                {
                    foreach (GameObject obj in doorColliders[i].activateOnOpen)
                    {
                        if (obj != null) obj.SetActive(false);
                    }

                    foreach (GameObject obj in doorColliders[i].deactivateOnOpen)
                    {
                        if (obj != null) obj.SetActive(true);
                    }
                }
            }
        }
    }

    public void SetEntrancePoint(ConnectionPoint entrancePoint)
    {
        cachedEntrancePoint = entrancePoint;
    }

    public void UnlockExitDoorsManually()
    {
        if (cachedEntrancePoint != null)
        {
            UnlockExitDoors(cachedEntrancePoint);
        }
        else
        {
            Debug.LogWarning("No hay punto de entrada cacheado. Asegúrate de llamar SetEntrancePoint() primero.");
        }
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

        for (int i = 0; i < animatedDoors.Length; i++)
        {
            if (animatedDoors[i] != null)
            {
                animatedDoors[i].SetBool("Open", false);

                if (doorColliders != null && i < doorColliders.Length)
                {
                    foreach (GameObject obj in doorColliders[i].activateOnOpen)
                    {
                        if (obj != null) obj.SetActive(false);
                    }

                    foreach (GameObject obj in doorColliders[i].deactivateOnOpen)
                    {
                        if (obj != null) obj.SetActive(true);
                    }
                }
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
                        OpenDoor(i);
                    }
                }
            }
        }
    }

    private void OpenDoor(int doorIndex)
    {
        if (doorIndex < 0 || doorIndex >= animatedDoors.Length) return;

        Animator animator = animatedDoors[doorIndex];
        if (animator == null) return;

        animator.SetBool("Open", true);

        if (roomAudioSource != null && openDoorClip != null)
        {
            roomAudioSource.PlayOneShot(openDoorClip);
        }

        if (doorColliders != null && doorIndex < doorColliders.Length)
        {
            foreach (GameObject obj in doorColliders[doorIndex].activateOnOpen)
            {
                if (obj != null) obj.SetActive(true);
            }

            foreach (GameObject obj in doorColliders[doorIndex].deactivateOnOpen)
            {
                if (obj != null) obj.SetActive(false);
            }
        }
    }

    public void EventsOnFinsih()
    {
        onFinsih?.Invoke();
    }
}