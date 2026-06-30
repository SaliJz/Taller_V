using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class ControlMenu : MonoBehaviour, PlayerControlls.IUIActions
{
    [SerializeField] private GameObject firstSelectedButton;
    private PlayerControlls playerControls;
    private GameObject previousSelectedGameObject;
    private GameObject lastSelected;

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
    #endregion
}