using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

public class BossIntroDirector : MonoBehaviour
{
    #region Inspector

    [Header("Sequence and Control")]
    [SerializeField] private SequenceTransition introSequence;
    [SerializeField] private float cameraBlendDuration = 1.5f;
    [SerializeField] private float uiAnimationDuration = 3.0f;

    [Header("Boss References")]
    [SerializeField] private GameObject bossGameObject;

    [Header("User Interface")]
    [SerializeField] private GameObject bossIntroCanvas;

    [Header("Shake Settings")]
    [SerializeField] private float shakeDelayAfterUiActive = 0.5f;

    #endregion

    #region Private State

    private bool isIntroRunning = false;
    private CinemachineCamera activeCinemachineCamera;
    private Transform originalCameraTarget;
    private Camera canvasCamera;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        canvasCamera = FindNonMainCamera();
    }

    #endregion

    #region Public API

    public IEnumerator BossIntroRoutine(Transform playerTransform)
    {
        if (isIntroRunning) yield break;
        isIntroRunning = true;

        if (introSequence != null)
        {
            yield return StartCoroutine(introSequence.ExecuteSequence(playerTransform));

            while (introSequence.IsSequenceRunning)
            {
                yield return null;
            }

            Animator playerAnimator = playerTransform.GetComponentInChildren<Animator>();
            if (playerAnimator != null)
            {
                playerAnimator.SetFloat("Speed", 0f);
                playerAnimator.Play("Idle");
            }
        }

        FindActiveCinemachineCamera();
        DisableBossLogicScripts();

        if (activeCinemachineCamera != null && bossGameObject != null)
        {
            originalCameraTarget = activeCinemachineCamera.Target.TrackingTarget;
            activeCinemachineCamera.Target.TrackingTarget = bossGameObject.transform;
        }

        yield return new WaitForSeconds(cameraBlendDuration);

        if (bossIntroCanvas != null)
        {
            if (canvasCamera == null) canvasCamera = FindNonMainCamera();

            Canvas canvas = bossIntroCanvas.GetComponent<Canvas>();
            if (canvas != null && canvasCamera != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = canvasCamera;
            }

            bossIntroCanvas.SetActive(true);

            Animator uiAnimator = bossIntroCanvas.GetComponent<Animator>();
            if (uiAnimator != null)
            {
                uiAnimator.updateMode = AnimatorUpdateMode.Normal;
            }
        }

        StartCoroutine(ExecuteDelayedShakeRoutine());

        yield return new WaitForSeconds(uiAnimationDuration);

        if (bossIntroCanvas != null)
        {
            bossIntroCanvas.SetActive(false);
        }

        if (activeCinemachineCamera != null && originalCameraTarget != null)
        {
            activeCinemachineCamera.Target.TrackingTarget = originalCameraTarget;
            yield return new WaitForSeconds(cameraBlendDuration);
        }

        EnableBossLogicScripts();

        if (introSequence != null)
        {
            introSequence.RestoreControl();
        }

        isIntroRunning = false;
    }

    #endregion

    #region Helper Methods

    private IEnumerator ExecuteDelayedShakeRoutine()
    {
        yield return new WaitForSeconds(shakeDelayAfterUiActive);

        CinemachineImpulseSource impulseSource = GetComponent<CinemachineImpulseSource>();
        if (impulseSource != null)
        {
            impulseSource.GenerateImpulse();
        }
    }

    private Camera FindNonMainCamera()
    {
        Camera[] allCameras = Camera.allCameras;
        foreach (Camera cam in allCameras)
        {
            if (!cam.CompareTag("MainCamera"))
                return cam;
        }
        return null;
    }

    private void FindActiveCinemachineCamera()
    {
        CinemachineBrain brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;

        if (brain != null && brain.ActiveVirtualCamera != null)
        {
            activeCinemachineCamera = brain.ActiveVirtualCamera as CinemachineCamera;
        }
        else
        {
            activeCinemachineCamera = Object.FindFirstObjectByType<CinemachineCamera>();
        }
    }

    private void DisableBossLogicScripts()
    {
        if (bossGameObject == null) return;

        MonoBehaviour[] scripts = bossGameObject.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts)
        {
            if (script == null || script == this || script.GetType().Namespace?.StartsWith("UnityEngine") == true)
                continue;

            script.enabled = false;
        }
    }

    private void EnableBossLogicScripts()
    {
        if (bossGameObject == null) return;

        MonoBehaviour[] scripts = bossGameObject.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts)
        {
            if (script == null || script == this || script.GetType().Namespace?.StartsWith("UnityEngine") == true)
                continue;

            script.enabled = true;
        }
    }

    #endregion
}