using UnityEngine;
using System.Collections;
using System;

public class FadeController : MonoBehaviour
{
    public static FadeController Instance;
    public CanvasGroup fade;
    public float fadeDuration = 1.0f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public IEnumerator FadeOut(Action onStart = null, Action<float> onUpdate = null, Action onComplete = null)
    {
        onStart?.Invoke();

        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
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

        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            fade.alpha = alpha;
            onUpdate?.Invoke(alpha);
            yield return null;
        }

        fade.alpha = 0f;
        onComplete?.Invoke();
    }
}