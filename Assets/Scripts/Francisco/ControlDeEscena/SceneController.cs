using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using System.Collections;

public class SceneController : MonoBehaviour
{
    public static SceneController Instance;

    [SerializeField] private KeyCode _inputKey;

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
        yield return FadeController.Instance.FadeOut();

        SceneManager.LoadScene(sceneName);
    }
}