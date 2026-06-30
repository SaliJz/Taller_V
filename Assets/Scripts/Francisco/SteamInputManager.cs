using UnityEngine;
using Steamworks;
using UnityEngine.SceneManagement;

#region Configuracion
public class SteamInputManager : MonoBehaviour
{
    private static SteamInputManager instance;
    public static SteamInputManager Instance => instance;

    private InputHandle_t[] controllerHandles = new InputHandle_t[Constants.STEAM_INPUT_MAX_COUNT];
    private int activeControllerCount;
    private bool actionsInitialized;

    private InputActionSetHandle_t inGameSetHandle;
    private InputActionSetHandle_t menuSetHandle;

    private InputDigitalActionHandle_t meleeAttackHandle;
    private InputDigitalActionHandle_t shieldThrowHandle;
    private InputDigitalActionHandle_t dashHandle;
    private InputDigitalActionHandle_t pauseMenuHandle;
    private InputAnalogActionHandle_t moveHandle;
    private InputDigitalActionHandle_t interactHandle;
    private InputDigitalActionHandle_t inventoryHandle;
    private InputDigitalActionHandle_t activateSkillHandle;
    private InputDigitalActionHandle_t defenseHandle;
    private InputDigitalActionHandle_t menuUpHandle;
    private InputDigitalActionHandle_t menuDownHandle;
    private InputDigitalActionHandle_t menuLeftHandle;
    private InputDigitalActionHandle_t menuRightHandle;
    private InputDigitalActionHandle_t menuSelectHandle;
    private InputDigitalActionHandle_t menuCancelHandle;
    private InputDigitalActionHandle_t menuSubmitHandle;
    private InputAnalogActionHandle_t aimHandle;
    #endregion

    #region Ciclo De Vida
    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (!SteamManager.Initialized) return;

#if UNITY_EDITOR
        Application.OpenURL("steam://forceinputappid/4858720");
#endif

        string manifestPath = System.IO.Path.Combine(Application.dataPath, "..", "game_actions_4858720.vdf");
        string fullPath = System.IO.Path.GetFullPath(manifestPath);

        Debug.Log($"[SteamInput] Ruta calculada: {fullPath}");
        Debug.Log($"[SteamInput] Existe el archivo: {System.IO.File.Exists(fullPath)}");

        bool manifestSet = SteamInput.SetInputActionManifestFilePath(fullPath);
        Debug.Log($"[SteamInput] Manifest seteado: {manifestSet}");

        bool inputInitialized = SteamInput.Init(false);
        Debug.Log($"[SteamInput] Init: {inputInitialized}");

        SteamInput.RunFrame();

        inGameSetHandle = SteamInput.GetActionSetHandle("InGameControls");
        menuSetHandle = SteamInput.GetActionSetHandle("MenuControls");

        Debug.Log($"[SteamInput] InGameControls handle: {inGameSetHandle}");
        Debug.Log($"[SteamInput] MenuControls handle: {menuSetHandle}");

        if (inGameSetHandle == default(InputActionSetHandle_t))
        {
            Debug.LogError("[SteamInput] No se pudo cargar InGameControls. Revisa que Steam esté abierto, que el AppID sea 4858720 y reinicia Unity después de forceinputappid.");
            return;
        }

        Debug.Log($"[SteamInput] InGameSet handle: {inGameSetHandle}");
    }

    private void Update()
    {
        if (!SteamManager.Initialized) return;

        SteamInput.RunFrame();
        RefreshControllers();

        if (activeControllerCount <= 0) return;

        if (!actionsInitialized)
        {
            InitializeActions();
            actionsInitialized = true;
        }

        if (SteamManager.OverlayActive) return;

        if (IsMenuScene())
        {
            ActivateMenuSet();
        }
        else
        {
            ActivateInGameSet();
        }
    }


    private void InitializeActions()
    {
        meleeAttackHandle = SteamInput.GetDigitalActionHandle("melee_attack");
        shieldThrowHandle = SteamInput.GetDigitalActionHandle("shield_throw");
        dashHandle = SteamInput.GetDigitalActionHandle("dash");
        pauseMenuHandle = SteamInput.GetDigitalActionHandle("pause_menu");

        moveHandle = SteamInput.GetAnalogActionHandle("Move");
        aimHandle = SteamInput.GetAnalogActionHandle("Aim");

        interactHandle = SteamInput.GetDigitalActionHandle("interact");
        inventoryHandle = SteamInput.GetDigitalActionHandle("inventory");
        activateSkillHandle = SteamInput.GetDigitalActionHandle("activate_skill");
        defenseHandle = SteamInput.GetDigitalActionHandle("defense");

        menuUpHandle = SteamInput.GetDigitalActionHandle("menu_up");
        menuDownHandle = SteamInput.GetDigitalActionHandle("menu_down");
        menuLeftHandle = SteamInput.GetDigitalActionHandle("menu_left");
        menuRightHandle = SteamInput.GetDigitalActionHandle("menu_right");
        menuSelectHandle = SteamInput.GetDigitalActionHandle("menu_select");
        menuCancelHandle = SteamInput.GetDigitalActionHandle("menu_cancel");
        menuSubmitHandle = SteamInput.GetDigitalActionHandle("menu_submit");

        Debug.Log("[SteamInput] Actions inicializadas correctamente");
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
#if UNITY_EDITOR
            Application.OpenURL("steam://forceinputappid/0");
#endif
            instance = null;
        }
    }
    #endregion

    #region Escenas Menu

    [SerializeField] private string[] menuScenes;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }


    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (string menuScene in menuScenes)
        {
            if (scene.name == menuScene)
            {
                ActivateMenuSet();
                Debug.Log("[SteamInput] MenuControls activado en escena: " + scene.name);
                return;
            }
        }

        ActivateInGameSet();
        Debug.Log("[SteamInput] InGameControls activado en escena: " + scene.name);
    }

    private bool IsMenuScene()
    {
        foreach (string s in menuScenes)
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == s)
                return true;

        return false;
    }
    #endregion

    #region Controladores
    private void RefreshControllers()
    {
        activeControllerCount = SteamInput.GetConnectedControllers(controllerHandles);
    }

    private InputHandle_t GetFirstController()
    {
        if (activeControllerCount <= 0)
            activeControllerCount = SteamInput.GetConnectedControllers(controllerHandles);

        return activeControllerCount > 0 ? controllerHandles[0] : default(InputHandle_t);
    }
    #endregion

    #region Action Sets
    public void ActivateInGameSet()
    {
        InputHandle_t controller = GetFirstController();

        if (controller == default(InputHandle_t))
        {
            Debug.LogWarning("[SteamInput] No hay control para activar InGameControls");
            return;
        }

        if (inGameSetHandle == default(InputActionSetHandle_t))
        {
            Debug.LogError("[SteamInput] InGameControls handle es 0");
            return;
        }

        SteamInput.ActivateActionSet(controller, inGameSetHandle);
    }

    public void ActivateMenuSet()
    {
        InputHandle_t controller = GetFirstController();

        if (controller == default(InputHandle_t))
        {
            Debug.LogWarning("[SteamInput] No hay control para activar MenuControls");
            return;
        }

        if (menuSetHandle == default(InputActionSetHandle_t))
        {
            Debug.LogError("[SteamInput] MenuControls handle es 0");
            return;
        }

        SteamInput.ActivateActionSet(controller, menuSetHandle);
    }
    #endregion

    #region Lecturas Publicas
    public bool GetMeleeAttackPressed()
    {
        if (SteamManager.OverlayActive) return false;
        InputHandle_t controller = GetFirstController();
        if (controller == default(InputHandle_t)) return false;
        return SteamInput.GetDigitalActionData(controller, meleeAttackHandle).bState != 0;
    }

    public bool GetShieldThrowPressed()
    {
        if (SteamManager.OverlayActive) return false;
        InputHandle_t controller = GetFirstController();
        if (controller == default(InputHandle_t)) return false;
        return SteamInput.GetDigitalActionData(controller, shieldThrowHandle).bState != 0;
    }

    public bool GetDashPressed()
    {
        if (SteamManager.OverlayActive) return false;
        InputHandle_t controller = GetFirstController();
        if (controller == default(InputHandle_t)) return false;
        return SteamInput.GetDigitalActionData(controller, dashHandle).bState != 0;
    }

    public Vector2 GetMoveAxis()
    {
        if (SteamManager.OverlayActive) return Vector2.zero;
        if (moveHandle == default(InputAnalogActionHandle_t)) return Vector2.zero;

        InputHandle_t controller = GetFirstController();
        if (controller == default(InputHandle_t)) return Vector2.zero;

        InputAnalogActionData_t data = SteamInput.GetAnalogActionData(controller, moveHandle);
        if (data.bActive == 0) return Vector2.zero;

        return new Vector2(data.x, data.y);
    }

    public Vector2 GetAimAxis()
    {
        if (SteamManager.OverlayActive) return Vector2.zero;
        if (aimHandle == default(InputAnalogActionHandle_t)) return Vector2.zero;

        InputHandle_t controller = GetFirstController();
        if (controller == default(InputHandle_t)) return Vector2.zero;

        InputAnalogActionData_t data = SteamInput.GetAnalogActionData(controller, aimHandle);
        if (data.bActive == 0) return Vector2.zero;

        return new Vector2(data.x, data.y);
    }

    public bool GetInteractPressed() => GetDigital(interactHandle);
    public bool GetInventoryPressed() => GetDigital(inventoryHandle);
    public bool GetActivateSkillPressed() => GetDigital(activateSkillHandle);
    public bool GetDefensePressed() => GetDigital(defenseHandle);

    public bool GetMenuUpPressed() => GetDigital(menuUpHandle);
    public bool GetMenuDownPressed() => GetDigital(menuDownHandle);
    public bool GetMenuLeftPressed() => GetDigital(menuLeftHandle);
    public bool GetMenuRightPressed() => GetDigital(menuRightHandle);
    public bool GetMenuSelectPressed() => GetDigital(menuSelectHandle);
    public bool GetMenuCancelPressed() => GetDigital(menuCancelHandle);
    public bool GetMenuSubmitPressed() => GetDigital(menuSubmitHandle);
    public bool GetPauseMenuPressed() => GetDigital(pauseMenuHandle);

    private bool GetDigital(InputDigitalActionHandle_t handle)
    {
        if (SteamManager.OverlayActive) return false;

        InputHandle_t controller = GetFirstController();
        if (controller == default(InputHandle_t)) return false;
        if (handle == default(InputDigitalActionHandle_t)) return false;

        return SteamInput.GetDigitalActionData(controller, handle).bState != 0;
    }
    #endregion
}