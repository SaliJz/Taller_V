using UnityEngine;

public class ConnectionTrigger : MonoBehaviour
{
    private DungeonGenerator dungeonGenerator;
    private ConnectionPoint connectionPoint;
    private bool hasTriggered = false;

    void Start()
    {
        dungeonGenerator = FindAnyObjectByType<DungeonGenerator>();
        connectionPoint = GetComponent<ConnectionPoint>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasTriggered ||
            connectionPoint == null ||
            connectionPoint.isConnected ||
            dungeonGenerator == null ||
            !other.CompareTag("Player"))
        {
            return;
        }

        hasTriggered = true;

        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider != null)
        {
            boxCollider.enabled = false;
        }

        dungeonGenerator.StartCoroutine(
            dungeonGenerator.TransitionToNextRoom(connectionPoint, other.transform)
        );
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && hasTriggered)
        {
            BoxCollider boxCollider = GetComponent<BoxCollider>();
            if (boxCollider != null)
            {
                boxCollider.enabled = false;
            }
        }
    }
}