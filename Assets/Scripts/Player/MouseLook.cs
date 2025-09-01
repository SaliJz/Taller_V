using UnityEngine;

public class MouseLook : MonoBehaviour
{
    #region Mouse Settings
    [Header("Mouse Sensitivity")]
    [SerializeField] private float mouseXSensitivity = 100f;
    [SerializeField] private float mouseYSensitivity = 100f;
    #endregion

    #region Rotation Limits
    [Header("Rotation Limits")]
    [SerializeField] private float minVerticalAngle = -90f;
    [SerializeField] private float maxVerticalAngle = 90f;
    [SerializeField] private bool invertYAxis = false;
    #endregion

    #region Cursor Settings
    [Header("Cursor Settings")]
    [SerializeField] private bool hideCursor = true;
    [SerializeField] private bool confineCursor = false;
    [SerializeField] private CursorLockMode initialLockMode = CursorLockMode.Locked;
    #endregion

    #region Smoothing Settings
    [Header("Smoothing")]
    [SerializeField] private bool enableSmoothing = false;
    [SerializeField] private float smoothingFactor = 5f;
    #endregion

    #region Components
    private Transform playerBody;
    private Camera playerCamera;
    #endregion

    #region Rotation Variables
    private float xRotation = 0f;
    private Vector2 currentMouseDelta;
    private Vector2 targetMouseDelta;
    #endregion

    void Start()
    {
        playerCamera = GetComponent<Camera>();
        playerBody = transform.parent;

        if (playerBody == null)
        {
            playerBody = FindFirstObjectByType<MovementPlayer>().transform;
        }

        Cursor.lockState = initialLockMode;
        Cursor.visible = !hideCursor;

        if (confineCursor && initialLockMode != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Confined;
        }
    }

    void Update()
    {
        HandleMouseInput();
        ApplyRotation();
        HandleCursorToggle();
    }

    #region Mouse Input
    private void HandleMouseInput()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseXSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseYSensitivity * Time.deltaTime;

        if (invertYAxis)
            mouseY = -mouseY;

        targetMouseDelta = new Vector2(mouseX, mouseY);

        if (enableSmoothing)
        {
            currentMouseDelta = Vector2.Lerp(currentMouseDelta, targetMouseDelta, smoothingFactor * Time.deltaTime);
        }
        else
        {
            currentMouseDelta = targetMouseDelta;
        }
    }
    #endregion

    #region Rotation Logic
    private void ApplyRotation()
    {
        xRotation -= currentMouseDelta.y;
        xRotation = Mathf.Clamp(xRotation, minVerticalAngle, maxVerticalAngle);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerBody.Rotate(Vector3.up * currentMouseDelta.x);
    }
    #endregion

    #region Cursor Management
    private void HandleCursorToggle()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleCursorState();
        }
    }

    private void ToggleCursorState()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            if (confineCursor)
            {
                Cursor.lockState = CursorLockMode.Confined;
                Cursor.visible = !hideCursor;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = !hideCursor;
            }
        }
    }
    #endregion

    #region Public Methods
    public void SetSensitivity(float sensitivity)
    {
        mouseXSensitivity = sensitivity;
        mouseYSensitivity = sensitivity;
    }

    public void SetMouseLock(bool lockMouse, bool hideMouseCursor = true)
    {
        if (lockMouse)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = !hideMouseCursor;
        }
        else
        {
            if (confineCursor)
            {
                Cursor.lockState = CursorLockMode.Confined;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
            }
            Cursor.visible = true;
        }
    }

    public void SetCursorConfined(bool confined)
    {
        confineCursor = confined;
        if (confined && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Confined;
        }
    }
    #endregion
}