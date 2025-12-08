using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class PauseController : MonoBehaviour, PlayerControlls.IUIActions
{
    [Header("Menu Panel References")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private SettingsPanel settingsPanel;
    [SerializeField] private GameObject firstSelectedButton;

    [Header("SFX / Music Sources")]
    [Tooltip("Fuente dedicada para reproducir la música o ambientación del menú de pausa.")]
    [SerializeField] private AudioSource pauseMusicSource;
    [Tooltip("Fuente dedicada para reproducir SFX (botones, clicks).")]
    [SerializeField] private AudioSource sfxSource;

    [Header("SFX Clips")]
    [SerializeField] private AudioClip openPauseClip;
    [SerializeField] private AudioClip clickButtonSFX;

    // Guardamos el estado previo de cada AudioSource que desactivamos
    private class AudioState
    {
        public bool enabled;
        public bool wasPlaying;
        public int timeSamples;
    }
    private Dictionary<AudioSource, AudioState> previousAudioStates = new Dictionary<AudioSource, AudioState>();

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

        // Seguridad: si no se asignó ninguna source, intentamos buscar alguna en el mismo GameObject
        if (pauseMusicSource == null)
        {
            pauseMusicSource = GetComponent<AudioSource>();
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

        // Si el componente se desactiva mientras está en pausa, restauramos los audio para evitar que queden desactivados permanentemente.
        if (isPaused)
        {
            Time.timeScale = previousTimeScale;
            RestoreAudioSources();
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

        // --- Desactivar todos los AudioSources no asignados a este script (guardando su estado) ---
        DisableOtherAudioSources();

        // --- Reproducir música de pausa sin duplicados ---
        if (openPauseClip != null)
        {
            if (pauseMusicSource != null)
            {
                if (pauseMusicSource.clip != openPauseClip)
                {
                    pauseMusicSource.clip = openPauseClip;
                    pauseMusicSource.loop = true; // opcional: repetir mientras esté en pausa
                }

                if (!pauseMusicSource.isPlaying)
                {
                    pauseMusicSource.Play();
                }
            }
        }

        // --- Desactivar controles de jugador ---
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

        // --- Click SFX ---
        PlayClickSFX();

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

        // --- Detener música de pausa (si está sonando) ---
        if (pauseMusicSource != null && pauseMusicSource.isPlaying)
        {
            pauseMusicSource.Stop();
            // pauseMusicSource.clip = null; // opcional
        }

        // --- Restaurar AudioSources que desactivamos al pausar ---
        RestoreAudioSources();

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

        PlayClickSFX();

        settingsPanel.OpenPanel();
        isSettingsOpen = true;
    }

    public void CloseSettings()
    {
        if (settingsPanel == null || !isSettingsOpen) return;

        PlayClickSFX();

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
        PlayClickSFX();

        Time.timeScale = 1f;
        StartCoroutine(FadeAndReloadScene(SceneManager.GetActiveScene().name));
    }

    public void LoadMainMenu()
    {
        PlayClickSFX();

        Time.timeScale = 1f;
        
        MerchantDialogHandler.ResetReputationState();

        StartCoroutine(FadeAndReloadScene("MainMenu"));
    }

    private IEnumerator FadeAndReloadScene(string sceneName)
    {
        isPaused = false;
        isSettingsOpen = false;

        // Antes de hacer fade / carga, nos aseguramos de restaurar los AudioSources.
        RestoreAudioSources();

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

    // Helper para reproducir SFX de click sin interferir con la música de pausa
    private void PlayClickSFX()
    {
        if (clickButtonSFX == null) return;

        if (sfxSource != null)
        {
            sfxSource.PlayOneShot(clickButtonSFX);
        }
        else if (pauseMusicSource != null)
        {
            // Fallback: si no hay sfxSource, intentamos reproducir con pauseMusicSource
            if (pauseMusicSource.clip == null || pauseMusicSource.clip != clickButtonSFX)
            {
                pauseMusicSource.PlayOneShot(clickButtonSFX);
            }
        }
        else
        {
            Debug.LogWarning("[PauseController] No hay AudioSource asignado para SFX. Asigna 'SFX Source' en el Inspector.");
        }
    }

    // --- NUEVO: desactivar AudioSources no asignados (guardando estado y si estaban reproduciendo) ---
    private void DisableOtherAudioSources()
    {
        previousAudioStates.Clear();

        // Incluir AudioSources inactivos también (requiere Unity 2020.1+)
        AudioSource[] allSources = FindObjectsOfType<AudioSource>(true);

        foreach (AudioSource src in allSources)
        {
            if (src == null) continue;

            // Excluir las fuentes asignadas a este script
            if (src == pauseMusicSource || src == sfxSource) continue;

            // Guardar estado previo y si estaba reproduciendo
            AudioState state = new AudioState();
            state.enabled = src.enabled;
            state.wasPlaying = src.isPlaying;
            state.timeSamples = 0;
            try
            {
                if (src.isPlaying && src.clip != null)
                {
                    // Guardamos la posición de reproducción
                    state.timeSamples = src.timeSamples;
                }
            }
            catch
            {
                // En caso de error al leer timeSamples (por ejemplo clip null), dejamos 0
                state.timeSamples = 0;
            }

            previousAudioStates[src] = state;

            // Desactivar el componente para silenciar (esto también detiene la reproducción)
            try
            {
                src.enabled = false;
            }
            catch
            {
                // ignorar si hubo un problema (objeto destruido, etc.)
            }
        }
    }

    // --- NUEVO: restaurar los AudioSources desactivados ---
    private void RestoreAudioSources()
    {
        if (previousAudioStates == null || previousAudioStates.Count == 0) return;

        // Hacemos una copia de las claves para evitar problemas si la colección cambia
        var keys = new List<AudioSource>(previousAudioStates.Keys);
        foreach (AudioSource src in keys)
        {
            if (src == null) continue;

            AudioState prev = previousAudioStates[src];

            try
            {
                // Restaurar el enabled tal como estaba
                src.enabled = prev.enabled;

                // Si antes estaba reproduciéndose, y ahora el componente y el GameObject están activos, reanudar
                if (prev.wasPlaying)
                {
                    if (src.enabled && src.gameObject.activeInHierarchy && src.clip != null)
                    {
                        // Restauramos la posición y reanudamos
                        try
                        {
                            src.timeSamples = prev.timeSamples;
                        }
                        catch
                        {
                            // algunos tipos de clips pueden no soportar timeSamples; en ese caso rompemos y solo Play()
                        }

                        src.Play();
                    }
                    else
                    {
                        // Si no es posible reanudar (gameObject inactivo), dejamos como estaba habilitado y no reproducimos.
                    }
                }
            }
            catch
            {
                // ignorar errores (objeto destruido, etc.)
            }
        }

        previousAudioStates.Clear();
    }

    public void OnPoint(InputAction.CallbackContext context) { }
    public void OnClick(InputAction.CallbackContext context) { }
    public void OnScrollWheel(InputAction.CallbackContext context) { }
    public void OnMiddleClick(InputAction.CallbackContext context) { }
    public void OnRightClick(InputAction.CallbackContext context) { }
    public void OnTrackedDevicePosition(InputAction.CallbackContext context) { }
    public void OnTrackedDeviceOrientation(InputAction.CallbackContext context) { }
}

//using UnityEngine;
//using UnityEngine.SceneManagement;
//using System.Collections;
//using UnityEngine.InputSystem;
//using UnityEngine.EventSystems;

//public class PauseController : MonoBehaviour, PlayerControlls.IUIActions
//{
//    [Header("Menu Panel References")]
//    [SerializeField] private GameObject pausePanel;
//    [SerializeField] private SettingsPanel settingsPanel;
//    [SerializeField] private GameObject firstSelectedButton;

//    [Header("SFX Configuration")]
//    [SerializeField] private AudioSource audioSource;
//    [SerializeField] private AudioClip openPauseClip;
//    [SerializeField] private AudioClip clickButtonSFX;

//    private static bool isPaused = false;
//    private bool isSettingsOpen = false; 

//    private PlayerControlls playerControls;
//    private bool canTogglePause = true;
//    private const float ToggleCooldown = 0.15f;

//    [SerializeField] private PlayerStatsManager statsManager;
//    private float previousTimeScale;
//    public static bool IsGamePaused => isPaused;
//    public static PauseController Instance { get; private set; }

//    void Awake()
//    {
//        if (Instance != null && Instance != this)
//        {
//            Destroy(gameObject);
//            return;
//        }
//        Instance = this;

//        playerControls = new PlayerControlls();
//        playerControls.UI.SetCallbacks(this);

//        if (statsManager == null)
//        {
//            statsManager = FindAnyObjectByType<PlayerStatsManager>();
//        }

//        if (pausePanel != null) pausePanel.SetActive(false);
//    }

//    void OnEnable()
//    {
//        playerControls?.UI.Enable();

//        if (Gamepad.current != null)
//        {
//            if (firstSelectedButton != null)
//            {
//                if (EventSystem.current != null)
//                {
//                    EventSystem.current.SetSelectedGameObject(null);
//                    EventSystem.current.SetSelectedGameObject(firstSelectedButton);
//                }
//            }
//            else
//            {
//                Debug.LogWarning("[ControlMenu] No se pudo establecer el foco inicial. Asigna 'First Selected Button' en el Inspector.");
//            }
//        }
//        else
//        {
//            Debug.LogWarning("[ControlMenu] No se pudo establecer el foco inicial. Asigna 'First Selected Button' en el Inspector.");
//        }
//    }

//    void OnDisable()
//    {
//        playerControls?.UI.Disable();

//        if (isPaused)
//        {
//            Time.timeScale = previousTimeScale;
//        }
//    }

//    void OnDestroy()
//    {
//        playerControls?.Dispose();
//    }

//    public void OnCancel(InputAction.CallbackContext context)
//    {
//        if (context.performed && canTogglePause)
//        {
//            if (isSettingsOpen)
//            {
//                CloseSettings();
//            }
//            else if (isPaused)
//            {
//                ResumeGame();
//            }
//            else
//            {
//                PauseGame();
//            }
//        }
//    }

//    public void OnNavigate(InputAction.CallbackContext context) { }
//    public void OnSubmit(InputAction.CallbackContext context) { }

//    public void PauseGame()
//    {
//        if (isPaused) return;

//        if (audioSource != null && openPauseClip != null)
//        {
//            audioSource.PlayOneShot(openPauseClip);
//        }

//        if (playerControls != null)
//        {
//            playerControls.Movement.Disable();
//            playerControls.Combat.Disable();
//        }

//        previousTimeScale = Time.timeScale;
//        Time.timeScale = 0f;
//        isPaused = true;
//        StartCoroutine(PauseCooldown());

//        if (pausePanel != null)
//        {
//            pausePanel.SetActive(true);

//            if (Gamepad.current != null) 
//            {
//                SetFocus(firstSelectedButton, "PauseController");
//            }
//            else
//            {
//                EventSystem.current?.SetSelectedGameObject(null);
//            }
//        }
//    }

//    public void ResumeGame()
//    {
//        if (!isPaused) return;

//        if (audioSource != null && clickButtonSFX != null)
//        {
//            audioSource.PlayOneShot(clickButtonSFX);
//        }

//        if (isSettingsOpen) return;

//        if (EventSystem.current != null)
//        {
//            EventSystem.current.SetSelectedGameObject(null);
//        }

//        if (Gamepad.current != null)
//        {
//            InputSystem.ResetDevice(Gamepad.current);
//        }
//        if (Keyboard.current != null)
//        {
//            InputSystem.ResetDevice(Keyboard.current);
//        }
//        if (Mouse.current != null)
//        {
//            InputSystem.ResetDevice(Mouse.current);
//        }

//        Time.timeScale = previousTimeScale;
//        isPaused = false;
//        StartCoroutine(PauseCooldown());

//        if (playerControls != null)
//        {
//            playerControls.Movement.Enable();
//            playerControls.Combat.Enable();
//        }

//        if (pausePanel != null)
//        {
//            pausePanel.SetActive(false);
//        }
//    }

//    public void OpenSettings()
//    {
//        if (settingsPanel == null)
//        {
//            Debug.LogError("[PauseController] No se pudo abrir Settings: 'Settings Panel' no está asignado.");
//            return;
//        }

//        if (audioSource != null && clickButtonSFX != null)
//        {
//            audioSource.PlayOneShot(clickButtonSFX);
//        }

//        settingsPanel.OpenPanel();
//        isSettingsOpen = true;
//    }

//    public void CloseSettings()
//    {
//        if (settingsPanel == null || !isSettingsOpen) return;

//        if (audioSource != null && clickButtonSFX != null)
//        {
//            audioSource.PlayOneShot(clickButtonSFX);
//        }

//        settingsPanel.ClosePanel();
//        isSettingsOpen = false;

//        if (Gamepad.current != null)
//        {
//            SetFocus(firstSelectedButton, "Settings Close");
//        }
//    }

//    private IEnumerator PauseCooldown()
//    {
//        canTogglePause = false;
//        yield return new WaitForSecondsRealtime(ToggleCooldown);
//        canTogglePause = true;
//    }

//    private void SetFocus(GameObject focusObject, string context)
//    {
//        if (focusObject == null) return;

//        if (EventSystem.current != null)
//        {
//            EventSystem.current.SetSelectedGameObject(null);
//            EventSystem.current.SetSelectedGameObject(focusObject);
//        }
//        else
//        {
//            Debug.LogWarning($"[{context} - PauseController] EventSystem.current es nulo. No se pudo establecer el foco.");
//        }
//    }

//    public void ResetGame()
//    {
//        if (audioSource != null && clickButtonSFX != null)
//        {
//            audioSource.PlayOneShot(clickButtonSFX);
//        }

//        Time.timeScale = 1f;
//        StartCoroutine(FadeAndReloadScene(SceneManager.GetActiveScene().name));
//    }

//    public void LoadMainMenu()
//    {
//        if (audioSource != null && clickButtonSFX != null)
//        {
//            audioSource.PlayOneShot(clickButtonSFX);
//        }

//        Time.timeScale = 1f;
//        StartCoroutine(FadeAndReloadScene("MainMenu"));
//    }

//    private IEnumerator FadeAndReloadScene(string sceneName)
//    {
//        isPaused = false;
//        isSettingsOpen = false; 

//        if (FadeController.Instance != null && FadeController.Instance.fade != null)
//        {
//            if (pausePanel != null) pausePanel.SetActive(false);
//            if (settingsPanel != null) settingsPanel.gameObject.SetActive(false);

//            yield return StartCoroutine(FadeController.Instance.FadeOut());
//        }

//        Time.timeScale = 1f;

//        if (statsManager != null)
//        {
//            statsManager.ResetRunStatsToDefaults();
//            statsManager.ResetStatsOnDeath();

//            float maxHealth = statsManager.GetStat(StatType.MaxHealth);
//            if (statsManager._currentStatSO != null)
//            {
//                statsManager._currentStatSO.currentHealth = maxHealth;
//            }
//        }

//        InventoryManager inventory = FindAnyObjectByType<InventoryManager>();
//        if (inventory != null)
//        {
//            inventory.ClearInventory();
//        }

//        SceneManager.LoadScene(sceneName);
//    }

//    public void OnPoint(InputAction.CallbackContext context) { }
//    public void OnClick(InputAction.CallbackContext context) { }
//    public void OnScrollWheel(InputAction.CallbackContext context) { }
//    public void OnMiddleClick(InputAction.CallbackContext context) { }
//    public void OnRightClick(InputAction.CallbackContext context) { }
//    public void OnTrackedDevicePosition(InputAction.CallbackContext context) { }
//    public void OnTrackedDeviceOrientation(InputAction.CallbackContext context) { }
//}