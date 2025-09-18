using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    #region Variables

    [Header("References")]
    [SerializeField] private PlayerStatsManager statsManager;

    [Header("Stats")]
    [Tooltip("Velocidad de movimiento por defecto si no se encuentra PlayerStatsManager.")]
    [HideInInspector] private float fallbackMoveSpeed = 5f;
    [SerializeField] private float moveSpeed = 5f;
    [Tooltip("Gravedad por defecto si no se encuentra PlayerStatsManager.")]
    [HideInInspector] private float fallbackGravity = -9.81f;
    [SerializeField] private float gravity = -9.81f;
    //public Animator playerAnimator;

    private CharacterController controller;
    private Vector3 moveDirection;
    private float yVelocity;
    private Transform mainCameraTransform;
    private bool canMove = true;

    #endregion

    #region Unity Methods

    private void OnEnable()
    {
        PlayerStatsManager.OnStatChanged += HandleStatChanged;
    }

    private void OnDisable()
    {
        PlayerStatsManager.OnStatChanged -= HandleStatChanged;
    }

    private void Start()
    {
        statsManager = GetComponent<PlayerStatsManager>();
        if (statsManager == null) ReportDebug("StatsManager no está asignado en PlayerMovement. Usando valores de fallback.", 2);

        controller = GetComponent<CharacterController>();
        mainCameraTransform = Camera.main.transform;
        
        float moveSpeedStat = statsManager != null ? statsManager.GetStat(StatType.MoveSpeed) : fallbackMoveSpeed;
        moveSpeed = moveSpeedStat;

        float gravityStat = statsManager != null ? statsManager.GetStat(StatType.Gravity) : fallbackGravity;
    }

    /// <summary>
    /// Maneja los cambios de stats.
    /// </summary>
    /// <param name="statType">Tipo de estadística que ha cambiado.</param>
    /// <param name="newValue">Nuevo valor de la estadística.</param>
    private void HandleStatChanged(StatType statType, float newValue)
    {
        if (statType == StatType.MoveSpeed)
        {
            moveSpeed = newValue;
        }
        else if (statType == StatType.Gravity)
        {
            gravity = newValue;
        }

        ReportDebug($"Stat {statType} cambiado a {newValue}.", 1);
    }

    private void Update()
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

    private void FixedUpdate()
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

    private void HandleMovementInput()
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

    private void ApplyGravity()
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

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    /// <summary> 
    /// Función de depuración para reportar mensajes en la consola de Unity. 
    /// </summary> 
    /// <<param name="message">Mensaje a reportar.</param> >
    /// <param name="reportPriorityLevel">Nivel de prioridad: Debug, Warning, Error.</param>
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[PlayerMovement] {message}");
                break;
            case 2:
                Debug.LogWarning($"[PlayerMovement] {message}");
                break;
            case 3:
                Debug.LogError($"[PlayerMovement] {message}");
                break;
            default:
                Debug.Log($"[PlayerMovement] {message}");
                break;
        }
    }
}