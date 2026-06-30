using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class ControlMenu : MonoBehaviour, PlayerControlls.IUIActions
{
    [SerializeField] private GameObject firstSelectedButton;
    private PlayerControlls playerControls;
    private GameObject previousSelectedGameObject;
    private GameObject lastSelected;

    private bool upPressedLastFrame;
    private bool downPressedLastFrame;
    private bool leftPressedLastFrame;
    private bool rightPressedLastFrame;
    private bool selectPressedLastFrame;
    private bool cancelPressedLastFrame;

    private float repeatTimer = 0f;
    private const float RepeatDelay = 0.4f;
    private const float RepeatInterval = 0.1f;
    private Vector2 heldDirection = Vector2.zero;
    private float repeatIntervalTimer = 0f;

    #region Ciclo De Vida
    private void Awake()
    {
        playerControls = new PlayerControlls();
        playerControls.UI.SetCallbacks(this);
    }

    private void OnEnable()
    {
        playerControls?.UI.Enable();

        if (GamepadPointer.Instance != null && GamepadPointer.Instance.GetCurrentGamepad() != null
            || SteamInputManager.Instance != null)
        {
            if (firstSelectedButton != null)
            {
                EventSystem.current?.SetSelectedGameObject(null);
                EventSystem.current?.SetSelectedGameObject(firstSelectedButton);
                lastSelected = firstSelectedButton;
            }
        }
    }

    private void OnDisable()
    {
        playerControls?.UI.Disable();

        if (EventSystem.current != null)
        {
            if (previousSelectedGameObject != null)
            {
                EventSystem.current.SetSelectedGameObject(previousSelectedGameObject);
                previousSelectedGameObject = null;
            }
            else
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        lastSelected = null;
    }

    private void OnDestroy()
    {
        playerControls?.Dispose();
    }

    private void Update()
    {
        if (SteamInputManager.Instance == null) return;

        bool up = SteamInputManager.Instance.GetMenuUpPressed();
        bool down = SteamInputManager.Instance.GetMenuDownPressed();
        bool left = SteamInputManager.Instance.GetMenuLeftPressed();
        bool right = SteamInputManager.Instance.GetMenuRightPressed();
        bool select = SteamInputManager.Instance.GetMenuSelectPressed();
        bool cancel = SteamInputManager.Instance.GetMenuCancelPressed();

        if (up && !upPressedLastFrame) { SendNavigate(Vector2.up); heldDirection = Vector2.up; repeatTimer = 0f; }
        else if (down && !downPressedLastFrame) { SendNavigate(Vector2.down); heldDirection = Vector2.down; repeatTimer = 0f; }
        else if (left && !leftPressedLastFrame) { SendNavigate(Vector2.left); heldDirection = Vector2.left; repeatTimer = 0f; }
        else if (right && !rightPressedLastFrame) { SendNavigate(Vector2.right); heldDirection = Vector2.right; repeatTimer = 0f; }

        if (heldDirection != Vector2.zero)
        {
            bool stillHeld = (heldDirection == Vector2.up && up)
                || (heldDirection == Vector2.down && down)
                || (heldDirection == Vector2.left && left)
                || (heldDirection == Vector2.right && right);

            if (stillHeld)
            {
                repeatTimer += Time.unscaledDeltaTime;
                if (repeatTimer >= RepeatDelay)
                {
                    repeatIntervalTimer += Time.unscaledDeltaTime;
                    if (repeatIntervalTimer >= RepeatInterval)
                    {
                        SendNavigate(heldDirection);
                        repeatIntervalTimer = 0f;
                    }
                }
            }
            else
            {
                heldDirection = Vector2.zero;
                repeatTimer = 0f;
                repeatIntervalTimer = 0f;
            }
        }

        if (select && !selectPressedLastFrame) SendSubmit();
        if (cancel && !cancelPressedLastFrame) SendCancel();

        upPressedLastFrame = up;
        downPressedLastFrame = down;
        leftPressedLastFrame = left;
        rightPressedLastFrame = right;
        selectPressedLastFrame = select;
        cancelPressedLastFrame = cancel;
    }

    private void LateUpdate()
    {
        if (SteamInputManager.Instance == null) return;
        if (EventSystem.current == null) return;

        GameObject current = EventSystem.current.currentSelectedGameObject;

        if (current != null)
        {
            lastSelected = current;
            return;
        }

        GameObject target = (lastSelected != null && lastSelected.activeInHierarchy)
            ? lastSelected
            : firstSelectedButton;

        if (target != null)
        {
            EventSystem.current.SetSelectedGameObject(target);
            lastSelected = target;
        }
    }
    #endregion

    #region Input Callbacks
    public void OnCancel(InputAction.CallbackContext context) { }
    public void OnNavigate(InputAction.CallbackContext context) { }
    public void OnSubmit(InputAction.CallbackContext context) { }
    public void OnPoint(InputAction.CallbackContext context) { }
    public void OnClick(InputAction.CallbackContext context) { }
    public void OnScrollWheel(InputAction.CallbackContext context) { }
    public void OnMiddleClick(InputAction.CallbackContext context) { }
    public void OnRightClick(InputAction.CallbackContext context) { }
    public void OnTrackedDevicePosition(InputAction.CallbackContext context) { }
    public void OnTrackedDeviceOrientation(InputAction.CallbackContext context) { }
    public void OnToggleInventory(InputAction.CallbackContext context) { }
    #endregion

    #region Navegacion
    public void FirstSelected()
    {
        if (firstSelectedButton == null) return;
        EventSystem.current?.SetSelectedGameObject(null);
        EventSystem.current?.SetSelectedGameObject(firstSelectedButton);
        lastSelected = firstSelectedButton;
    }

    private void SendNavigate(Vector2 direction)
    {
        if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null) return;

        AxisEventData eventData = new AxisEventData(EventSystem.current);
        eventData.moveVector = direction;
        eventData.moveDir = direction.y > 0 ? MoveDirection.Up
                          : direction.y < 0 ? MoveDirection.Down
                          : direction.x > 0 ? MoveDirection.Right
                                            : MoveDirection.Left;

        ExecuteEvents.Execute(EventSystem.current.currentSelectedGameObject, eventData, ExecuteEvents.moveHandler);

        if (EventSystem.current.currentSelectedGameObject != null)
            lastSelected = EventSystem.current.currentSelectedGameObject;
    }

    private void SendSubmit()
    {
        if (EventSystem.current?.currentSelectedGameObject == null) return;
        BaseEventData eventData = new BaseEventData(EventSystem.current);
        ExecuteEvents.Execute(EventSystem.current.currentSelectedGameObject, eventData, ExecuteEvents.submitHandler);
    }

    private void SendCancel()
    {
        if (EventSystem.current?.currentSelectedGameObject == null) return;
        BaseEventData eventData = new BaseEventData(EventSystem.current);
        ExecuteEvents.Execute(EventSystem.current.currentSelectedGameObject, eventData, ExecuteEvents.cancelHandler);
    }
    #endregion
}