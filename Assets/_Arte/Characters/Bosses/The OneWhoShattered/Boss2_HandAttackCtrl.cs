using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class Boss2_HandAttackCtrl : MonoBehaviour
{
    #region Referencias

    [Header("Referencias")]
    [SerializeField] private Transform handTransform;
    [SerializeField] private SpriteRenderer handRend;

    #endregion

    #region Inspector - Rise Settings

    [Header("Rise Settings")]
    [SerializeField] private float targetY = 1f;
    [SerializeField] private float hiddenY = -1f;
    [SerializeField] private float riseDuration = 2f;

    #endregion

    #region  Inspector - Anticipation Shake

    [Header("Anticipation Shake")]
    [SerializeField] private float shakeIntensity = 0.2f;
    [SerializeField] private float shakeFrequency = 2f;
    [SerializeField] public float shakeDuration = 1f;

    [SerializeField] private float waitToGoDown = 0.5f;

    #endregion

    #region  Inspector Evento para daño

    [Header("Event")]
    public UnityEvent OnAttack;

    #endregion

    #region Internal Variables

    Vector3 originalLocalPosition;
    MaterialPropertyBlock mpb;
    bool canGoDown = false; //Esto se usará en un evento de animacion cuando estén las animacionees

    Coroutine currentRutine;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        originalLocalPosition = handTransform.position;
        handTransform.localPosition = new Vector3(0f, hiddenY, 0f);

        mpb = new MaterialPropertyBlock();
    }

    void LateUpdate()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "AndreiNew")
        {
            if (Input.GetKeyDown(KeyCode.O))
            {
                if (currentRutine != null) StopCoroutine(currentRutine);
                currentRutine = StartCoroutine(AttackSecuence());
            }
        }
    }

    private IEnumerator AttackSecuence()
    {

        yield return RiseRoutine();

        yield return ShakeRoutine();

        OnAttack?.Invoke();

        handRend.GetPropertyBlock(mpb);
        mpb.SetFloat("_Amount", 0);
        handRend.SetPropertyBlock(mpb);

        yield return new WaitForSeconds(waitToGoDown);

        yield return ReturnDownRutine();
    }

    #endregion

    #region Corrutines

    private IEnumerator RiseRoutine()
    {
        float elapse = 0;
        Vector3 startPos = new Vector3(0f, hiddenY, 0f);
        Vector3 endPos = new Vector3(0f, targetY, 0f);
        handTransform.localPosition = startPos;

        while (elapse < riseDuration)
        {
            elapse += Time.deltaTime;
            float t = elapse/riseDuration;

            handTransform.localPosition = Vector3.Lerp(startPos,endPos, t);
            yield return null;
        }    

        handTransform.localPosition = endPos;
    }

    private IEnumerator ShakeRoutine()
    {
        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            float t = elapsed/shakeDuration;

            float offsetX = Mathf.Sin(elapsed * shakeFrequency * Mathf.PI * 2f) * shakeIntensity;
            float offsetZ = Mathf.Cos(elapsed * shakeFrequency * Mathf.PI * 2.3f) * shakeIntensity * 0.6f;
            handTransform.localPosition = originalLocalPosition + new Vector3(offsetX, 0f, offsetZ);

            handRend.GetPropertyBlock(mpb);
            mpb.SetFloat("_Amount", Mathf.Lerp(0f, 1f, t));
            handRend.SetPropertyBlock(mpb);

            elapsed += Time.deltaTime;
            yield return null;
        }

        handTransform.localPosition = originalLocalPosition;
    }

    private IEnumerator ReturnDownRutine()
    {
        float elapse = 0f;
        Vector3 startPos = handTransform.position;
        Vector3 endPos = new Vector3(0f, hiddenY, 0f);

        while (elapse < riseDuration)
        {
            elapse += Time.deltaTime;

            float t = elapse / riseDuration;
            handTransform.localPosition = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        handTransform.localPosition = endPos;
    }

    #endregion

}
