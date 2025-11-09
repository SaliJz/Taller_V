using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class PauseController : MonoBehaviour, PlayerControlls.IUIActions
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject firstSelectedButton;

    private PlayerControlls playerControls;

    [SerializeField] private PlayerStatsManager statsManager;
    private float previousTimeScale;
    private bool isPaused = false;

    private bool canTogglePause = true;
    private const float ToggleCooldown = 0.15f;

    public static PauseController Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        playerControls = new PlayerControlls();
        playerControls.UI.SetCallbacks(this);

        statsManager = FindAnyObjectByType<PlayerStatsManager>();

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
    }

    void OnEnable()
    {
        playerControls?.UI.Enable();
    }

    void OnDisable()
    {
        playerControls?.UI.Disable();

        if (isPaused)
        {
            Time.timeScale = previousTimeScale;
        }
    }

    void OnDestroy()
    {
        playerControls?.Dispose();
    }

    public void OnCancel(InputAction.CallbackContext context)
    {
        if (context.performed && canTogglePause)
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

    public void OnNavigate(InputAction.CallbackContext context) { }
    public void OnSubmit(InputAction.CallbackContext context) { }
    public void OnPoint(InputAction.CallbackContext context) { }
    public void OnClick(InputAction.CallbackContext context) { }
    public void OnScrollWheel(InputAction.CallbackContext context) { }
    public void OnMiddleClick(InputAction.CallbackContext context) { }
    public void OnRightClick(InputAction.CallbackContext context) { }
    public void OnTrackedDevicePosition(InputAction.CallbackContext context) { }
    public void OnTrackedDeviceOrientation(InputAction.CallbackContext context) { }

    public void PauseGame()
    {
        if (isPaused) return;

        if (playerControls != null)
        {
            playerControls.Movement.Disable();
            playerControls.Combat.Disable();
            playerControls.UI.Enable();
        }

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        isPaused = true;
        StartCoroutine(PauseCooldown());

        if (pausePanel != null)
        {
            pausePanel.SetActive(true);

            if (firstSelectedButton != null)
            {
                EventSystem.current.SetSelectedGameObject(firstSelectedButton);
            }
            else
            {
                Debug.LogWarning("[PauseController] No se pudo establecer el foco inicial. Asigna 'First Selected Button' en el Inspector.");
            }
        }
    }

    public void ResumeGame()
    {
        if (!isPaused) return;

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        if (Gamepad.current != null)
        {
            InputSystem.ResetDevice(Gamepad.current);
        }
        if (Keyboard.current != null)
        {
            InputSystem.ResetDevice(Keyboard.current);
        }
        if (Mouse.current != null)
        {
            InputSystem.ResetDevice(Mouse.current);
        }

        Time.timeScale = previousTimeScale;
        isPaused = false;
        StartCoroutine(PauseCooldown());

        if (playerControls != null)
        {
            playerControls.Movement.Enable();
            playerControls.Combat.Enable();
        }

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
    }

    private IEnumerator PauseCooldown()
    {
        canTogglePause = false;
        yield return new WaitForSecondsRealtime(ToggleCooldown);
        canTogglePause = true;
    }

    public void ResetGame()
    {
        Time.timeScale = 1f;
        StartCoroutine(FadeAndReloadScene(SceneManager.GetActiveScene().name));
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        StartCoroutine(FadeAndReloadScene("MainMenu"));
    }

    private IEnumerator FadeAndReloadScene(string sceneName)
    {
        isPaused = false;

        if (FadeController.Instance != null && FadeController.Instance.fade != null)
        {
            yield return StartCoroutine(FadeController.Instance.FadeOut());
        }

        Time.timeScale = 1f;

        if (statsManager != null) statsManager.ResetRunStatsToDefaults();
        if (statsManager != null) statsManager.ResetStatsOnDeath();

        InventoryManager inventory = FindAnyObjectByType<InventoryManager>();

        if (inventory != null)
        {
            inventory.ClearInventory();
        }

        float maxHealth = statsManager.GetStat(StatType.MaxHealth);
        if (statsManager._currentStatSO != null)
        {
            statsManager._currentStatSO.currentHealth = maxHealth;
        }

        SceneManager.LoadScene(sceneName);
    }
}