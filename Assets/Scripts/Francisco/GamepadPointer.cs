using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;

public class GamepadPointer : MonoBehaviour
{
    public RectTransform virtualCursor;
    [SerializeField] private float cursorSpeed = 1000f;

    private InputSystemUIInputModule uiInputModule;
    private RectTransform canvasRect;
    private Camera canvasCamera;

    private const float CursorFollowSpeed = 20f;

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
            Debug.LogError("InputSystemUIInputModule no encontrado. El cursor de mando no funcionará.");
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
        if (uiInputModule == null || virtualCursor == null || canvasRect == null) return;

        if (Time.timeScale == 0f)
        {
            GameObject selectedObject = EventSystem.current?.currentSelectedGameObject;

            if (selectedObject != null)
            {
                if (!virtualCursor.gameObject.activeSelf)
                {
                    virtualCursor.gameObject.SetActive(true);
                }

                RectTransform selectedRect = selectedObject.GetComponent<RectTransform>();

                if (selectedRect != null)
                {
                    Vector3 targetPosition = selectedRect.position;
                    virtualCursor.position = Vector3.Lerp(virtualCursor.position, targetPosition, Time.unscaledDeltaTime * CursorFollowSpeed);
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

            return;
        }

        bool isGamepadActive = Gamepad.current != null;
        bool cursorWasMovedByGamepad = false;

        if (isGamepadActive)
        {
            Vector2 navigateValue = uiInputModule.move.action.ReadValue<Vector2>();

            if (navigateValue.magnitude > 0.1f)
            {
                virtualCursor.gameObject.SetActive(true);
                cursorWasMovedByGamepad = true;

                Vector2 screenDelta = navigateValue * cursorSpeed * Time.deltaTime;
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
            }
            else if (Mouse.current != null && Mouse.current.delta.ReadValue().magnitude > 0.1f)
            {
                virtualCursor.gameObject.SetActive(false);
            }
        }
        else
        {
            virtualCursor.gameObject.SetActive(false);
        }

        if (virtualCursor.gameObject.activeInHierarchy && cursorWasMovedByGamepad && Mouse.current != null)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvasCamera, virtualCursor.position);
            Mouse.current.WarpCursorPosition(screenPoint);
        }
    }
}