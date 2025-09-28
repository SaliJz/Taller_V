using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic; 

[System.Serializable]
public struct SceneTransition
{
    public KeyCode inputKey;
    public string targetSceneName;
}

public class SceneController : MonoBehaviour
{
    public static SceneController Instance;

    [Header("Input General")]
    [SerializeField] private KeyCode _inputKey;

    [Header("Transiciones de Escena")]
    [SerializeField] private List<SceneTransition> _sceneTransitions;
    [SerializeField] private UnityEvent OnKeyPressed;

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

    private void Update()
    {
        if (Input.GetKeyDown(_inputKey))
        {
            OnKeyPressed.Invoke();
        }

        CheckSceneTransitions();
    }

    private void CheckSceneTransitions()
    {
        if (_sceneTransitions == null) return;

        foreach (var transition in _sceneTransitions)
        {
            if (Input.GetKeyDown(transition.inputKey))
            {
                LoadSceneByName(transition.targetSceneName);
            }
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

    public void ReloadCurrentScene()
    {
        StartCoroutine(LoadSceneWithFade(SceneManager.GetActiveScene().name));
    }

    public void QuitGame()
    {
        Application.Quit();
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