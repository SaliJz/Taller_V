using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

public class FadeController : MonoBehaviour
{
    #region Singleton
    public static FadeController Instance;
    #endregion

    #region Enums
    public enum FadeMode
    {
        CanvasGroup,
        Material
    }
    #endregion

    #region Inspector Fields
    [Header("References")]
    public CanvasGroup fade;
    public Image fadeImage;

    [Header("Configuration")]
    public FadeMode fadeMode = FadeMode.CanvasGroup;
    public float fadeDuration = 1f;

    [Header("Global Settings")]
    public Color globalFadeColor = Color.black;
    #endregion

    #region Properties
    public bool IsFading { get; private set; }

    private Material activeMaterial;
    private static readonly int ColorProp = Shader.PropertyToID("_Color");

    private static readonly int ProgressProp =
        Shader.PropertyToID("_Progress");
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (fade == null)
        {
            fade = GetComponent<CanvasGroup>();
        }

        InitializeMode();
    }

    private void Start()
    {
        SetupInitialState();

        StartCoroutine(FadeIn());
    }
    #endregion

    #region Initialization
    private void InitializeMode()
    {
        switch (fadeMode)
        {
            case FadeMode.Material:

                if (fadeImage != null)
                {
                    activeMaterial = fadeImage.material;
                }

                break;

            case FadeMode.CanvasGroup:

                activeMaterial = null;

                break;
        }
    }

    private void SetupInitialState()
    {
        ApplyGlobalColor();

        switch (fadeMode)
        {
            case FadeMode.Material:

                if (activeMaterial != null)
                {
                    activeMaterial.SetFloat(ProgressProp, 0f);
                }

                if (fade != null)
                {
                    fade.alpha = 0f;
                }

                break;

            case FadeMode.CanvasGroup:

                if (activeMaterial != null)
                {
                    activeMaterial.SetFloat(ProgressProp, 1f);
                }

                SetAlpha(1f);

                break;
        }
    }
    #endregion

    #region Public API
    public IEnumerator FadeOut(
        Action onStart = null,
        Action<float> onUpdate = null,
        Action onComplete = null,
        Color? fadeColor = null)
    {
        IsFading = true;

        onStart?.Invoke();

        if (fadeColor.HasValue)
        {
            globalFadeColor = fadeColor.Value;
        }

        ApplyGlobalColor();

        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;

            float t = timer / fadeDuration;

            switch (fadeMode)
            {
                case FadeMode.Material:

                    if (fade != null)
                    {
                        fade.alpha = 0f;
                    }

                    if (activeMaterial != null)
                    {
                        activeMaterial.SetFloat(
                            ProgressProp,
                            Mathf.Lerp(1f, 0f, t)
                        );
                    }

                    break;

                case FadeMode.CanvasGroup:

                    SetAlpha(Mathf.Lerp(0f, 1f, t));

                    break;
            }

            onUpdate?.Invoke(t);

            yield return null;
        }

        FinalizeState(true);

        onComplete?.Invoke();

        IsFading = false;
    }

    public IEnumerator FadeIn(
        Action onStart = null,
        Action<float> onUpdate = null,
        Action onComplete = null)
    {
        IsFading = true;

        onStart?.Invoke();

        ApplyGlobalColor();

        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;

            float t = timer / fadeDuration;

            switch (fadeMode)
            {
                case FadeMode.Material:

                    if (fade != null)
                    {
                        fade.alpha = 0f;
                    }

                    if (activeMaterial != null)
                    {
                        activeMaterial.SetFloat(
                            ProgressProp,
                            Mathf.Lerp(0f, 1f, t)
                        );
                    }

                    break;

                case FadeMode.CanvasGroup:

                    SetAlpha(Mathf.Lerp(1f, 0f, t));

                    break;
            }

            onUpdate?.Invoke(t);

            yield return null;
        }

        FinalizeState(false);

        onComplete?.Invoke();

        IsFading = false;
    }
    #endregion

    #region Helpers
    private void ApplyGlobalColor()
    {
        if (fadeImage != null)
        {
            Color c = globalFadeColor;
            c.a = fadeImage.color.a;

            fadeImage.color = c;
        }

        if (activeMaterial != null &&
            activeMaterial.HasProperty(ColorProp))
        {
            activeMaterial.SetColor(
                ColorProp,
                globalFadeColor
            );
        }
    }

    private void SetAlpha(float alpha)
    {
        if (fadeMode != FadeMode.CanvasGroup)
            return;

        if (fade != null)
        {
            fade.alpha = alpha;
        }
        else if (fadeImage != null)
        {
            Color c = fadeImage.color;
            c.a = alpha;
            fadeImage.color = c;
        }
    }

    private void FinalizeState(bool isFullDark)
    {
        switch (fadeMode)
        {
            case FadeMode.Material:

                if (activeMaterial != null)
                {
                    activeMaterial.SetFloat(
                        ProgressProp,
                        isFullDark ? 0f : 1f
                    );
                }

                break;

            case FadeMode.CanvasGroup:

                SetAlpha(isFullDark ? 1f : 0f);

                break;
        }
    }
    #endregion
}