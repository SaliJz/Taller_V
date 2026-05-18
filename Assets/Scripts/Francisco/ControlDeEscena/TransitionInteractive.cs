using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TransitionInteractive : MonoBehaviour
{
    #region Inspector

    [Header("Nodos de Trayectoria")]
    [SerializeField] private List<Nodo> nodes = new List<Nodo>();

    [Header("Velocidad")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float arrivalThreshold = 0.05f;

    #endregion

    #region Referencias internas

    private CharacterController characterController;
    private PlayerMovement playerMovement;
    private PlayerAnimCtrl playerAnimCtrl;
    private Transform playerTransform;

    #endregion

    #region Estado

    private bool isRunning = false;

    #endregion

    #region API pública

    public bool IsRunning => isRunning;
    public List<Nodo> Nodes => nodes;

    public IEnumerator RunNodes(Transform player)
    {
        if (nodes == null || nodes.Count == 0) yield break;

        playerTransform = player;
        characterController = player.GetComponent<CharacterController>();
        playerMovement = player.GetComponent<PlayerMovement>();
        playerAnimCtrl = player.GetComponentInChildren<PlayerAnimCtrl>();

        isRunning = true;

        if (characterController != null) characterController.enabled = false;
        playerMovement?.SetCanMove(false);

        for (int i = 0; i < nodes.Count; i++)
        {
            Nodo node = nodes[i];
            if (node == null) continue;

            bool hasNext = i + 1 < nodes.Count;
            Nodo nextNode = hasNext ? nodes[i + 1] : null;

            Vector3 target = node.NodeTransform.position;
            if (!node.UseYAxis)
                target.y = playerTransform.position.y;

            while (Vector3.Distance(playerTransform.position, target) > arrivalThreshold)
            {
                playerTransform.position = Vector3.MoveTowards(
                    playerTransform.position,
                    target,
                    moveSpeed * Time.deltaTime);

                Vector3 moveDir = target - playerTransform.position;
                moveDir.y = 0f;

                if (moveDir.sqrMagnitude > 0.001f)
                {
                    moveDir.Normalize();

                    playerTransform.rotation =
                        Quaternion.LookRotation(moveDir, Vector3.up);

                    SetAnimDir(moveDir);
                }

                yield return null;
            }

            playerTransform.position = target;

            playerAnimCtrl?.SetInputAxes(0f, 0f);

            playerAnimCtrl?.PlayState(
                PlayerAnimCtrl.PlayerState.idle,
                BaseAnimCtrl<PlayerAnimCtrl.PlayerState>.AnimPriority.locomotion
            );

            yield return null;
        }

        isRunning = false;
    }

    public void RestoreControl()
    {
        if (characterController != null) characterController.enabled = true;
        playerMovement?.SetCanMove(true);
    }

    #endregion

    #region Lógica interna

    private void SetAnimDir(Vector3 worldDir)
    {
        if (playerAnimCtrl == null) return;

        worldDir.y = 0f;
        if (worldDir.sqrMagnitude < 0.001f) return;
        worldDir.Normalize();

        Transform cam = Camera.main != null ? Camera.main.transform : null;
        Vector3 camFwd = cam != null ? cam.forward : Vector3.forward;
        Vector3 camRight = cam != null ? cam.right : Vector3.right;
        camFwd.y = 0f;
        camRight.y = 0f;
        camFwd.Normalize();
        camRight.Normalize();

        float h = Mathf.Round(Vector3.Dot(worldDir, camRight));
        float v = Mathf.Round(Vector3.Dot(worldDir, camFwd));

        playerAnimCtrl.SetInputAxes(h, v);
        playerAnimCtrl.UpdateDirection(h, v);
    }

    #endregion
}