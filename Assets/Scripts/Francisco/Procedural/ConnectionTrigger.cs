using UnityEngine;

public class ConnectionTrigger : MonoBehaviour
{
    private DungeonGenerator dungeonGenerator;
    private ConnectionPoint connectionPoint;

    [Header("SFX Configuration")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip transitionClip;

    private bool hasTriggered = false;

    void Start()
    {
        dungeonGenerator = FindAnyObjectByType<DungeonGenerator>();
        connectionPoint = GetComponent<ConnectionPoint>();
        if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>();
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

        if (audioSource != null && transitionClip != null)
        {
            audioSource.PlayOneShot(transitionClip);
        }

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