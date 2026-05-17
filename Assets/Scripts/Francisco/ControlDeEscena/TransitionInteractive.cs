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

    #region Referencias 

    private CharacterController characterController;
    private PlayerAnimCtrl playerAnimCtrl;
    private Transform playerTransform;

    #endregion

    #region Estado

    private bool isRunning = false;

    #endregion

    #region API pública

    public bool IsRunning => isRunning;

    public void StartTraslation(Transform player)
    {
        if (isRunning || nodes == null || nodes.Count == 0) return;

        playerTransform = player;
        characterController = player.GetComponent<CharacterController>();
        playerAnimCtrl = player.GetComponentInChildren<PlayerAnimCtrl>();

        StartCoroutine(RunTraslation());
    }

    #endregion

    #region Lógica 

    private IEnumerator RunTraslation()
    {
        isRunning = true;

        if (characterController != null)
            characterController.enabled = false;

        for (int i = 0; i < nodes.Count; i++)
        {
            Nodo node = nodes[i];
            if (node == null) continue;

            bool hasNext = i + 1 < nodes.Count;
            Nodo nextNode = hasNext ? nodes[i + 1] : null;

            Vector3 target = node.NodeTransform.position;
            if (!node.UseYAxis)
                target.y = playerTransform.position.y;

            SetAnimationTowardsNode(node, nextNode);

            while (Vector3.Distance(playerTransform.position, target) > arrivalThreshold)
            {
                Vector3 dir = (target - playerTransform.position).normalized;
                playerTransform.position = Vector3.MoveTowards(playerTransform.position, target, moveSpeed * Time.deltaTime);

                Vector3 toNext = nextNode != null
                    ? (nextNode.NodeTransform.position - playerTransform.position)
                    : dir;
                toNext.y = 0f;
                if (toNext.sqrMagnitude > 0.01f)
                    playerTransform.rotation = Quaternion.LookRotation(toNext.normalized, Vector3.up);

                yield return null;
            }

            playerTransform.position = target;

            if (playerAnimCtrl != null)
                playerAnimCtrl.SetInputAxes(0f, 0f);

            yield return null;
        }

        if (characterController != null)
            characterController.enabled = true;

        isRunning = false;
    }

    private void SetAnimationTowardsNode(Nodo current, Nodo next)
    {
        if (playerAnimCtrl == null) return;

        Nodo target = next != null ? next : current;
        Vector3 dir = target.NodeTransform.position - playerTransform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f) return;

        dir.Normalize();

        Vector3 camForward = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;
        Vector3 camRight = Camera.main != null ? Camera.main.transform.right : Vector3.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        float h = Vector3.Dot(dir, camRight);
        float v = Vector3.Dot(dir, camForward);

        playerAnimCtrl.SetInputAxes(h, v);
    }

    #endregion
}