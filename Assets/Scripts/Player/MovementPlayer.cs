using UnityEngine;

public class MovementPlayer : MonoBehaviour
{
    #region Movement Settings
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 6f;
    [SerializeField] private float stepTransitionSpeed = 8f;
    [SerializeField] private float stopThreshold = 0.1f;
    #endregion

    #region Ground Detection Settings
    [Header("Ground Detection")]
    [SerializeField] private float groundDistance = 0.4f;
    [SerializeField] private LayerMask groundMask = 1;
    #endregion

    #region Components
    private CharacterController controller;
    private Transform playerCamera;
    #endregion

    #region Movement Variables
    private Vector3 currentVelocity;
    private Vector3 targetVelocity;
    private bool isGrounded;
    private bool isRunning;
    private Transform groundCheck;

    private Vector3 lastInputDirection;
    private bool isCompletingStep;
    private float stepCompletionTime;
    private const float MAX_STEP_COMPLETION_TIME = 0.3f;
    #endregion

    void Start()
    {
        controller = GetComponent<CharacterController>();
        playerCamera = Camera.main.transform;

        GameObject groundCheckObj = new GameObject("GroundCheck");
        groundCheckObj.transform.SetParent(transform);
        groundCheckObj.transform.localPosition = new Vector3(0, -1f, 0);
        groundCheck = groundCheckObj.transform;
    }

    void Update()
    {
        HandleGroundCheck();
        HandleInput();
        HandleMovement();
        ApplyMovement();
    }

    #region Ground Detection
    private void HandleGroundCheck()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
    }
    #endregion

    #region Input Handling
    private void HandleInput()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        isRunning = Input.GetKey(KeyCode.LeftShift);

        Vector3 inputDirection = (transform.right * horizontal + transform.forward * vertical).normalized;

        if (inputDirection.magnitude > 0.1f)
        {
            if (Vector3.Angle(inputDirection, lastInputDirection) > 30f || lastInputDirection.magnitude < 0.1f)
            {
                isCompletingStep = false;
                stepCompletionTime = 0f;
            }
            lastInputDirection = inputDirection;

            float currentSpeed = isRunning ? runSpeed : walkSpeed;
            targetVelocity = inputDirection * currentSpeed;
        }
        else
        {
            if (!isCompletingStep && currentVelocity.magnitude > stopThreshold)
            {
                isCompletingStep = true;
                stepCompletionTime = 0f;
            }

            if (isCompletingStep)
            {
                stepCompletionTime += Time.deltaTime;
                if (stepCompletionTime >= MAX_STEP_COMPLETION_TIME)
                {
                    targetVelocity = Vector3.zero;
                    isCompletingStep = false;
                }
            }
            else
            {
                targetVelocity = Vector3.zero;
            }

            lastInputDirection = Vector3.zero;
        }
    }
    #endregion

    #region Movement Logic
    private void HandleMovement()
    {
        if (!isGrounded)
        {
            currentVelocity.y += Physics.gravity.y * Time.deltaTime;
            return;
        }

        currentVelocity.y = -2f;

        if (isCompletingStep)
        {
            float completionProgress = stepCompletionTime / MAX_STEP_COMPLETION_TIME;
            float easedProgress = Mathf.SmoothStep(0f, 1f, completionProgress);

            Vector3 stepTarget = Vector3.Lerp(currentVelocity, Vector3.zero, easedProgress);
            currentVelocity.x = stepTarget.x;
            currentVelocity.z = stepTarget.z;
        }
        else
        {
            Vector3 horizontalTarget = new Vector3(targetVelocity.x, 0, targetVelocity.z);
            Vector3 horizontalCurrent = new Vector3(currentVelocity.x, 0, currentVelocity.z);

            Vector3 newHorizontalVelocity = Vector3.Lerp(horizontalCurrent, horizontalTarget,
                stepTransitionSpeed * Time.deltaTime);

            currentVelocity.x = newHorizontalVelocity.x;
            currentVelocity.z = newHorizontalVelocity.z;
        }
    }
    #endregion

    #region Apply Movement
    private void ApplyMovement()
    {
        controller.Move(currentVelocity * Time.deltaTime);
    }
    #endregion

    #region Debug
    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
        }

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, currentVelocity);

        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, targetVelocity);
    }
    #endregion
}