using System.Collections;
using UnityEngine;

public class ElevatorRiseController : MonoBehaviour
{
    #region Inspector

    [Header("Centro del ascensor")]
    [SerializeField] private Transform centerPoint;
    [SerializeField] private float centeringSpeed = 4f;
    [SerializeField] private float centeringThreshold = 0.05f;

    [Header("Puertas del ascensor")]
    [SerializeField] private Animator[] elevatorDoorAnimators;
    [SerializeField] private string doorCloseParam = "Open";
    [SerializeField] private float doorCloseWait = 0.8f;

    [Header("Elevación")]
    [SerializeField] private float riseHeight = 5f;
    [SerializeField] private float riseDuration = 1.5f;

    #endregion

    #region Estado

    private bool hasRisen = false;
    private bool isRising = false;

    #endregion

    #region API pública

    public bool IsRising => isRising;

    public IEnumerator ExecuteSequence(Transform playerTransform)
    {
        if (hasRisen) yield break;

        hasRisen = true;
        isRising = true;

        yield return StartCoroutine(CenterPlayer(playerTransform));

        yield return StartCoroutine(CloseDoors()); 

        StartCoroutine(Rise(playerTransform));

        Coroutine fadeRoutine = StartCoroutine(
            FadeController.Instance.FadeOut(
                onStart: null,
                onComplete: null));

        yield return fadeRoutine;

        isRising = false;
    }

    #endregion

    #region Lógica 

    private IEnumerator CenterPlayer(Transform playerTransform)
    {
        if (centerPoint == null) yield break;

        CharacterController cc = playerTransform.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        Vector3 target = centerPoint.position;
        target.y = playerTransform.position.y;

        while (Vector3.Distance(playerTransform.position, target) > centeringThreshold)
        {
            playerTransform.position = Vector3.MoveTowards(
                playerTransform.position, target, centeringSpeed * Time.deltaTime);
            yield return null;
        }

        playerTransform.position = target;
    }

    private IEnumerator CloseDoors()
    {
        if (elevatorDoorAnimators == null ||
            elevatorDoorAnimators.Length == 0)
        {
            yield break;
        }

        foreach (Animator anim in elevatorDoorAnimators)
        {
            if (anim != null)
            {
                anim.SetBool(doorCloseParam, false);
            }
        }

        yield return new WaitForSeconds(doorCloseWait);
    }


    private IEnumerator Rise(Transform playerTransform)
    {
        playerTransform.SetParent(transform);

        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + Vector3.up * riseHeight;
        float elapsed = 0f;

        while (elapsed < riseDuration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, endPos, Mathf.SmoothStep(0f, 1f, elapsed / riseDuration));
            yield return null;
        }

        transform.position = endPos;
        playerTransform.SetParent(null);
    }

    #endregion
}