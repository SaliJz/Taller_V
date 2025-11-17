using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class ControlMenu : MonoBehaviour, PlayerControlls.IUIActions
{
    [SerializeField] private GameObject firstSelectedButton;
    private PlayerControlls playerControls;

    void Awake()
    {
        playerControls = new PlayerControlls();
        playerControls.UI.SetCallbacks(this);
    }

    void OnEnable()
    {
        playerControls?.UI.Enable();

        if (firstSelectedButton != null && Gamepad.current != null)
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(firstSelectedButton);
            }
        }
        else if (firstSelectedButton == null)
        {
            Debug.LogWarning("[ControlMenu] No se pudo establecer el foco inicial. Asigna 'First Selected Button' en el Inspector.");
        }
    }

    void OnDisable()
    {
        playerControls?.UI.Disable();

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    void OnDestroy()
    {
        playerControls?.Dispose();
    }

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
}