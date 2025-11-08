using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class InteractionEvents : MonoBehaviour
{
    [Header("Eventos")]
    [SerializeField] private UnityEvent OnObjectEntered;
    [SerializeField] private UnityEvent OnObjectExited;

    [SerializeField] private string tagName;

    private void OnTriggerEnter(Collider other)
    {
        if (tagName == "")
        {
            OnObjectEntered?.Invoke();
        }
        else
        {
            if (other.CompareTag(tagName)) OnObjectEntered?.Invoke();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (tagName == "")
        {
            OnObjectExited?.Invoke();
        }
        else
        {
            if (other.CompareTag(tagName)) OnObjectExited?.Invoke();
        }
    }

    public void LoadSceneByName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("The scene name cannot be null or empty.");
            return;
        }

        StartCoroutine(LoadSceneWithFade(sceneName));
    }

    private IEnumerator LoadSceneWithFade(string sceneName)
    {
        if (FadeController.Instance != null)
        {
            yield return FadeController.Instance.FadeOut();
        }
        else
        {
            Debug.LogWarning("FadeController.Instance is missing. Loading scene without fade.");
        }

        SceneManager.LoadScene(sceneName);
    }
}