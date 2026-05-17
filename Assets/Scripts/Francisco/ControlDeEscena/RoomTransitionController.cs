using System.Collections;
using UnityEngine;

#region Enum de Modos

public enum TransitionMode
{
    Classic,
    Level1
}

#endregion

public class RoomTransitionController : MonoBehaviour
{
    #region Inspector 

    [Header("Modo de Transición")]
    [SerializeField] private TransitionMode transitionMode = TransitionMode.Classic;

    [Header("Configuración Classic")]
    [SerializeField] private float playerMoveDuration = 0.5f;
    [SerializeField] private float playerMoveDistance = 3f;
    [SerializeField] private float doorActivateDelay = 0.5f;

    [Header("Configuración Level 1")]
    [SerializeField] private TransitionInteractive transitionInteractive;
    [SerializeField] private ElevatorRiseController elevatorController;

    #endregion

    #region Dependencias

    private PlayerMovement playerMovement;

    #endregion

    #region Unity

    private void Start()
    {
        playerMovement = FindAnyObjectByType<PlayerMovement>();
    }

    #endregion

    #region API pública

    public TransitionMode Mode => transitionMode;
    public float PlayerMoveDuration => playerMoveDuration;
    public float PlayerMoveDistance => playerMoveDistance;

    public IEnumerator RunPreFadeSequence(Transform playerTransform)
    {
        if (transitionMode == TransitionMode.Level1 && elevatorController != null)
        {
            yield return StartCoroutine(elevatorController.ExecuteSequence(playerTransform));
        }
    }

    public bool HasPreFadeSequence =>
        transitionMode == TransitionMode.Level1 && elevatorController != null;

    public IEnumerator MovePlayerSmooth(Transform playerTransform, Vector3 targetPosition, float duration)
    {
        Vector3 startPosition = playerTransform.position;
        float originalY = startPosition.y;
        targetPosition.y = originalY;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            Vector3 newPos = Vector3.Lerp(startPosition, targetPosition, t);
            newPos.y = originalY;

            if (playerMovement != null)
                playerMovement.TeleportTo(newPos);
            else
                playerTransform.position = newPos;

            yield return null;
        }

        Vector3 finalPos = targetPosition;
        finalPos.y = originalY;

        if (playerMovement != null)
            playerMovement.TeleportTo(finalPos);
        else
            playerTransform.position = finalPos;
    }

    public IEnumerator ActivateDoorDelayed(GameObject door)
    {
        yield return new WaitForSeconds(doorActivateDelay);
        if (door != null) door.SetActive(true);
    }

    #endregion
}