using UnityEngine;
using UnityEngine.SceneManagement;

public class ReelAnimCtrl : MonoBehaviour
{
    #region Enums And Structs

    public enum Cameras { right, left }

    #endregion

    #region Inspector - References

    [Header("Core References")]
    [SerializeField] private Animator anim;
    [SerializeField] private SkinnedMeshRenderer mesh;
    [SerializeField] private EscudoController shieldCtrl;

    #endregion

    #region Inspector - Cameras Aim

    [Header("Cameras Aim")]
    [SerializeField] private LookAt leftLookAt;
    [SerializeField] private LookAt rightLookAt;

    #endregion

    #region Inspector - Anticipation Shake

    [Header("Anticipation Shake")]
    [SerializeField] private float shakeIntensity = 0.2f;
    [SerializeField] private float shakeFrequency = 2f;

    #endregion

    #region Internal State

    private Coroutine shakeCoroutine;
    private Vector3 originalLocalPosition;

    #endregion

    #region Public Properties And Events

    public bool walking; //Conectar con el bool 

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        originalLocalPosition = transform.localPosition;
    }

    private void Update()
    {
        anim.SetBool("isWalking", walking);

        if (leftLookAt.Target != null || rightLookAt.Target != null)
        {
            mesh.SetBlendShapeWeight(0, 100);
        }
        else
        {
            mesh.SetBlendShapeWeight(0, 0);
        }

#if UNITY_EDITOR
        if (SceneManager.GetActiveScene().name == "AndreiNew") TestInputs();
#endif
    }

    #endregion

    #region Camera Targeting

    public void SetTarget(Cameras cam, Transform target)
    {
        switch (cam)
        {
            case Cameras.right:
                {
                    rightLookAt.Target = target;
                    break;
                }
            case Cameras.left:
                {
                    leftLookAt.Target = target;
                    break;
                }
        }
    }

    public Transform GetCameraTransform(Cameras cam)
    {
        return cam == Cameras.right ? rightLookAt.transform : leftLookAt.transform;
    }

    public void ClearTarget(Cameras cam)
    {
        switch (cam)
        {
            case Cameras.right:
                {
                    rightLookAt.Target = null;
                    break;
                }
            case Cameras.left:
                {
                    leftLookAt.Target = null;
                    break;
                }
        }
    }

    #endregion

    #region Animation Control

    public void PlayAttack()
    {
        anim.SetTrigger("Attack");
    }

    public void PlayDeath()
    {
        anim.SetTrigger("Death");
    }

    public void PlayInvulnerabilityVFX()
    {
        if (shieldCtrl != null) shieldCtrl.Escudo = true;
    }

    //public void PlayDamage()
    //{
    //    if (anim != null) anim.Play("Damage", 0, 0);
    //}

    public void PauseAnimation()
    {
        if (anim != null) anim.speed = 0f;
    }

    public void ResumeAnimation()
    {
        if (anim != null) anim.speed = 1f;
    }

    #endregion

    #region Anticipation And Shake

    public void PlayAnticipationShake(float duration)
    {
        StopAnticipationShake();
        shakeCoroutine = StartCoroutine(ShakeRoutine(duration));
    }

    public void StopAnticipationShake()
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
        }
        transform.localPosition = originalLocalPosition;
    }

    private System.Collections.IEnumerator ShakeRoutine(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float offsetX = Mathf.Sin(elapsed * shakeFrequency * Mathf.PI * 2f) * shakeIntensity;
            float offsetZ = Mathf.Cos(elapsed * shakeFrequency * Mathf.PI * 2.3f) * shakeIntensity * 0.6f;
            transform.localPosition = originalLocalPosition + new Vector3(offsetX, 0f, offsetZ);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localPosition = originalLocalPosition;
        shakeCoroutine = null;
    }

    #endregion

    #region Debug And Testing

    private void TestInputs()
    {
        if (Input.GetKeyDown(KeyCode.J)) PlayAttack();
        if (Input.GetKeyDown(KeyCode.H)) PlayDeath();
        if (Input.GetKeyDown(KeyCode.O)) anim.Play("Idle");
        if (Input.GetKeyDown(KeyCode.P)) shieldCtrl.Escudo = true;
    }

    #endregion
}