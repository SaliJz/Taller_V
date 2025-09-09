using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    #region Variables
    [Header("Stats")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -9.81f;
    //public Animator playerAnimator;

    private CharacterController controller;
    private Vector3 moveDirection;
    private float yVelocity;
    private Transform mainCameraTransform;
    #endregion

    #region Unity Methods
    void Start()
    {
        controller = GetComponent<CharacterController>();
        mainCameraTransform = Camera.main.transform;
    }

    void Update()
    {
        HandleMovementInput();
        ApplyGravity();
    }

    void FixedUpdate()
    {
        Vector3 finalMove = moveDirection * moveSpeed;
        finalMove.y = yVelocity;
        controller.Move(finalMove * Time.fixedDeltaTime);
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
            transform.rotation = Quaternion.LookRotation(moveDirection);
            //playerAnimator.SetBool("IsMoving", true);
        }
        else
        {
            //playerAnimator.SetBool("IsMoving", false);
        }
    }

    void ApplyGravity()
    {
        if (controller.isGrounded)
        {
            yVelocity = -0.5f;
        }
        yVelocity += gravity * Time.deltaTime;
    }
    #endregion
}