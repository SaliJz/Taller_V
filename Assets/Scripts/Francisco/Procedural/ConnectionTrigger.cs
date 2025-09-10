using UnityEngine;

public class ConnectionTrigger : MonoBehaviour
{
    private DungeonGenerator dungeonGenerator;
    private ConnectionPoint connectionPoint;

    void Start()
    {
        dungeonGenerator = FindAnyObjectByType<DungeonGenerator>();
        connectionPoint = GetComponent<ConnectionPoint>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !connectionPoint.isConnected)
        {
            dungeonGenerator.StartCoroutine(dungeonGenerator.TransitionToNextRoom(connectionPoint, other.transform));
            GetComponent<BoxCollider>().enabled = false;
        }
    }
}