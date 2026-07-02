using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// Este script controla el indicador direccional al usar el mando, mostrandolo debajo del personaje o desactivandolo cuando se usa el
/// teclado + mouse
/// </summary>
public class JoystickDirectionalSpriteCtrl : MonoBehaviour
{
    [Header ("References")]
    [SerializeField] GameObject directionalObject;
    [SerializeField] Transform directionalTransform;

    [Header("Directional Sprite Settings")]
    [SerializeField] private float deadzone = 0.2f;
    [SerializeField] private float rotationSmoothSpeed = 20f;
    [SerializeField] private float angleOffset = 0f;

    private bool lastInputWasGamepad;
    private float currentAngle;
    private float targetAngle;
    private bool hasValidDirection;
    private float baseEulerX;
    private float baseEulerY;

    void Awake()
    {
        if (directionalTransform == null) directionalTransform = directionalObject.transform;

        baseEulerX = directionalTransform.localEulerAngles.x;
        baseEulerY = directionalTransform.localEulerAngles.y;

    }

    void Update()
    {
        DetectLastInput();

        Vector2 stick = Gamepad.current != null? Gamepad.current.leftStick.ReadValue() : Vector2.zero;
        hasValidDirection = stick.magnitude >= deadzone;

        bool shouldShow = lastInputWasGamepad && hasValidDirection;
        SetDirectionalVisible(shouldShow);

        if (!shouldShow) return;

        targetAngle = Mathf.Atan2(stick.x, stick. y) * Mathf.Rad2Deg;
        targetAngle += angleOffset;

        currentAngle = Mathf.LerpAngle(currentAngle, targetAngle, 1f - Mathf.Exp(-rotationSmoothSpeed * Time.deltaTime));

        directionalTransform.rotation = Quaternion.Euler(baseEulerX, baseEulerY, currentAngle);
    }

    private void DetectLastInput()
    {
        if (Gamepad.current != null && GamepadHasInput(Gamepad.current))
        {
            lastInputWasGamepad = true;
        }
        else if ((Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) || 
            (Mouse.current != null && (Mouse.current.delta.ReadValue().sqrMagnitude > 0.01f || Mouse.current.leftButton.wasPressedThisFrame ||
            Mouse.current.rightButton.wasPressedThisFrame)))
        {
            lastInputWasGamepad = false;
        }
    }

    private bool GamepadHasInput(Gamepad gamepad)
    {
        if (gamepad.leftStick.ReadValue().magnitude >= deadzone) return true;

        foreach (var control in gamepad.allControls)
        {
            if (control is ButtonControl button && button.wasPressedThisFrame) return true;
        }
        return false;
    }

    private void SetDirectionalVisible(bool value)
    {
        if (directionalObject != null && directionalObject.activeSelf != value)
        {
            directionalObject.SetActive(value);
        }
    }


}
