using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour, PlayerControlls.IMovementActions
{
    #region Variables

    [Header("References")]
    [SerializeField] private PlayerStatsManager statsManager;
    [SerializeField] private CharacterController controller;
    [SerializeField] private Transform mainCameraTransform;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerAudioController playerAudioController;
    [SerializeField] private PlayerInputModifier inputModifier;

    [Header("Movement")]
    [HideInInspector] private float fallbackMoveSpeed = 5f;
    [SerializeField] private float moveSpeed = 5f;
    [HideInInspector] private float fallbackGravity = -9.81f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Audio Step Settings")]
    [SerializeField] private int level = 0;
    [SerializeField] private float stepInterval = 0.35f;
    private float stepTimer = 0f;

    public int Level
    {
        get { return level; }
        set { level = value; }
    }

    [Header("Dash")]
    [Tooltip("Distancia base del dash antes de aplicar multiplicadores.")]
    [SerializeField] private float baseDashDistance = 10f;
    [Tooltip("Cooldown base del dash antes de aplicar modificadores.")]
    [SerializeField] private float baseDashCooldown = 0.3f;
    [Tooltip("Distancia total del dash después de aplicar multiplicadores.")]
    [SerializeField] private float dashDistance = 10f;
    [Tooltip("Cooldown actual del dash después de aplicar modificadores.")]
    [SerializeField] private float dashCooldown = 0.3f;
    [Tooltip("Duración del dash en segundos.")]
    [SerializeField] private float dashDuration = 0.3f;
    [SerializeField] private LayerMask traversableLayers;
    [SerializeField] private LayerMask dashCollisionLayers;

    [Header("Dash - Gap Crossing")]
    [SerializeField] private float gapDashBonusDistance = 3f;
    [SerializeField] private LayerMask groundLayerMask;

    [Header("Edge Detection")]
    [SerializeField] private bool enableEdgeDetection = true;
    [SerializeField] private float edgeDetectionDistance = 0.5f;
    [SerializeField] private float edgeRaycastHeight = 0.2f;
    [SerializeField] private float edgeSafetyMargin = 0.05f;
    [SerializeField] private int edgeSampleMax = 6;
    [SerializeField] private float minHorizontalMagnitude = 0.01f;
    [SerializeField] private float groundTolerance = 0.15f;

    [Header("Effects")]
    [Header("Afterimage Settings")]
    [SerializeField] private GameObject afterimagePrefab;
    [SerializeField, Range(0f, 1f)] private float afterimageAlpha = 0.5f;
    [SerializeField] private float afterimageLifetime = 0.5f;

    [Header("Dash VFX")]
    [SerializeField] private ParticleSystem dashDustVFX;

    [Header("Debug")]
    [SerializeField] private bool canDebug = true;

    private PlayerControlls playerControls;
    private Vector2 currentInputVector = Vector2.zero;

    private int playerLayer;
    private float dashCooldownTimer = 0f;

    public bool IsDashing { get; private set; }
    public float DashCooldownTimer => dashCooldownTimer;
    public float MoveSpeed
    {
        get { return moveSpeed; }
        set { moveSpeed = value; }
    }

    private float prevStepOffset = 0f;
    private Vector3 moveDirection;
    private float yVelocity;
    private bool canMove = true;
    private float lastMoveX;
    private float lastMoveY;

    private float currentDashDistance;
    private float currentDashCooldown;

    private bool allowExternalForces = true;

    private bool rotationLocked = false;
    private Quaternion lockedRotation = Quaternion.identity;

    private bool isDashDisabled = false;
    public bool IsDashDisabled
    {
        get { return isDashDisabled; }
        set { isDashDisabled = value; }
    }

    private Material dashVFXMaterialInstance;

    private bool inForcedMove = false;
    private bool ignoreGravityDuringForcedMove = false;

    private Vector3 lastTargetCheck = Vector3.zero;
    private bool lastTargetHit = false;

    public bool IsRotationExternallyControlled { get; set; } = false;

    private Coroutine _dashDisableCoroutine;
    private bool hasIsAttackingParameter = false;
    private readonly Dictionary<string, float> runAnimationSpeedOverrides = new Dictionary<string, float>();

    #endregion

    #region Unity Methods

    private void OnEnable()
    {
        PlayerStatsManager.OnStatChanged += HandleStatChanged;

        playerControls?.Movement.Enable();
    }

    private void OnDisable()
    {
        PlayerStatsManager.OnStatChanged -= HandleStatChanged;

        playerControls?.Movement.Disable();
    }

    private void OnDestroy()
    {
        PlayerStatsManager.OnStatChanged -= HandleStatChanged;

        if (dashDustVFX != null)
        {
            dashDustVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            dashDustVFX.Clear(true);
        }

        if (dashVFXMaterialInstance != null)
        {
            Destroy(dashVFXMaterialInstance);
            dashVFXMaterialInstance = null;
        }

        StopAllCoroutines();
    }

    private void Awake()
    {
        playerControls = new PlayerControlls();
        playerControls.Movement.SetCallbacks(this); 
    }

    private void Start()
    {
        statsManager = GetComponent<PlayerStatsManager>();
        if (statsManager == null) ReportDebug("StatsManager no está asignado en PlayerMovement. Usando valores de fallback.", 2);

        controller = GetComponent<CharacterController>();
        mainCameraTransform = Camera.main != null ? Camera.main.transform : mainCameraTransform;
        playerAnimator = GetComponentInChildren<Animator>();
        playerHealth = GetComponent<PlayerHealth>();
        if (playerAudioController == null) playerAudioController = GetComponent<PlayerAudioController>();
        hasIsAttackingParameter = AnimatorHasParameter(playerAnimator, "IsAttacking");

        playerLayer = LayerMask.NameToLayer("Player");

        float moveSpeedStat = statsManager != null ? statsManager.GetStat(StatType.MoveSpeed) : fallbackMoveSpeed;
        moveSpeed = moveSpeedStat;

        float gravityStat = statsManager != null ? statsManager.GetStat(StatType.Gravity) : fallbackGravity;
        gravity = gravityStat;

        lastMoveY = -1;
        lastMoveX = 0;

        InitializeDashVFX();
        UpdateDashStatsFromManager();
        UpdateMovementAnimationSpeed(false);
    }

    private void UpdateDashStatsFromManager()
    {
        if (statsManager == null)
        {
            currentDashDistance = baseDashDistance;
            currentDashCooldown = baseDashCooldown;
            return;
        }

        float dashRangeMultiplier = statsManager.GetStat(StatType.DashRangeMultiplier);
        if (dashRangeMultiplier <= 0f) dashRangeMultiplier = 1f; 

        currentDashDistance = baseDashDistance * dashRangeMultiplier;

        float dashCooldownMod = statsManager.GetStat(StatType.DashCooldownPost);
        currentDashCooldown = Mathf.Max(0.1f, baseDashCooldown + dashCooldownMod);

        ReportDebug($"Dash Stats actualizados: Distancia={currentDashDistance} (base:{baseDashDistance} x {dashRangeMultiplier}), Cooldown={currentDashCooldown} (base:{baseDashCooldown} + {dashCooldownMod}s)", 1);
    }

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
        else if (statType == StatType.DashRangeMultiplier || statType == StatType.DashCooldownPost)
        {
            UpdateDashStatsFromManager();
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
            if (playerAnimator != null) playerAnimator.SetBool("Running", false);
            UpdateMovementAnimationSpeed(false);
        }

        ApplyGravity();
    }

    private void FixedUpdate()
    {
        if (IsDashing) return;

        if (controller.enabled)
        {
            Vector3 finalMove = moveDirection * moveSpeed;
            finalMove.y = yVelocity;

            if (enableEdgeDetection && moveDirection.magnitude > 0.1f)
            {
                finalMove = ApplyEdgeDetection(finalMove);
            }

            controller.Move(finalMove * Time.fixedDeltaTime);

            if (IsRotationExternallyControlled)
            {
                return;
            }

            RotateTowardsMovement();
        }
    }

    #endregion

    #region Custom Methods
    public void OnMove(InputAction.CallbackContext context)
    {
        Vector2 rawInput = context.ReadValue<Vector2>();

        if (inputModifier != null)
        {
            currentInputVector = inputModifier.ProcessInput(rawInput);
        }
        else
        {
            currentInputVector = rawInput;
        }
    }

    private void HandleMovementInput()
    {
        float moveX = currentInputVector.x;
        float moveY = currentInputVector.y;

        Vector3 cameraForward = mainCameraTransform != null ? mainCameraTransform.forward : Vector3.forward;
        Vector3 cameraRight = mainCameraTransform != null ? mainCameraTransform.right : Vector3.right;

        cameraForward.y = 0;
        cameraRight.y = 0;
        cameraForward.Normalize();  
        cameraRight.Normalize();

        moveDirection = (cameraForward * moveY + cameraRight * moveX).normalized;

        bool hasInput = moveDirection.magnitude > 0.1f;
        bool timeIsRunning = Time.timeScale > 0.01f;
        bool isMoving = hasInput && timeIsRunning;

        if (playerAnimator != null) playerAnimator.SetBool("Running", isMoving);
        UpdateMovementAnimationSpeed(isMoving);

        if (playerAudioController != null)
        {
            if (isMoving && controller.isGrounded)
            {
                HandleFootstepsTimer();
            }
            else
            {
                playerAudioController.StopFootsteps();
                stepTimer = 0f;
            }
        }

        if (timeIsRunning)
        {
            if (!rotationLocked)
            {
                if (hasInput)
                {
                    lastMoveX = Mathf.Round(moveX);
                    lastMoveY = Mathf.Round(moveY);
                }

                if (playerAnimator != null)
                {
                    playerAnimator.SetFloat("Xaxis", lastMoveX);
                    playerAnimator.SetFloat("Yaxis", lastMoveY);
                }
            }
        }
    }

    private void HandleFootstepsTimer()
    {
        stepTimer -= Time.deltaTime;

        if (stepTimer <= 0f)
        {
            if (playerAudioController != null)
            {
                playerAudioController.PlayStepSound(level);
            }

            stepTimer = stepInterval;
        }
    }

    private void RotateTowardsMovement()
    {
        if (rotationLocked)
        {

            transform.rotation = Quaternion.Slerp(transform.rotation, lockedRotation, 12f * Time.fixedDeltaTime);
            return;
        }

        Vector3 direction = new Vector3(moveDirection.x, 0, moveDirection.z);
        if (direction.magnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 12f * Time.fixedDeltaTime);
        }
    }
    public IEnumerator ExecuteDashFromManager()
    {
        yield return StartCoroutine(DashRoutine());
    }

    private IEnumerator DashRoutine()
    {
        Vector3 dashDirection;

        if (currentInputVector.sqrMagnitude > 0.01f)
        {
            Vector3 cameraForward = mainCameraTransform != null ? mainCameraTransform.forward : Vector3.forward;
            Vector3 cameraRight = mainCameraTransform != null ? mainCameraTransform.right : Vector3.right;

            cameraForward.y = 0;
            cameraRight.y = 0;
            cameraForward.Normalize();
            cameraRight.Normalize();

            dashDirection = (cameraForward * currentInputVector.y + cameraRight * currentInputVector.x).normalized;
            transform.rotation = Quaternion.LookRotation(dashDirection);
        }
        else
        {
            dashDirection = moveDirection.magnitude > 0.1f ? moveDirection : transform.forward;
        }

        if (!ValidateDashPath(dashDirection, out Vector3 targetDashPosition))
        {
            yield break;
        }

        IsDashing = true;
        yVelocity = 0f;
        moveDirection = Vector3.zero;
        if (playerHealth != null) playerHealth.IsInvulnerable = true;
        ToggleLayerCollisions(true);

        if (playerAnimator != null)
        {
            playerAnimator.SetBool("Dashing", true);
        }

        if (playerAudioController != null)
        {
            playerAudioController.PlayDashSound();
        }

        if (dashDustVFX != null) PlayDashVFX(true);
        if (afterimagePrefab != null) StartCoroutine(AfterimageRoutine());

        yield return StartCoroutine(PerformDash(targetDashPosition, dashDuration));

        float safetyPushSpeed = (currentDashDistance / dashDuration) * 0.4f;
        float maxStuckTime = 0.3f;
        float stuckTimer = 0f;
        int maxPushAttempts = 5;
        int pushAttempts = 0;

        Vector3 capsuleCenter = transform.position + controller.center;
        Vector3 p1 = capsuleCenter + Vector3.up * (controller.height / 2f - controller.radius);
        Vector3 p2 = capsuleCenter - Vector3.up * (controller.height / 2f - controller.radius);

        while (Physics.CheckCapsule(p1, p2, controller.radius, traversableLayers, QueryTriggerInteraction.Ignore)
               && stuckTimer < maxStuckTime
               && pushAttempts < maxPushAttempts)
        {
            Vector3 pushDelta = dashDirection * safetyPushSpeed * Time.deltaTime;
            Vector3 nextPos = transform.position + pushDelta;

            if (!IsPositionSafeForCapsule(nextPos))
            {
                ReportDebug("Safety push detenido: posición insegura detectada.", 1);
                break;
            }

            controller.Move(pushDelta);
            stuckTimer += Time.deltaTime;
            pushAttempts++;

            capsuleCenter = transform.position + controller.center;
            p1 = capsuleCenter + Vector3.up * (controller.height / 2f - controller.radius);
            p2 = capsuleCenter - Vector3.up * (controller.height / 2f - controller.radius);

            yield return null;
        }

        if (dashDustVFX != null) PlayDashVFX(false);
        ToggleLayerCollisions(false);
        if (playerHealth != null) playerHealth.IsInvulnerable = false;

        IsDashing = false;

        if (playerAnimator != null)
        {
            playerAnimator.SetBool("Dashing", false);
        }

        dashCooldownTimer = currentDashCooldown;
    }

    private void InitializeDashVFX()
    {
        if (dashDustVFX == null) return;

        dashVFXMaterialInstance = new Material(dashDustVFX.GetComponent<ParticleSystemRenderer>().sharedMaterial);
        dashDustVFX.GetComponent<ParticleSystemRenderer>().material = dashVFXMaterialInstance;

        dashDustVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        dashDustVFX.Clear(true);
    }

    private void PlayDashVFX(bool active)
    {
        if (dashDustVFX == null) return;
        var emission = dashDustVFX.emission;
        emission.enabled = active;

        if (active)
        {
            if (!dashDustVFX.isPlaying) dashDustVFX.Play();
        }
        else
        {
            dashDustVFX.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            dashDustVFX.Clear(false);
        }
    }

    private IEnumerator PerformDash(Vector3 targetPosition, float duration)
    {
        float savedGravity = gravity;
        float savedYVelocity = yVelocity;

        gravity = 0f;
        yVelocity = 0f;

        float originalStepOffset = controller.stepOffset;
        controller.stepOffset = Mathf.Max(originalStepOffset, 0.5f);

        Vector3 startPosition = transform.position;
        float startY = transform.position.y;

        Vector3 startPosXZ = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 targetPosXZ = new Vector3(targetPosition.x, 0f, targetPosition.z);

        float startTime = Time.time;
        float endTime = startTime + duration;
        float journeyLength = Vector3.Distance(startPosXZ, targetPosXZ);

        if (journeyLength < 0.1f)
        {
            gravity = savedGravity;
            yVelocity = savedYVelocity;
            if (controller != null) controller.stepOffset = prevStepOffset;
            yield break;
        }

        while (Time.time < endTime)
        {
            float t = Mathf.Clamp01((Time.time - startTime) / duration);
            Vector3 lerpedXZ = Vector3.Lerp(startPosXZ, targetPosXZ, t);

            Vector3 current = transform.position;
            Vector3 currentFixedY = new Vector3(current.x, startY, current.z);
            Vector3 next = new Vector3(lerpedXZ.x, startY, lerpedXZ.z);

            Vector3 delta = next - currentFixedY;

            delta.y = 0f;

            controller.Move(delta);

            yVelocity = 0f;

            yield return null;
        }

        Vector3 finalXZ = new Vector3(targetPosXZ.x, startY, targetPosXZ.z);
        Vector3 finalCurrent = transform.position;
        Vector3 finalCurrentFixedY = new Vector3(finalCurrent.x, startY, finalCurrent.z);
        Vector3 finalDelta = finalXZ - finalCurrentFixedY;
        finalDelta.y = 0f;
        controller.Move(finalDelta);

        gravity = savedGravity;
        yVelocity = savedYVelocity;
        if (controller != null) controller.stepOffset = prevStepOffset;
    }

    private void ToggleLayerCollisions(bool ignore)
    {
        for (int i = 0; i < 32; i++)
        {
            if (traversableLayers == (traversableLayers | (1 << i)))
            {
                Physics.IgnoreLayerCollision(playerLayer, i, ignore);
            }
        }
    }

    private Vector3 ApplyEdgeDetection(Vector3 movement)
    {

        if (!controller.isGrounded)
        {
            return movement;
        }


        Vector3 horizontalMovement = new Vector3(movement.x, 0f, movement.z);
        if (horizontalMovement.magnitude < 0.01f)
        {
            return movement;
        }

        Vector3 movementDirection = horizontalMovement.normalized;
        Vector3 rayOrigin = transform.position + Vector3.up * edgeRaycastHeight;
        Vector3 checkPosition = rayOrigin + movementDirection * (controller.radius + edgeDetectionDistance);


        float rayDistance = controller.height + 0.5f;

        if (!Physics.Raycast(checkPosition, Vector3.down, rayDistance, groundLayerMask, QueryTriggerInteraction.Ignore))
        {
            ReportDebug("Borde detectado. Bloqueando movimiento para prevenir caída.", 1);


            return new Vector3(0f, movement.y, 0f);
        }


        return movement;
    }
    private bool ValidateDashPath(Vector3 direction, out Vector3 finalPosition)
    {
        Vector3 origin = transform.position;
        float playerRadius = controller.radius;
        float playerHeight = controller.height;

        Vector3 p1 = origin + controller.center + Vector3.up * (playerHeight / 2f - playerRadius);
        Vector3 p2 = origin + controller.center - Vector3.up * (playerHeight / 2f - playerRadius);


        if (Physics.CapsuleCast(p1, p2, playerRadius, direction, out RaycastHit obstacleHit, currentDashDistance, dashCollisionLayers, QueryTriggerInteraction.Ignore))
        {
            float adjustedDistance = Mathf.Max(0f, obstacleHit.distance - playerRadius);

            if (!HasGroundAlongPath(direction, adjustedDistance))
            {
                finalPosition = origin;
                lastTargetCheck = origin;
                lastTargetHit = false;
                ReportDebug("Dash cancelado: vacío detectado entre jugador y obstáculo.", 2);
                return false;
            }

            finalPosition = origin + direction * adjustedDistance;
            lastTargetCheck = finalPosition;
            lastTargetHit = true;
            ReportDebug("El camino está bloqueado por un obstáculo. Ajustando la distancia.", 1);
            return true;
        }


        float scanHeight = 2.0f;
        Vector3 baseTarget = origin + direction * currentDashDistance + Vector3.up * scanHeight;

        if (Physics.Raycast(baseTarget, Vector3.down, out RaycastHit baseGroundHit, playerHeight + scanHeight + 1f, groundLayerMask, QueryTriggerInteraction.Ignore))
        {
            finalPosition = baseGroundHit.point;
            lastTargetCheck = finalPosition;
            lastTargetHit = true;
            ReportDebug("Dash estándar seguro.", 1);
            return true;
        }


        float scanStart = currentDashDistance + 0.1f;
        float scanMax = currentDashDistance + gapDashBonusDistance;
        float scanStep = Mathf.Max(0.5f, playerRadius * 0.5f);

        RaycastHit foundHit = new RaycastHit();
        bool found = false;

        for (float t = scanStart; t <= scanMax; t += scanStep)
        {
            Vector3 probe = origin + direction * t + Vector3.up * scanHeight;
            if (Physics.Raycast(probe, Vector3.down, out RaycastHit gh, playerHeight + scanHeight + 1f, groundLayerMask, QueryTriggerInteraction.Ignore))
            {
                foundHit = gh;
                found = true;
                break;
            }
        }

        if (found)
        {
            float safetyPenetration = playerRadius + 0.3f;
            Vector3 idealLandingPos = foundHit.point + (direction.normalized * safetyPenetration);

            if (IsPositionSafeForCapsule(idealLandingPos))
            {
                finalPosition = idealLandingPos;
                lastTargetCheck = finalPosition;
                lastTargetHit = true;
                ReportDebug("Aterrizaje profundo seguro.", 1);
                return true;
            }

            Vector3 midPoint = Vector3.Lerp(foundHit.point, idealLandingPos, 0.5f);
            if (IsPositionSafeForCapsule(midPoint))
            {
                finalPosition = midPoint;
                lastTargetCheck = finalPosition;
                lastTargetHit = true;
                ReportDebug("Aterrizaje ajustado (Punto medio).", 1);
                return true;
            }

            finalPosition = foundHit.point + (direction.normalized * 0.05f);
            lastTargetCheck = finalPosition;
            lastTargetHit = true;
            ReportDebug("Aterrizaje en borde crítico (Plataforma pequeña).", 2);
            return true;
        }


        float safeLedgeDistance = GetMaxSafeDistance(direction, currentDashDistance);

        float minEffectiveDashDistance = 0.5f;

        if (safeLedgeDistance > edgeSafetyMargin)
        {
            if (safeLedgeDistance < minEffectiveDashDistance)
            {
                finalPosition = origin;
                ReportDebug("Dash bloqueado por borde (Distancia segura insuficiente).", 1);
                return true;
            }

            Vector3 safeTarget = origin + direction * safeLedgeDistance + Vector3.up * scanHeight;
            if (Physics.Raycast(safeTarget, Vector3.down, out RaycastHit ledgeGroundHit, playerHeight + scanHeight + 1f, groundLayerMask, QueryTriggerInteraction.Ignore))
            {
                finalPosition = ledgeGroundHit.point;
                lastTargetCheck = finalPosition;
                lastTargetHit = true;
                ReportDebug("Dash limitado por borde (Safe Ledge).", 1);
                return true;
            }

            finalPosition = origin + direction * safeLedgeDistance;
            lastTargetCheck = finalPosition;
            lastTargetHit = true;
            return true;
        }


        finalPosition = origin;
        lastTargetCheck = origin;
        lastTargetHit = false;
        ReportDebug("Dash cancelado por inseguridad total.", 2);
        return true;
    }

    private IEnumerator AfterimageRoutine()
    {
        float interval = 0.05f;

        while (IsDashing)
        {
            if (afterimagePrefab != null)
            {
                SpriteRenderer srcSprite = GetComponentInChildren<SpriteRenderer>();

                if (srcSprite != null)
                {
                    GameObject afterimage = Instantiate(afterimagePrefab, srcSprite.transform.position, srcSprite.transform.rotation);

                    afterimage.transform.localScale = srcSprite.transform.lossyScale;

                    SpriteRenderer dstSprite = afterimage.GetComponent<SpriteRenderer>();

                    if (dstSprite != null)
                    {

                        dstSprite.sprite = srcSprite.sprite;
                        dstSprite.flipX = srcSprite.flipX;
                        dstSprite.flipY = srcSprite.flipY;
                        Color color = srcSprite.color;
                        color.a = afterimageAlpha;
                        dstSprite.color = color;

                        dstSprite.sortingLayerID = srcSprite.sortingLayerID;
                        dstSprite.sortingOrder = srcSprite.sortingOrder - 1;
                    }
                    Destroy(afterimage, afterimageLifetime);
                }
            }
            yield return new WaitForSeconds(interval);
        }
    }

    public void DisableDashForDuration(float duration)
    {
        if (_dashDisableCoroutine != null)
        {
            StopCoroutine(_dashDisableCoroutine);
        }

        _dashDisableCoroutine = StartCoroutine(DashDisableRoutine(duration));
    }

    private IEnumerator DashDisableRoutine(float duration)
    {
        IsDashDisabled = true;

        yield return new WaitForSeconds(duration);

        IsDashDisabled = false;
        _dashDisableCoroutine = null;
    }

    private void ApplyGravity()
    {
        if (IsDashing) return;

        if (inForcedMove && ignoreGravityDuringForcedMove)
        {
            yVelocity = -0.5f;
            return;
        }

        if (controller.enabled && controller.isGrounded)
        {
            yVelocity = -0.5f;
        }
        else if (controller.enabled)
        {
            yVelocity += gravity * Time.deltaTime;
        }
    }

    public void CancelDash()
    {
        if (!IsDashing) return;

        StopAllCoroutines();

        IsDashing = false;

        if (playerAnimator != null)
        {
            playerAnimator.SetBool("Dashing", false);
        }

        if (dashDustVFX != null) PlayDashVFX(false);

        ToggleLayerCollisions(false);

        if (playerHealth != null) playerHealth.IsInvulnerable = false;

        if (controller != null && prevStepOffset > 0)
        {
            controller.stepOffset = prevStepOffset;
            prevStepOffset = 0f;
        }

        yVelocity = 0f;

        ReportDebug("Dash cancelado.", 1);
    }

    public void SetCanMove(bool state)
    {
        canMove = state;

        if (!state)
        {
            moveDirection = Vector3.zero;

            if (playerAnimator != null)
            {
                playerAnimator.SetBool("Running", false);
            }

            UpdateMovementAnimationSpeed(false);
        }
    }

    private void UpdateMovementAnimationSpeed(bool isMoving)
    {
        if (playerAnimator == null)
        {
            return;
        }

        if (hasIsAttackingParameter && playerAnimator.GetBool("IsAttacking"))
        {
            return;
        }

        float targetAnimatorSpeed = 1f;

        if (isMoving && runAnimationSpeedOverrides.Count > 0)
        {
            targetAnimatorSpeed = GetLowestRunAnimationSpeedOverride();
        }

        playerAnimator.speed = targetAnimatorSpeed;
    }

    private bool AnimatorHasParameter(Animator animator, string parameterName)
    {
        if (animator == null)
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    public void SetRunAnimationSpeedOverride(string key, float animatorSpeed)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        runAnimationSpeedOverrides[key] = Mathf.Clamp(animatorSpeed, 0.1f, 3f);
        UpdateMovementAnimationSpeed(playerAnimator != null && playerAnimator.GetBool("Running"));
    }

    public void ClearRunAnimationSpeedOverride(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        runAnimationSpeedOverrides.Remove(key);
        UpdateMovementAnimationSpeed(playerAnimator != null && playerAnimator.GetBool("Running"));
    }

    private float GetLowestRunAnimationSpeedOverride()
    {
        float lowestSpeed = 1f;

        foreach (float overrideSpeed in runAnimationSpeedOverrides.Values)
        {
            if (overrideSpeed < lowestSpeed)
            {
                lowestSpeed = overrideSpeed;
            }
        }

        return lowestSpeed;
    }

    public void TeleportTo(Vector3 position)
    {
        controller.enabled = false;
        transform.position = position;
        controller.enabled = true;

        yVelocity = -0.5f;
        moveDirection = Vector3.zero;
    }

    public bool IsEffectivelyGrounded()
    {
        if (controller == null) return false;
        if (controller.isGrounded) return true;

        Vector3 origin = transform.position + Vector3.up * 0.1f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit h, groundTolerance + 0.05f, groundLayerMask, QueryTriggerInteraction.Ignore))
        {
            return true;
        }
        return false;
    }

    public bool IsPositionSafeForCapsule(Vector3 pos)
    {
        float checkRadius = controller.radius * 0.95f;
        float rayLength = controller.height + 1.0f;


        Vector3[] checkPoints = new Vector3[]
        {
        pos,
        pos + Vector3.forward * checkRadius,
        pos + Vector3.back * checkRadius,
        pos + Vector3.right * checkRadius,
        pos + Vector3.left * checkRadius,
        pos + (Vector3.forward + Vector3.right).normalized * checkRadius,
        pos + (Vector3.forward + Vector3.left).normalized * checkRadius,
        pos + (Vector3.back + Vector3.right).normalized * checkRadius,
        pos + (Vector3.back + Vector3.left).normalized * checkRadius
        };

        foreach (var p in checkPoints)
        {
            Vector3 origin = p + Vector3.up * edgeRaycastHeight;

            if (!Physics.Raycast(origin, Vector3.down, rayLength, groundLayerMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }
        }

        return true;
    }

    private bool HasGroundAlongPath(Vector3 direction, float distance)
    {
        if (distance < 0.1f) return true;

        int samples = Mathf.CeilToInt(distance / 0.5f);
        samples = Mathf.Clamp(samples, 3, 15);

        float step = distance / samples;

        for (int i = 1; i <= samples; i++)
        {
            Vector3 checkPos = transform.position + direction * (step * i) + Vector3.up * 2f;

            if (!Physics.Raycast(checkPos, Vector3.down, controller.height + 3f, groundLayerMask, QueryTriggerInteraction.Ignore))
            {
                ReportDebug($"Vacío detectado en el camino a {(step * i):F2}m de {distance:F2}m totales.", 2);
                return false;
            }
        }

        return true;
    }

    public float GetMaxSafeDistance(Vector3 dir, float maxDesiredDistance)
    {
        if (!enableEdgeDetection || controller == null || !IsEffectivelyGrounded()) return maxDesiredDistance;

        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return 0f;
        dir.Normalize();

        int samples = Mathf.Clamp(Mathf.CeilToInt((maxDesiredDistance) / (controller.radius * 0.5f)), 2, edgeSampleMax);
        float step = maxDesiredDistance / samples;

        Vector3 basePos = transform.position;

        for (int i = 1; i <= samples; i++)
        {
            float traveled = step * i;
            Vector3 testPos = basePos + dir * traveled;

            if (!IsPositionSafeForCapsule(testPos))
            {
                float safeDistance = Mathf.Max(0f, (step * (i - 1)) - edgeSafetyMargin);
                return safeDistance;
            }
        }

        return maxDesiredDistance;
    }

    private Vector3 ComputeSafeHorizontalDisplacement(Vector3 desiredDisplacement)
    {
        Vector3 horizontal = new Vector3(desiredDisplacement.x, 0f, desiredDisplacement.z);
        float mag = horizontal.magnitude;
        if (mag < minHorizontalMagnitude || controller == null) return desiredDisplacement;

        Vector3 dir = horizontal.normalized;
        int samples = Mathf.Clamp(Mathf.CeilToInt((mag) / (controller.radius + edgeDetectionDistance)), 1, edgeSampleMax);
        float step = mag / samples;
        Vector3 rayOriginBase = transform.position + Vector3.up * edgeRaycastHeight;
        float rayDistance = controller.height + 0.6f;

        for (int i = 1; i <= samples; i++)
        {
            float traveled = step * i;
            Vector3 samplePos = rayOriginBase + dir * Mathf.Max(0f, traveled + controller.radius + edgeDetectionDistance);

            if (!Physics.Raycast(samplePos, Vector3.down, rayDistance, groundLayerMask, QueryTriggerInteraction.Ignore))
            {
                float safeDistance = Mathf.Max(0f, (step * (i - 1)) - edgeSafetyMargin);
                Vector3 safeHorizontal = dir * safeDistance;
                return new Vector3(safeHorizontal.x, desiredDisplacement.y, safeHorizontal.z);
            }
        }

        return desiredDisplacement;
    }

    private Vector3 TryComputeSlideAlongEdge(Vector3 forwardDir, float maxAllowedForwardDistance)
    {
        Vector3 right = Vector3.Cross(Vector3.up, forwardDir).normalized;
        Vector3[] lateralDirs = new Vector3[] { right, -right };
        Vector3 rayOriginBase = transform.position + Vector3.up * edgeRaycastHeight;
        float rayDistance = controller.height + 0.6f;
        float lateralMagnitude = Mathf.Max(controller.radius, 0.25f);

        foreach (var lat in lateralDirs)
        {
            Vector3 testPos = rayOriginBase + forwardDir * (maxAllowedForwardDistance + controller.radius) + lat * lateralMagnitude;
            if (Physics.Raycast(testPos, Vector3.down, rayDistance, groundLayerMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 combined = forwardDir * maxAllowedForwardDistance + lat * lateralMagnitude;
                return combined;
            }
        }

        return Vector3.zero;
    }
    private Vector3 ApplyEdgeDetectionToDisplacement(Vector3 displacement)
    {
        if (!enableEdgeDetection || controller == null || !IsEffectivelyGrounded()) return displacement;

        Vector3 horizontal = new Vector3(displacement.x, 0f, displacement.z);
        if (horizontal.magnitude < minHorizontalMagnitude) return displacement;

        Vector3 safe = ComputeSafeHorizontalDisplacement(displacement);
        Vector3 desiredHorizontal = new Vector3(displacement.x, 0f, displacement.z);
        Vector3 safeHorizontal = new Vector3(safe.x, 0f, safe.z);

        if (safeHorizontal.sqrMagnitude + 0.0001f < desiredHorizontal.sqrMagnitude)
        {
            ReportDebug("Borde detectado durante ataque. Desplazamiento recortado", 1);

            Vector3 slide = TryComputeSlideAlongEdge(desiredHorizontal.normalized, safeHorizontal.magnitude);
            if (slide.sqrMagnitude > 0.0001f)
            {

                return new Vector3(slide.x, displacement.y, slide.z);
            }

            return new Vector3(0f, displacement.y, 0f);
        }

        return displacement;
    }
    public void MoveCharacter(Vector3 displacement)
    {
        if (controller != null && controller.enabled)
        {

            if (enableEdgeDetection && controller.isGrounded)
            {
                displacement = ApplyEdgeDetectionToDisplacement(displacement);
            }

            controller.Move(displacement);
        }
    }

    public bool IsMovementSafeDirection(Vector3 dir, float distance)
    {
        if (!enableEdgeDetection || controller == null) return true;
        if (!IsEffectivelyGrounded()) return false;

        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return true;
        dir.Normalize();

        Vector3 rayOrigin = transform.position + Vector3.up * edgeRaycastHeight;
        Vector3 targetCheck = rayOrigin + dir * (Mathf.Max(0f, distance) + controller.radius + edgeDetectionDistance);
        float rayDistance = controller.height + 0.6f;

        if (Physics.Raycast(targetCheck, Vector3.down, rayDistance, groundLayerMask, QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        return false;
    }

    public void LockFacingTo8Directions(Vector3 worldDirection, bool setAnimatorAxes = true)
    {
        if (worldDirection.sqrMagnitude < 0.0001f) return;

        Vector3 dir = worldDirection;
        dir.y = 0f;
        dir.Normalize();

        float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        float snapped = Mathf.Round(angle / 45f) * 45f;
        Quaternion target = Quaternion.Euler(0f, snapped, 0f);

        rotationLocked = true;
        lockedRotation = target;

        if (setAnimatorAxes && playerAnimator != null && mainCameraTransform != null)
        {
            Vector3 camForward = mainCameraTransform.forward;
            Vector3 camRight = mainCameraTransform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 snappedDir = target * Vector3.forward;
            float x = Mathf.Round(Vector3.Dot(snappedDir, camRight));
            float y = Mathf.Round(Vector3.Dot(snappedDir, camForward));

            lastMoveX = x;
            lastMoveY = y;

            playerAnimator.SetFloat("Xaxis", lastMoveX);
            playerAnimator.SetFloat("Yaxis", lastMoveY);

        }
    }
    public void UnlockFacing()
    {


        rotationLocked = false;

        if (playerAnimator != null && mainCameraTransform != null)
        {
            Vector3 camForward = mainCameraTransform.forward;
            Vector3 camRight = mainCameraTransform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            if (moveDirection.magnitude > 0.1f)
            {
                Vector3 currentDir = moveDirection.normalized;
                float x = Mathf.Round(Vector3.Dot(currentDir, camRight));
                float y = Mathf.Round(Vector3.Dot(currentDir, camForward));

                lastMoveX = x;
                lastMoveY = y;
            }
            else
            {

            }

            playerAnimator.SetFloat("Xaxis", lastMoveX);
            playerAnimator.SetFloat("Yaxis", lastMoveY);

        }
    }

    public void StartForcedMovement(bool ignoreGravity)
    {
        inForcedMove = true;
        ignoreGravityDuringForcedMove = ignoreGravity;
        allowExternalForces = false;
        if (ignoreGravity)
        {
            yVelocity = -0.5f;
        }
    }

    public void StopForcedMovement()
    {
        inForcedMove = false;
        ignoreGravityDuringForcedMove = false;
        allowExternalForces = true;
    }

    public void SetExternalForcesAllowed(bool allowed) { allowExternalForces = allowed; }
    public Quaternion GetLockedRotation()
    {
        return lockedRotation;
    }
    public void ForceApplyLockedRotation()
    {
        transform.rotation = lockedRotation;
    }

    #endregion

    #region Debugging

    #region Debugging

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR 
        if (!Application.isPlaying || controller == null) return;
        if (!canDebug) return;

        Vector3 origin = transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(origin, 0.15f);

        Vector3 dashDirection = moveDirection.magnitude > 0.1f ? moveDirection.normalized : transform.forward;

        Gizmos.color = Color.yellow;
        Vector3 baseEnd = origin + dashDirection * dashDistance;
        Gizmos.DrawLine(origin, baseEnd);
        Gizmos.DrawWireSphere(baseEnd, 0.15f);

        Gizmos.color = Color.magenta;
        Vector3 bonusEnd = origin + dashDirection * (currentDashDistance + gapDashBonusDistance);
        Gizmos.DrawLine(baseEnd, bonusEnd);
        Gizmos.DrawWireSphere(bonusEnd, 0.1f);

        float scanHeight = 2.0f;
        Vector3 downOrigin = baseEnd + Vector3.up * scanHeight;
        float downRayMax = controller.height + scanHeight + 1f;
        if (Physics.Raycast(downOrigin, Vector3.down, out RaycastHit baseGroundHit, downRayMax, groundLayerMask))
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(downOrigin, baseGroundHit.point);
            Gizmos.DrawWireSphere(baseGroundHit.point, 0.2f);
        }
        else
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(downOrigin, downOrigin + Vector3.down * downRayMax);
        }

        if (enableEdgeDetection && controller.isGrounded && moveDirection.magnitude > 0.1f)
        {
            Vector3 horizontalMovement = new Vector3(moveDirection.x, 0f, moveDirection.z);
            Vector3 movementDirection = horizontalMovement.normalized;

            Vector3 rayOrigin = transform.position + Vector3.up * edgeRaycastHeight;
            float rayDistance = controller.height + 0.6f;

            float totalDistance = currentDashDistance + gapDashBonusDistance;
            int samples = Mathf.Clamp(Mathf.CeilToInt(totalDistance / (controller.radius + edgeDetectionDistance)), 1, edgeSampleMax);
            float step = totalDistance / samples;

            Vector3 firstCheck = rayOrigin + movementDirection * (controller.radius + edgeDetectionDistance);
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(rayOrigin, firstCheck);
            Gizmos.DrawWireSphere(firstCheck, 0.1f);

            for (int i = 1; i <= samples; i++)
            {
                float traveled = step * i;
                Vector3 samplePos = rayOrigin + movementDirection * Mathf.Max(0f, traveled + controller.radius + edgeDetectionDistance);

                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(samplePos, Mathf.Max(0.03f, controller.radius * 0.2f));

                if (Physics.Raycast(samplePos, Vector3.down, out RaycastHit hit, rayDistance, groundLayerMask, QueryTriggerInteraction.Ignore))
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(samplePos, hit.point);
                    Gizmos.DrawWireSphere(hit.point, 0.12f);
                }
                else
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(samplePos, samplePos + Vector3.down * rayDistance);
                    Gizmos.DrawWireSphere(samplePos + Vector3.down * rayDistance, 0.12f);
                }
            }
        }

        if (lastTargetCheck != Vector3.zero)
        {
            Gizmos.color = lastTargetHit ? Color.green : Color.red;
            Gizmos.DrawWireSphere(lastTargetCheck, 0.12f);
            Gizmos.DrawLine(lastTargetCheck, lastTargetCheck + Vector3.down * (controller.height + 0.6f));

            UnityEditor.Handles.Label(lastTargetCheck + Vector3.up * 0.2f, lastTargetHit ? "lastTargetCheck hit" : "lastTargetCheck miss");
        }

        UnityEditor.Handles.Label(origin + Vector3.up * 1.5f, "Origen de Dash");
        UnityEditor.Handles.Label(baseEnd + Vector3.up * 1.5f, "Alcance base");
        UnityEditor.Handles.Label(bonusEnd + Vector3.up * 1.5f, "Máximo con bonificación");

        if (enableEdgeDetection && controller.isGrounded)
        {
            UnityEditor.Handles.Label(origin + Vector3.up * 2.0f, "Detección de bordes activada");
        }
        else
        {
            UnityEditor.Handles.Label(origin + Vector3.up * 2.0f, "Detección de bordes desactivada");
        }

        Vector3 testDir = moveDirection.magnitude > 0.1f ? moveDirection.normalized : transform.forward;
        Vector3 testP1 = origin + controller.center + Vector3.up * (controller.height / 2f - controller.radius);
        Vector3 testP2 = origin + controller.center - Vector3.up * (controller.height / 2f - controller.radius);

        if (Physics.CapsuleCast(testP1, testP2, controller.radius, testDir, out RaycastHit obsHit,
            currentDashDistance, dashCollisionLayers, QueryTriggerInteraction.Ignore))
        {
            float checkDist = Mathf.Max(0f, obsHit.distance - controller.radius);
            int samples = Mathf.CeilToInt(checkDist / 1f);
            samples = Mathf.Clamp(samples, 2, 10);
            float step = checkDist / samples;

            for (int i = 1; i <= samples; i++)
            {
                Vector3 checkPos = origin + testDir * (step * i);
                Vector3 rayStart = checkPos + Vector3.up * 2f;
                bool hasGround = Physics.Raycast(rayStart, Vector3.down, controller.height + 3f,
                    groundLayerMask, QueryTriggerInteraction.Ignore);

                Gizmos.color = hasGround ? new Color(0, 1, 0, 0.7f) : new Color(1, 0, 0, 0.9f);
                Gizmos.DrawWireSphere(checkPos, 0.15f);
                Gizmos.DrawLine(rayStart, rayStart + Vector3.down * (controller.height + 3f));

                if (!hasGround)
                {
                    UnityEditor.Handles.color = Color.red;
                    UnityEditor.Handles.Label(checkPos + Vector3.up * 0.5f, "¡VACÍO!");
                }
            }

            UnityEditor.Handles.color = Color.yellow;
            UnityEditor.Handles.Label(origin + Vector3.up * 2.5f, $"Obstáculo a {obsHit.distance:F1}m - Verificando camino");
        }

        if (lastTargetCheck != Vector3.zero)
        {
            int numDirections = 8;
            float angleStep = 360f / numDirections;
            float checkRadius = controller.radius * 0.95f;

            for (int i = 0; i < numDirections; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(
                    Mathf.Cos(angle) * checkRadius,
                    0f,
                    Mathf.Sin(angle) * checkRadius
                );

                Vector3 checkPoint = lastTargetCheck + offset + Vector3.up * edgeRaycastHeight;
                bool hasGround = Physics.Raycast(checkPoint, Vector3.down, controller.height + 1f,
                    groundLayerMask, QueryTriggerInteraction.Ignore);

                Gizmos.color = hasGround ? new Color(0, 1, 0, 0.5f) : new Color(1, 0, 0, 0.8f);
                Gizmos.DrawLine(checkPoint, checkPoint + Vector3.down * (controller.height + 1f));
                Gizmos.DrawWireSphere(checkPoint, 0.05f);
            }
        }
#endif
    }

    #endregion

    #endregion

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
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
