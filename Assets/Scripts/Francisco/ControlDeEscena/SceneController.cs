using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using System.Collections;

public class SceneController : MonoBehaviour
{
    public static SceneController Instance;

    [SerializeField] private KeyCode _inputKey;

    [Header("Scene Navigation Keys")] 
    [SerializeField] private KeyCode _inputKeyNext;
    [SerializeField] private KeyCode _inputKeyPrevious;

    [Header("Scene Names")] 
    [SerializeField] private string _nextSceneName;
    [SerializeField] private string _previousSceneName;

    public UnityEvent OnKeyPressed;

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

        if (Input.GetKeyDown(_inputKeyNext))
        {
            LoadNextScene();
        }

        if (Input.GetKeyDown(_inputKeyPrevious))
        {
            LoadPreviousScene();
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

    public void LoadNextScene()
    {
        LoadSceneByName(_nextSceneName);
    }

    public void LoadPreviousScene()
    {
        LoadSceneByName(_previousSceneName);
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