using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneController : MonoBehaviour
{
    public static SceneController Instance;

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