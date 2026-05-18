using System.Collections;
using UnityEngine;

public class SequenceTransition : TransitionInteractive
{
    #region Inspector

    [Header("Acción Post-Nodos")]
    [SerializeField] private bool riseAfterNodes = false;
    [SerializeField] private float riseHeight = 5f;
    [SerializeField] private float riseDuration = 1.5f;

    [Header("Animación")]
    [SerializeField] private Animator[] objectsAnimators;
    [SerializeField] private string objectsExitParam = "Open";
    [SerializeField] private float objectsExitWait = 0.8f;

    #endregion

    #region Estado

    private bool isSequenceRunning = false;

    #endregion

    #region API pública

    public bool IsSequenceRunning => isSequenceRunning;

    public IEnumerator ExecuteSequence(Transform playerTransform)
    {
        isSequenceRunning = true;

        yield return StartCoroutine(RunNodes(playerTransform));

        if (objectsAnimators != null && objectsAnimators.Length > 0)
            yield return StartCoroutine(CloseDoors());

        Coroutine fadeRoutine = StartCoroutine(
            FadeController.Instance.FadeOut(
            onStart: null,
            onComplete: null));

        if (riseAfterNodes)
            yield return StartCoroutine(Rise(playerTransform));
        else
            RestoreControl();

        isSequenceRunning = false;
    }

    #endregion

    #region Lógica extra

    private IEnumerator CloseDoors()
    {
        foreach (Animator anim in objectsAnimators)
            if (anim != null) anim.SetBool(objectsExitParam, false);

        yield return new WaitForSeconds(objectsExitWait);
    }

    private IEnumerator Rise(Transform playerTransform)
    {
        CharacterController cc = playerTransform.GetComponent<CharacterController>();
        PlayerMovement pm = playerTransform.GetComponent<PlayerMovement>();

        if (cc != null) cc.enabled = false;
        pm?.SetCanMove(false);

        playerTransform.SetParent(transform);

        Vector3 start = transform.position;
        Vector3 end = start + Vector3.up * riseHeight;
        float elapsed = 0f;

        while (elapsed < riseDuration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(start, end,
                Mathf.SmoothStep(0f, 1f, elapsed / riseDuration));
            yield return null;
        }

        transform.position = end;
        playerTransform.SetParent(null);

        RestoreControl();
    }

    #endregion
}