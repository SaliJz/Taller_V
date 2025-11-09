using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;

public class GamepadPointer : MonoBehaviour
{
    public RectTransform virtualCursor;

    [SerializeField] private float cursorSpeed = 1500f;

    private const float CursorFollowSpeed = 30f;
    private const float RightStickDeadZone = 0.2f;

    private InputSystemUIInputModule uiInputModule;
    private RectTransform canvasRect;
    private Camera canvasCamera;

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
    }

    private void Update()
    {
        bool isAnyGamepadConnected = Gamepad.all.Count > 0;

        bool isGamepadActive = isAnyGamepadConnected && Gamepad.current != null;

        if (!isGamepadActive || virtualCursor == null || canvasRect == null || uiInputModule == null)
        {
            if (virtualCursor != null && virtualCursor.gameObject.activeSelf)
            {
                virtualCursor.gameObject.SetActive(false);
            }
            return;
        }

        Vector2 stickValue = Gamepad.current.rightStick.ReadValue();
        bool isMovingCursor = stickValue.magnitude > RightStickDeadZone;

        float deltaTime = Time.unscaledDeltaTime;

        bool isPauseControllerActive = PauseController.Instance != null;
        bool shouldFollowSelected = Time.timeScale == 0f || !isPauseControllerActive;

        if (isMovingCursor)
        {
            if (!virtualCursor.gameObject.activeSelf) virtualCursor.gameObject.SetActive(true);

            Vector2 screenDelta = stickValue * cursorSpeed * deltaTime;
            Vector2 newPosition = virtualCursor.anchoredPosition + screenDelta;

            float cursorHalfWidth = virtualCursor.rect.width * 0.5f;
            float cursorHalfHeight = virtualCursor.rect.height * 0.5f;

            float minX = -(canvasRect.rect.width * 0.5f) + cursorHalfWidth;
            float maxX = (canvasRect.rect.width * 0.5f) - cursorHalfWidth;
            float minY = -(canvasRect.rect.height * 0.5f) + cursorHalfHeight;
            float maxY = (canvasRect.rect.height * 0.5f) - cursorHalfHeight;

            newPosition.x = Mathf.Clamp(newPosition.x, minX, maxX);
            newPosition.y = Mathf.Clamp(newPosition.y, minY, maxY);

            virtualCursor.anchoredPosition = newPosition;

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

                RectTransform selectedRect = selectedObject.GetComponent<RectTransform>();

                if (selectedRect != null)
                {
                    Vector3 targetPosition = selectedRect.position;
                    virtualCursor.position = Vector3.Lerp(virtualCursor.position, targetPosition, deltaTime * CursorFollowSpeed);
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

        if (Mouse.current != null && Mouse.current.delta.ReadValue().magnitude > 0.1f)
        {
            virtualCursor.gameObject.SetActive(false);
        }
    }
}