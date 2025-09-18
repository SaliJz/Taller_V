using UnityEngine;
using System.Collections;

public class PlayerMovement : MonoBehaviour
{
    #region Variables

    [Header("References")]
    [SerializeField] private PlayerStatsManager statsManager;
    [SerializeField] private CharacterController controller;
    [SerializeField] private Transform mainCameraTransform;
    //[SerializeField] private Animator playerAnimator;
    //[SerializeField] private AudioSource audioSource;
    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Movimiento")]
    [Tooltip("Velocidad de movimiento por defecto si no se encuentra PlayerStatsManager.")]
    [HideInInspector] private float fallbackMoveSpeed = 5f;
    [SerializeField] private float moveSpeed = 5f;
    [Tooltip("Gravedad por defecto si no se encuentra PlayerStatsManager.")]
    [HideInInspector] private float fallbackGravity = -9.81f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 15f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;
    [Tooltip("Capas que el jugador puede atravesar durante el Dash.")]
    [SerializeField] private LayerMask traversableLayers;

    [Header("Efectos")]
    //[SerializeField] private AudioClip dashStartSound;
    //[SerializeField] private AudioClip dashImpactSound;
    [SerializeField] private GameObject afterimagePrefab;

    private int playerLayer;
    private int enemyLayer;
    private float dashCooldownTimer = 0f;
    public bool IsDashing { get; private set; }

    private Vector3 moveDirection;
    private float yVelocity;
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
        //playerAnimator = GetComponent<Animator>();
        //audioSource = GetComponentInChildren<AudioSource>();
        //trailRenderer = GetComponentInChildren<TrailRenderer>();
        playerHealth = GetComponent<PlayerHealth>();

        playerLayer = LayerMask.NameToLayer("Player");
        enemyLayer = LayerMask.NameToLayer("Enemy");

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
        if (dashCooldownTimer > 0)
        {
            dashCooldownTimer -= Time.deltaTime;
        }

        if (canMove && !IsDashing)
        {
            HandleMovementInput();
        }
        else
        {
            moveDirection = Vector3.zero;
            //if (playerAnimator != null) playerAnimator.SetBool("IsMoving", false);
        }

        ApplyGravity();

        if (Input.GetKeyDown(KeyCode.LeftShift) && dashCooldownTimer <= 0 && !IsDashing)
        {
            StartCoroutine(DashRoutine());
        }
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

    private IEnumerator DashRoutine()
    {
        IsDashing = true;
        dashCooldownTimer = dashCooldown;
        // if (playerAnimator != null) playerAnimator.SetTrigger("Dash");
        //if (audioSource != null && dashStartSound != null) audioSource.PlayOneShot(dashStartSound);

        if (playerHealth != null) playerHealth.IsInvulnerable = true;
        ToggleLayerCollisions(true);
        if (trailRenderer != null) trailRenderer.emitting = true;
        if (afterimagePrefab != null) StartCoroutine(AfterimageRoutine());

        float startTime = Time.time;
        Vector3 dashDirection = moveDirection.magnitude > 0.1f ? moveDirection : transform.forward;

        if (Physics.Raycast(transform.position, dashDirection, out RaycastHit hit, dashSpeed * dashDuration, ~traversableLayers))
        {
            float distanceToWall = hit.distance - controller.radius;
            if (distanceToWall > 0)
            {
                StartCoroutine(PerformDash(dashDirection, (distanceToWall / dashSpeed)));
            }
        }
        else
        {
            StartCoroutine(PerformDash(dashDirection, dashDuration));
        }

        yield return new WaitForSeconds(dashDuration);

        if (trailRenderer != null) trailRenderer.emitting = false;
        if (playerHealth != null) playerHealth.IsInvulnerable = false;
        ToggleLayerCollisions(false);
        IsDashing = false;
    }

    private IEnumerator PerformDash(Vector3 direction, float duration)
    {
        float startTime = Time.time;
        while (Time.time < startTime + duration)
        {
            controller.Move(direction * dashSpeed * Time.deltaTime);
            yield return null;
        }
    }

    /// <summary>
    /// Activa o desactiva las colisiones entre el jugador y las capas definidas en 'traversableLayers'.
    /// </summary>
    private void ToggleLayerCollisions(bool ignore)
    {
        for (int i = 0; i < 32; i++)
        {
            // Verifica si la capa i está en el LayerMask traversableLayers
            if (traversableLayers == (traversableLayers | (1 << i)))
            {
                Physics.IgnoreLayerCollision(playerLayer, i, ignore);
            }
        }
    }

    /// <summary>
    /// Crea afterimages del jugador a intervalos regulares mientras está dashing.
    /// </summary>
    private IEnumerator AfterimageRoutine()
    {
        float interval = 0.05f;
        while (IsDashing)
        {
            if (afterimagePrefab != null)
            {
                GameObject afterimage = Instantiate(afterimagePrefab, transform.position, transform.rotation);
                Destroy(afterimage, 0.5f);
            }
            yield return new WaitForSeconds(interval);
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

    /// <summary>
    /// Dibuja la trayectoria y el posible punto de impacto del Dash en el editor.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return; // Solo mostrar en modo juego para tener datos reales

        Gizmos.color = Color.cyan;
        Vector3 dashDirection = moveDirection.magnitude > 0.1f ? moveDirection.normalized : transform.forward;
        float dashDistance = dashSpeed * dashDuration;

        // Dibuja la línea de la trayectoria
        Gizmos.DrawRay(transform.position, dashDirection * dashDistance);

        // Dibuja una esfera en el punto de impacto si se detecta una pared
        if (Physics.Raycast(transform.position, dashDirection, out RaycastHit hit, dashDistance, ~traversableLayers))
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(hit.point, 0.5f);
        }
    }

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