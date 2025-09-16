using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    #region Variables

    [Header("References")]
    [SerializeField] private PlayerStats PlayerStats;

    [Header("Stats")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -9.81f;
    //public Animator playerAnimator;

    private CharacterController controller;
    private Vector3 moveDirection;
    private float yVelocity;
    private Transform mainCameraTransform;
    private bool canMove = true;
    #endregion

    #region Unity Methods
    void Start()
    {
        controller = GetComponent<CharacterController>();
        mainCameraTransform = Camera.main.transform;

        if (PlayerStats != null)
        {
            moveSpeed = PlayerStats.moveSpeed;
            gravity = PlayerStats.gravity;
        }
    }

    void Update()
    {
        if (canMove)
        {
            HandleMovementInput();
        }
        else
        {
            moveDirection = Vector3.zero;
            //if (playerAnimator != null) playerAnimator.SetBool("IsMoving", false);
        }

        ApplyGravity();
    }

    void FixedUpdate()
    {
        if (controller.enabled)
        {
            Vector3 finalMove = moveDirection * moveSpeed;
            finalMove.y = yVelocity;
            controller.Move(finalMove * Time.fixedDeltaTime);

            RotateTowardsMovement();
        }
    }
    #endregion

    #region Custom Methods
    void HandleMovementInput()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");

        Vector3 cameraForward = mainCameraTransform.forward;
        Vector3 cameraRight = mainCameraTransform.right;

        cameraForward.y = 0;
        cameraRight.y = 0;
        cameraForward.Normalize();
        cameraRight.Normalize();

        moveDirection = cameraForward * moveY + cameraRight * moveX;

        if (moveDirection != Vector3.zero)
        {
            //if (playerAnimator != null) playerAnimator.SetBool("IsMoving", true);
        }
        else
        {
            //if (playerAnimator != null) playerAnimator.SetBool("IsMoving", false);
        }
    }

    private void RotateTowardsMovement()
    {
        Vector3 direction = new Vector3(moveDirection.x, 0, moveDirection.z);
        if (direction.magnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 12f * Time.fixedDeltaTime);
        }
    }

    void ApplyGravity()
    {
        if (controller.enabled && controller.isGrounded)
        {
            yVelocity = -0.5f;
        }
        else if (controller.enabled)
        {
            yVelocity += gravity * Time.deltaTime;
        }
    }

    public void SetCanMove(bool state)
    {
        canMove = state;
        if (!state)
        {
            moveDirection = Vector3.zero;
            //if (playerAnimator != null) playerAnimator.SetBool("IsMoving", false);
        }
    }

    public void TeleportTo(Vector3 position)
    {
        controller.enabled = false;
        transform.position = position;
        controller.enabled = true;

        yVelocity = -0.5f;
        moveDirection = Vector3.zero;
    }
    #endregion
}