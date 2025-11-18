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

    private InputSystemUIInputModule uiInputModule;
    private RectTransform canvasRect;
    private Camera canvasCamera;

    private InputDevice currentActiveDevice = null;
    private Vector2 lastValidCursorPosition = Vector2.zero;

    private Gamepad currentGamepad;
    private InputDevice lastReportedDevice = null;

    private GameObject lastSelectedObject = null;
    private SettingsPanel settingsPanel;

    private void Awake()
    {
        if (virtualCursor != null)
        {
            Canvas canvas = virtualCursor.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvasRect = canvas.GetComponent<RectTransform>();

                if (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace)
                {
                    canvasCamera = canvas.worldCamera;
                }
            }
        }

        uiInputModule = FindAnyObjectByType<InputSystemUIInputModule>();
        settingsPanel = FindAnyObjectByType<SettingsPanel>();

        if (uiInputModule == null)
        {
            Debug.LogError("InputSystemUIInputModule no encontrado. El cursor de mando no funcionará correctamente.");
            enabled = false;
            return;
        }

        if (virtualCursor != null)
        {
            virtualCursor.gameObject.SetActive(false);
        }

        currentGamepad = Gamepad.current;
        if (Mouse.current != null)
        {
            currentActiveDevice = Mouse.current;
        }
        else if (Keyboard.current != null)
        {
            currentActiveDevice = Keyboard.current;
        }

        lastReportedDevice = currentActiveDevice;
    }

    private void Update()
    {
        if (currentGamepad == null)
        {
            currentGamepad = Gamepad.current;
        }

        bool isMouseMovedSignificantly = Mouse.current != null &&
                                 Mouse.current.delta.ReadValue().magnitude > 0.1f;

        bool isMouseOrKeyInteracting = (Mouse.current != null &&
                               (Mouse.current.leftButton.wasPressedThisFrame ||
                                Mouse.current.rightButton.wasPressedThisFrame)) ||
                               (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame);

        bool isMouseOrKeyboardActive = isMouseMovedSignificantly || isMouseOrKeyInteracting;

        bool isGamepadActive = false;
        Vector2 stickValue = Vector2.zero;
        if (currentGamepad != null)
        {
            stickValue = currentGamepad.rightStick.ReadValue();

            bool isStickMoving = stickValue.magnitude > RightStickDeadZone || currentGamepad.leftStick.ReadValue().magnitude > RightStickDeadZone;

            bool isGamepadInteracting = isStickMoving ||
                currentGamepad.dpad.IsActuated() ||
                currentGamepad.buttonSouth.wasPressedThisFrame ||
                currentGamepad.buttonEast.wasPressedThisFrame ||
                currentGamepad.buttonWest.wasPressedThisFrame ||
                currentGamepad.buttonNorth.wasPressedThisFrame;

            isGamepadActive = isGamepadInteracting;
        }

        InputDevice previousActiveDevice = currentActiveDevice;

        if (isMouseOrKeyboardActive)
        {
            currentActiveDevice = isMouseMovedSignificantly || isMouseOrKeyInteracting ? Mouse.current : (InputDevice)Keyboard.current;
        }
        else if (isGamepadActive)
        {
            currentActiveDevice = currentGamepad;
        }

        bool isCurrentDeviceGamepad = (currentActiveDevice == currentGamepad) && (currentGamepad != null);
        bool shouldFollowSelected = Time.timeScale == 0f || isCurrentDeviceGamepad;

        if (isCurrentDeviceGamepad)
        {
            GameObject selected = EventSystem.current?.currentSelectedGameObject;
            if (selected != null)
            {
                lastSelectedObject = selected;
            }
        }

        if (currentActiveDevice == currentGamepad && previousActiveDevice != currentGamepad)
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

        if (currentActiveDevice != lastReportedDevice)
        {
            string deviceName;
            if (currentActiveDevice == currentGamepad)
            {
                deviceName = "GAMEPAD (Control de Mando)";
            }
            else if (currentActiveDevice == Mouse.current)
            {
                deviceName = "MOUSE (Ratón)";
            }
            else if (currentActiveDevice == Keyboard.current)
            {
                deviceName = "KEYBOARD (Teclado)";
            }
            else
            {
                deviceName = "NINGUNO/OTRO";
            }

            Debug.Log($"[GamepadPointer] Control activo cambiado a: {deviceName}");
            lastReportedDevice = currentActiveDevice;
        }

        if (isCurrentDeviceGamepad)
        {
            float deltaTime = Time.unscaledDeltaTime;

            if (stickValue.magnitude > RightStickDeadZone)
            {
                if (!virtualCursor.gameObject.activeSelf)
                {
                    virtualCursor.gameObject.SetActive(true);
                    virtualCursor.anchoredPosition = lastValidCursorPosition;
                }

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
                    float effectiveRadius = maxRadius - cursorHalfSize;

                    effectiveRadius = Mathf.Max(0f, effectiveRadius);

                    float distanceFromCenter = newPosition.magnitude;

                    if (distanceFromCenter > effectiveRadius)
                    {
                        newPosition = newPosition.normalized * effectiveRadius;
                    }
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
                }
            }
            else if (shouldFollowSelected)
            {
                GameObject selectedObject = EventSystem.current?.currentSelectedGameObject;

                if (selectedObject != null)
                {
                    if (!virtualCursor.gameObject.activeSelf) virtualCursor.gameObject.SetActive(true);

                    RectTransform targetRect = selectedObject.GetComponent<RectTransform>();
                    Slider slider = selectedObject.GetComponent<Slider>();

                    if (slider != null && IsMenuSlider(slider))
                    {
                        if (slider.handleRect != null)
                        {
                            targetRect = slider.handleRect;
                        }
                    }

                    if (targetRect != null)
                    {
                        Vector3 targetPosition = targetRect.position;
                        virtualCursor.position = Vector3.Lerp(virtualCursor.position, targetPosition, deltaTime * CursorFollowSpeed);
                        lastValidCursorPosition = virtualCursor.anchoredPosition;
                    }

                    if (Mouse.current != null)
                    {
                        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvasCamera, virtualCursor.position);
                        Mouse.current.WarpCursorPosition(screenPoint);
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
            {
                virtualCursor.gameObject.SetActive(false);
            }

            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                (currentActiveDevice == Mouse.current || currentActiveDevice == Keyboard.current))
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            {
                if (previousActiveDevice == currentGamepad && currentActiveDevice != currentGamepad)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
            }
        }
    }

    private bool IsMenuSlider(Slider slider)
    {
        if (settingsPanel == null) return false;

        Slider[] menuSliders = settingsPanel.GetMenuSliders();
        if (menuSliders == null || menuSliders.Length == 0) return false;

        foreach (Slider menuSlider in menuSliders)
        {
            if (menuSlider == slider)
            {
                return true;
            }
        }
        return false;
    }
}