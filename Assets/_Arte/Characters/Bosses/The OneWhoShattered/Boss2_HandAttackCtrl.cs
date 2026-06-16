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

    #region  Inspector Eventos

    [Header("Events")]
    public UnityEvent OnAttack;
    public UnityEvent OnSequenceEnd;

    #endregion

    #region Internal Variables

    private Vector3 originalLocalPosition;
    private MaterialPropertyBlock mpb;
    private Coroutine currentRoutine;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        originalLocalPosition = handTransform != null ? handTransform.localPosition : Vector3.zero;
        if (handTransform != null)
        {
            handTransform.localPosition = new Vector3(originalLocalPosition.x, hiddenY, originalLocalPosition.z);
        }

        mpb = new MaterialPropertyBlock();
    }

    void LateUpdate()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "AndreiNew")
        {
            if (Input.GetKeyDown(KeyCode.O))
            {
                TriggerAttackSequence();
            }
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Inicia toda la secuencia visual (Rise -> Shake -> Attack -> ReturnDown)
    /// </summary>
    public void TriggerAttackSequence()
    {
        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(AttackSequence());
    }

    #endregion

    #region Secuencia Principal

    private IEnumerator AttackSequence()
    {
        yield return RiseRoutine();

        yield return ShakeRoutine();

        OnAttack?.Invoke();

        handRend.GetPropertyBlock(mpb);
        mpb.SetFloat("_Amount", 0);
        handRend.SetPropertyBlock(mpb);

        yield return new WaitForSeconds(waitToGoDown);

        yield return ReturnDownRutine();

        OnSequenceEnd?.Invoke();
    }

    #endregion

    #region Corrutinas de Movimiento

    private IEnumerator RiseRoutine()
    {
        float elapse = 0;
        Vector3 startPos = new Vector3(originalLocalPosition.x, hiddenY, originalLocalPosition.z);
        Vector3 endPos = new Vector3(originalLocalPosition.x, targetY, originalLocalPosition.z);
        if (handTransform != null) handTransform.localPosition = startPos;

        while (elapse < riseDuration)
        {
            elapse += Time.deltaTime;
            float t = elapse / riseDuration;

            if (handTransform != null) handTransform.localPosition = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        if (handTransform != null) handTransform.localPosition = endPos;
    }

    private IEnumerator ShakeRoutine()
    {
        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            float t = elapsed / shakeDuration;

            float offsetX = Mathf.Sin(elapsed * shakeFrequency * Mathf.PI * 2f) * shakeIntensity;
            float offsetZ = Mathf.Cos(elapsed * shakeFrequency * Mathf.PI * 2.3f) * shakeIntensity * 0.6f;

            if (handTransform != null)
            {
                handTransform.localPosition = new Vector3(originalLocalPosition.x 
                    + offsetX, targetY, originalLocalPosition.z + offsetZ);
            }
            
            if ( handRend != null) handRend.GetPropertyBlock(mpb);
            if (mpb != null) mpb.SetFloat("_Amount", Mathf.Lerp(0f, 1f, t));
            if (handRend != null) handRend.SetPropertyBlock(mpb);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (handTransform != null)
        {
            handTransform.localPosition = new Vector3(originalLocalPosition.x, targetY, originalLocalPosition.z);
        }
    }

    private IEnumerator ReturnDownRutine()
    {
        float elapse = 0f;
        Vector3 startPos = handTransform != null ? handTransform.localPosition : Vector3.zero;
        Vector3 endPos = new Vector3(originalLocalPosition.x, hiddenY, originalLocalPosition.z);

        while (elapse < riseDuration)
        {
            elapse += Time.deltaTime;

            float t = elapse / riseDuration;
            if (handTransform != null) handTransform.localPosition = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        if (handTransform != null) handTransform.localPosition = endPos;
    }

    #endregion
}