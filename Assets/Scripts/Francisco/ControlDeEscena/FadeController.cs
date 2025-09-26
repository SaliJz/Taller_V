using UnityEngine;
using System.Collections;
using System;
using UnityEngine.UI; 

public class FadeController : MonoBehaviour
{
    public static FadeController Instance;
    [Header("References")]
    public CanvasGroup fade;
    public Image fadeImage; 

    [Header("Configuration")]
    public float fadeDuration = 1.0f;
    public Color defaultFadeColor = Color.black; 

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); 
        }
        else
        {
            Destroy(gameObject);
        }

        if (fadeImage == null && fade != null)
        {
            fadeImage = fade.GetComponent<Image>();
        }
    }

    private void Start()
    {
        if (fadeImage != null)
        {
            fadeImage.color = defaultFadeColor;
        }
        StartCoroutine(FadeIn());
    }

    public IEnumerator FadeOut(Action onStart = null, Action<float> onUpdate = null, Action onComplete = null, Color? fadeColor = null)
    {
        onStart?.Invoke();

        Color targetColor = fadeColor ?? defaultFadeColor;
        if (fadeImage != null)
        {
            fadeImage.color = targetColor; 
        }

        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(0f, 1f, timer / fadeDuration);
            fade.alpha = alpha;
            onUpdate?.Invoke(alpha);
            yield return null;
        }

        fade.alpha = 1f;
        onComplete?.Invoke();
    }

    public IEnumerator FadeIn(Action onStart = null, Action<float> onUpdate = null, Action onComplete = null)
    {
        onStart?.Invoke();

        if (fadeImage != null)
        {
            fadeImage.color = defaultFadeColor; 
        }

        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            fade.alpha = alpha;
            onUpdate?.Invoke(alpha);
            yield return null;
        }

        fade.alpha = 0f;
        onComplete?.Invoke();
    }
}