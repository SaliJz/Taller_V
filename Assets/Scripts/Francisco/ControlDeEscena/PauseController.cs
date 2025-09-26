using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class PauseController : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;

    private float previousTimeScale;
    private bool isPaused = false;

    void Start()
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    public void PauseGame()
    {
        if (isPaused) return;

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        isPaused = true;

        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
        }
    }

    public void ResumeGame()
    {
        if (!isPaused) return;

        Time.timeScale = previousTimeScale;
        isPaused = false;

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
    }

    public void ResetGame()
    {
        StartCoroutine(FadeAndReloadScene(SceneManager.GetActiveScene().name));
    }

    public void LoadMainMenu()
    {
        StartCoroutine(FadeAndReloadScene("MainMenu"));
    }

    private IEnumerator FadeAndReloadScene(string sceneName)
    {
        if (FadeController.Instance != null && FadeController.Instance.fade != null)
        {
            yield return StartCoroutine(FadeController.Instance.FadeOut());
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }
}