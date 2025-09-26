using System.Collections;
using UnityEngine;

public class RoomTransitionTrigger : MonoBehaviour
{
    [Header("Room Generation")]
    public GameObject roomPrefab;
    public float generationDistance = 50f;
    public string playerSpawnPointName = "SpawnPoint";

    [Header("Player Control References")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerMeleeAttack playerMeleeAttack;
    [SerializeField] private PlayerShieldController playerShieldController;

    private bool hasTriggered = false;
    private GameObject player;

    private void Awake()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerMovement = player.GetComponent<PlayerMovement>();
            playerMeleeAttack = player.GetComponent<PlayerMeleeAttack>();
            playerShieldController = player.GetComponent<PlayerShieldController>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !hasTriggered)
        {
            hasTriggered = true;
            StartCoroutine(TransitionToNextRoom());
        }
    }

    private IEnumerator TransitionToNextRoom()
    {
        SetPlayerControls(false);

        yield return StartCoroutine(FadeController.Instance.FadeOut());

        Vector3 spawnPosition = transform.position + transform.forward * generationDistance;
        GameObject spawnedRoom = Instantiate(roomPrefab, spawnPosition, Quaternion.identity);

        Transform playerSpawnPoint = FindPlayerSpawnPoint(spawnedRoom);
        if (playerSpawnPoint != null)
        {
            playerMovement.TeleportTo(playerSpawnPoint.position);
            Debug.Log($"Jugador teletransportado a la nueva sala en: {playerSpawnPoint.position}");
        }
        else
        {
            Debug.LogError($"No se encontró un GameObject con el nombre '{playerSpawnPointName}' en la nueva sala. El jugador no será teletransportado.");
        }

        yield return StartCoroutine(FadeController.Instance.FadeIn());

        SetPlayerControls(true);
    }

    private Transform FindPlayerSpawnPoint(GameObject room)
    {
        return room.transform.Find(playerSpawnPointName);
    }

    private void SetPlayerControls(bool enabled)
    {
        if (playerMovement != null) playerMovement.enabled = enabled;
        if (playerMeleeAttack != null) playerMeleeAttack.enabled = enabled;
        if (playerShieldController != null) playerShieldController.enabled = enabled;
    }
}