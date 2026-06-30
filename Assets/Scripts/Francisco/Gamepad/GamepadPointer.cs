using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class GamepadPointer : MonoBehaviour
{
    [Header("Cursor in Pause Settings")]
    [SerializeField] private float pauseMovementRadiusFactor = 0.4f;
    [SerializeField] private float cursorSpeed = 1500f;
    [SerializeField] private RectTransform virtualCursor;

    private const float CursorFollowSpeed = 30f;
    private const float RightStickDeadZone = 0.2f;

    private float ignoreMouseInputUntilTime;
    private const float MouseWarpIgnoreDuration = 0.08f;

    private InputSystemUIInputModule uiInputModule;
    private RectTransform canvasRect;
    private Camera canvasCamera;

    private InputDevice currentActiveDevice = null;
    private Vector2 lastValidCursorPosition = Vector2.zero;

    private Gamepad currentGamepad;
    private InputDevice lastReportedDevice = null;
    private bool isSteamGamepadActive = false;
    private bool wasGamepadMode = false;

    private GameObject lastSelectedObject = null;
    private SettingsPanel settingsPanel;

    public static GamepadPointer Instance { get; private set; }

    #region Ciclo De Vida
    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = false;

        if (virtualCursor != null)
        {
            Canvas canvas = virtualCursor.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvasRect = canvas.GetComponent<RectTransform>();
                if (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace)
                    canvasCamera = canvas.worldCamera;
            }
        }

        uiInputModule = FindAnyObjectByType<InputSystemUIInputModule>();
        settingsPanel = FindAnyObjectByType<SettingsPanel>();

        if (uiInputModule == null)
        {
            Debug.LogError("InputSystemUIInputModule no encontrado.");
            enabled = false;
            return;
        }

        if (virtualCursor != null)
            virtualCursor.gameObject.SetActive(false);

        currentGamepad = Gamepad.current;

        if (SteamInputManager.Instance != null)
        {
            isSteamGamepadActive = true;
            currentActiveDevice = currentGamepad;
            Cursor.visible = false;
        }
        else if (currentGamepad != null)
        {
            isSteamGamepadActive = false;
            currentActiveDevice = currentGamepad;
            Cursor.visible = false;
        }
        else if (Mouse.current != null)
        {
            currentActiveDevice = Mouse.current;
            Cursor.visible = true;
        }
        else if (Keyboard.current != null)
        {
            currentActiveDevice = Keyboard.current;
            Cursor.visible = true;
        }

        lastReportedDevice = currentActiveDevice;
    }

    private void Update()
    {
        if (SteamManager.OverlayActive)
        {
            return;
        }

        if (currentGamepad == null)
            currentGamepad = Gamepad.current;

        bool canReadMouseInput = Time.unscaledTime >= ignoreMouseInputUntilTime;

        bool isMouseMovedSignificantly = canReadMouseInput &&
            Mouse.current != null &&
            Mouse.current.delta.ReadValue().magnitude > 0.1f;

        bool isMouseClickInteracting = canReadMouseInput &&
            Mouse.current != null &&
            (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame);

        bool isKeyboardInteracting = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
        bool isMouseOrKeyboardActive = isMouseMovedSignificantly || isMouseClickInteracting || isKeyboardInteracting;

        bool isGamepadActive = false;
        Vector2 stickValue = Vector2.zero;

        if (SteamInputManager.Instance != null)
        {
            Vector2 moveAxis = SteamInputManager.Instance.GetMoveAxis();
            Vector2 aimAxis = SteamInputManager.Instance.GetAimAxis();
            stickValue = aimAxis;

            bool isStickMoving = moveAxis.magnitude > RightStickDeadZone || aimAxis.magnitude > RightStickDeadZone;
            bool isButtonPressed = SteamInputManager.Instance.GetMeleeAttackPressed()
                || SteamInputManager.Instance.GetDashPressed()
                || SteamInputManager.Instance.GetMenuSelectPressed()
                || SteamInputManager.Instance.GetMenuCancelPressed();

            isGamepadActive = isStickMoving || isButtonPressed;
        }
        else if (currentGamepad != null)
        {
            stickValue = currentGamepad.rightStick.ReadValue();
            bool isStickMoving = stickValue.magnitude > RightStickDeadZone
                || currentGamepad.leftStick.ReadValue().magnitude > RightStickDeadZone;
            bool isButtonPressed = currentGamepad.dpad.IsActuated()
                || currentGamepad.buttonSouth.wasPressedThisFrame
                || currentGamepad.buttonEast.wasPressedThisFrame;

            isGamepadActive = isStickMoving || isButtonPressed;
        }

        InputDevice previousActiveDevice = currentActiveDevice;

        if (isMouseOrKeyboardActive)
        {
            currentActiveDevice = isMouseMovedSignificantly || isMouseClickInteracting
                ? Mouse.current
                : (InputDevice)Keyboard.current;
            isSteamGamepadActive = false;
        }
        else if (isGamepadActive)
        {
            currentActiveDevice = (InputDevice)currentGamepad ?? Mouse.current;
            isSteamGamepadActive = SteamInputManager.Instance != null;
        }

        bool isCurrentDeviceGamepad = isSteamGamepadActive || (currentActiveDevice == currentGamepad && currentGamepad != null);
        bool shouldFollowSelected = Time.timeScale == 0f || isCurrentDeviceGamepad;

        if (isCurrentDeviceGamepad)
        {
            GameObject selected = EventSystem.current?.currentSelectedGameObject;
            if (selected != null)
                lastSelectedObject = selected;
        }

        if (isCurrentDeviceGamepad && previousActiveDevice != currentGamepad && !isSteamGamepadActive)
        {
            GameObject targetObject = lastSelectedObject;

            if (targetObject == null)
            {
                ControlMenu controlMenu = FindAnyObjectByType<ControlMenu>();
                if (controlMenu != null)
                {
                    controlMenu.FirstSelected();
                    return;
                }
            }

            if (targetObject != null && targetObject.activeInHierarchy)
            {
                EventSystem.current?.SetSelectedGameObject(null);
                EventSystem.current?.SetSelectedGameObject(targetObject);
            }
        }

        bool currentGamepadMode = IsGamepadMode();
        if (currentGamepadMode != wasGamepadMode)
        {
            if (currentGamepadMode)
            {
                Debug.Log("[GamepadPointer] Control activo cambiado a: GAMEPAD (Control de Mando)");
                Cursor.visible = false;
            }
            else
            {
                string deviceName = currentActiveDevice == Mouse.current ? "MOUSE (Ratón)" : "KEYBOARD (Teclado)";
                Debug.Log($"[GamepadPointer] Control activo cambiado a: {deviceName}");
                Cursor.visible = currentActiveDevice != null;
            }
            wasGamepadMode = currentGamepadMode;
            lastReportedDevice = currentActiveDevice;
        }

        if (isCurrentDeviceGamepad)
        {
            float deltaTime = Time.unscaledDeltaTime;

            if (stickValue.magnitude > RightStickDeadZone)
            {
                Cursor.visible = false;
                virtualCursor.anchoredPosition = lastValidCursorPosition;

                Vector2 screenDelta = stickValue * cursorSpeed * deltaTime;
                Vector2 newPosition = virtualCursor.anchoredPosition + screenDelta;

                float cursorHalfWidth = virtualCursor.rect.width * 0.5f;
                float cursorHalfHeight = virtualCursor.rect.height * 0.5f;

                if (Time.timeScale != 0f || !PauseController.IsGamePaused)
                {
                    float canvasHalfWidth = canvasRect.rect.width * 0.5f;
                    float canvasHalfHeight = canvasRect.rect.height * 0.5f;
                    float maxRadius = Mathf.Min(canvasHalfWidth, canvasHalfHeight) * pauseMovementRadiusFactor;
                    float cursorHalfSize = Mathf.Max(cursorHalfWidth, cursorHalfHeight);
                    float effectiveRadius = Mathf.Max(0f, maxRadius - cursorHalfSize);
                    float distanceFromCenter = newPosition.magnitude;

                    if (distanceFromCenter > effectiveRadius)
                        newPosition = newPosition.normalized * effectiveRadius;
                }
                else
                {
                    float minX = -(canvasRect.rect.width * 0.5f) + cursorHalfWidth;
                    float maxX = (canvasRect.rect.width * 0.5f) - cursorHalfWidth;
                    float minY = -(canvasRect.rect.height * 0.5f) + cursorHalfHeight;
                    float maxY = (canvasRect.rect.height * 0.5f) - cursorHalfHeight;

                    newPosition.x = Mathf.Clamp(newPosition.x, minX, maxX);
                    newPosition.y = Mathf.Clamp(newPosition.y, minY, maxY);
                }

                virtualCursor.anchoredPosition = newPosition;
                lastValidCursorPosition = newPosition;

                if (Mouse.current != null)
                {
                    Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvasCamera, virtualCursor.position);
                    Mouse.current.WarpCursorPosition(screenPoint);
                    ignoreMouseInputUntilTime = Time.unscaledTime + MouseWarpIgnoreDuration;
                    Cursor.visible = false;
                }
            }
            else if (shouldFollowSelected)
            {
                GameObject selectedObject = EventSystem.current?.currentSelectedGameObject;

                if (selectedObject != null)
                {
                    RectTransform targetRect = selectedObject.GetComponent<RectTransform>();
                    Slider slider = selectedObject.GetComponent<Slider>();

                    if (slider != null && IsMenuSlider(slider) && slider.handleRect != null)
                        targetRect = slider.handleRect;

                    if (targetRect != null)
                    {
                        virtualCursor.position = Vector3.Lerp(virtualCursor.position, targetRect.position, deltaTime * CursorFollowSpeed);
                        lastValidCursorPosition = virtualCursor.anchoredPosition;
                    }

                    if (Mouse.current != null)
                    {
                        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvasCamera, virtualCursor.position);
                        Mouse.current.WarpCursorPosition(screenPoint);
                        ignoreMouseInputUntilTime = Time.unscaledTime + MouseWarpIgnoreDuration;
                        Cursor.visible = false;
                    }
                }
                else
                {
                    virtualCursor.gameObject.SetActive(false);
                }
            }
            else
            {
                virtualCursor.gameObject.SetActive(false);
            }
        }
        else
        {
            if (virtualCursor != null && virtualCursor.gameObject.activeSelf)
                virtualCursor.gameObject.SetActive(false);

            bool steamControllingMenu = SteamInputManager.Instance != null;

            if (!steamControllingMenu &&
                EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                && (currentActiveDevice == Mouse.current || currentActiveDevice == Keyboard.current))
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
    }
    #endregion

    #region Utilidades
    private bool IsMenuSlider(Slider slider)
    {
        if (settingsPanel == null) return false;
        Slider[] menuSliders = settingsPanel.GetMenuSliders();
        if (menuSliders == null || menuSliders.Length == 0) return false;

        foreach (Slider menuSlider in menuSliders)
            if (menuSlider == slider) return true;

        return false;
    }

    public bool IsGamepadMode()
    {
        return isSteamGamepadActive || (currentActiveDevice == currentGamepad && currentGamepad != null);
    }

    public InputDevice GetCurrentActiveDevice() => currentActiveDevice;
    public Gamepad GetCurrentGamepad() => currentGamepad;

    public Vector2 GetAimDirectionValue()
    {
        if (SteamInputManager.Instance != null)
        {
            Vector2 aim = SteamInputManager.Instance.GetAimAxis();
            if (aim.magnitude > RightStickDeadZone) return aim.normalized;

            Vector2 move = SteamInputManager.Instance.GetMoveAxis();
            if (move.magnitude > RightStickDeadZone) return move.normalized;
        }
        else if (currentGamepad != null)
        {
            Vector2 rightStick = currentGamepad.rightStick.ReadValue();
            if (rightStick.magnitude > RightStickDeadZone) return rightStick.normalized;

            Vector2 leftStick = currentGamepad.leftStick.ReadValue();
            if (leftStick.magnitude > RightStickDeadZone) return leftStick.normalized;
        }

        return Vector2.zero;
    }
    #endregion
}