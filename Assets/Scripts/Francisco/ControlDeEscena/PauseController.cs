using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class PauseController : MonoBehaviour, PlayerControlls.IUIActions
{
    [Header("Menu Panel References")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private SettingsPanel settingsPanel;
    [SerializeField] private GameObject firstSelectedButton; 

    private static bool isPaused = false;
    private bool isSettingsOpen = false; 

    private PlayerControlls playerControls;
    private bool canTogglePause = true;
    private const float ToggleCooldown = 0.15f;

    [SerializeField] private PlayerStatsManager statsManager;
    private float previousTimeScale;
    public static bool IsGamePaused => isPaused;
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

        if (statsManager == null)
        {
            statsManager = FindAnyObjectByType<PlayerStatsManager>();
        }

        if (pausePanel != null) pausePanel.SetActive(false);
    }

    void OnEnable()
    {
        playerControls?.UI.Enable();

        if (Gamepad.current != null)
        {
            if (firstSelectedButton != null)
            {
                if (EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                    EventSystem.current.SetSelectedGameObject(firstSelectedButton);
                }
            }
            else
            {
                Debug.LogWarning("[ControlMenu] No se pudo establecer el foco inicial. Asigna 'First Selected Button' en el Inspector.");
            }
        }
        else
        {
            Debug.LogWarning("[ControlMenu] No se pudo establecer el foco inicial. Asigna 'First Selected Button' en el Inspector.");
        }
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
            if (isSettingsOpen)
            {
                CloseSettings();
            }
            else if (isPaused)
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

    public void PauseGame()
    {
        if (isPaused) return;

        if (playerControls != null)
        {
            playerControls.Movement.Disable();
            playerControls.Combat.Disable();
        }

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        isPaused = true;
        StartCoroutine(PauseCooldown());

        if (pausePanel != null)
        {
            pausePanel.SetActive(true);

            if (Gamepad.current != null) 
            {
                SetFocus(firstSelectedButton, "PauseController");
            }
            else
            {
                EventSystem.current?.SetSelectedGameObject(null);
            }
        }
    }

    public void ResumeGame()
    {
        if (!isPaused) return;

        if (isSettingsOpen) return;

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

    public void OpenSettings()
    {
        if (settingsPanel == null)
        {
            Debug.LogError("[PauseController] No se pudo abrir Settings: 'Settings Panel' no está asignado.");
            return;
        }

        settingsPanel.OpenPanel();
        isSettingsOpen = true;
    }

    public void CloseSettings()
    {
        if (settingsPanel == null || !isSettingsOpen) return;

        settingsPanel.ClosePanel();
        isSettingsOpen = false;

        if (Gamepad.current != null)
        {
            SetFocus(firstSelectedButton, "Settings Close");
        }
    }

    private IEnumerator PauseCooldown()
    {
        canTogglePause = false;
        yield return new WaitForSecondsRealtime(ToggleCooldown);
        canTogglePause = true;
    }

    private void SetFocus(GameObject focusObject, string context)
    {
        if (focusObject == null) return;

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(focusObject);
        }
        else
        {
            Debug.LogWarning($"[{context} - PauseController] EventSystem.current es nulo. No se pudo establecer el foco.");
        }
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
        isSettingsOpen = false; 

        if (FadeController.Instance != null && FadeController.Instance.fade != null)
        {
            if (pausePanel != null) pausePanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.gameObject.SetActive(false);

            yield return StartCoroutine(FadeController.Instance.FadeOut());
        }

        Time.timeScale = 1f;

        if (statsManager != null)
        {
            statsManager.ResetRunStatsToDefaults();
            statsManager.ResetStatsOnDeath();

            float maxHealth = statsManager.GetStat(StatType.MaxHealth);
            if (statsManager._currentStatSO != null)
            {
                statsManager._currentStatSO.currentHealth = maxHealth;
            }
        }

        InventoryManager inventory = FindAnyObjectByType<InventoryManager>();
        if (inventory != null)
        {
            inventory.ClearInventory();
        }

        SceneManager.LoadScene(sceneName);
    }

    public void OnPoint(InputAction.CallbackContext context) { }
    public void OnClick(InputAction.CallbackContext context) { }
    public void OnScrollWheel(InputAction.CallbackContext context) { }
    public void OnMiddleClick(InputAction.CallbackContext context) { }
    public void OnRightClick(InputAction.CallbackContext context) { }
    public void OnTrackedDevicePosition(InputAction.CallbackContext context) { }
    public void OnTrackedDeviceOrientation(InputAction.CallbackContext context) { }
}