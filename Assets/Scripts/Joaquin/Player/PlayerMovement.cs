using System.Collections;
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

    [Header("Movement")]
    [HideInInspector] private float fallbackMoveSpeed = 5f;
    [SerializeField] private float moveSpeed = 5f;
    [HideInInspector] private float fallbackGravity = -9.81f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Dash")]
    [SerializeField] private float dashDistance = 10f;
    [SerializeField] private float dashDuration = 0.3f;
    [SerializeField] private float dashCooldown = 0.3f;
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
    private bool prevAnimatorApplyRootMotion = false;
    private Vector3 moveDirection;
    private float yVelocity;
    private bool canMove = true;
    private float lastMoveX;
    private float lastMoveY;

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

    #endregion

    #region Unity Methods

    private void OnEnable()
    {
        PlayerStatsManager.OnStatChanged += HandleStatChanged;
        PlayerHealth.OnLifeStageChanged += HandleLifeStageChanged;

        playerControls?.Movement.Enable();
    }

    private void OnDisable()
    {
        PlayerStatsManager.OnStatChanged -= HandleStatChanged;
        PlayerHealth.OnLifeStageChanged -= HandleLifeStageChanged;

        playerControls?.Movement.Disable();
    }

    private void OnDestroy()
    {
        PlayerStatsManager.OnStatChanged -= HandleStatChanged;
        PlayerHealth.OnLifeStageChanged -= HandleLifeStageChanged;

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

        playerLayer = LayerMask.NameToLayer("Player");

        float moveSpeedStat = statsManager != null ? statsManager.GetStat(StatType.MoveSpeed) : fallbackMoveSpeed;
        moveSpeed = moveSpeedStat;

        float gravityStat = statsManager != null ? statsManager.GetStat(StatType.Gravity) : fallbackGravity;
        gravity = gravityStat;

        lastMoveY = -1;
        lastMoveX = 0;

        InitializeDashVFX();
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

        ReportDebug($"Stat {statType} cambiado a {newValue}.", 1);
    }

    private void HandleLifeStageChanged(PlayerHealth.LifeStage newStage)
    {
        int ageStageValue = 0;
        switch (newStage)
        {
            case PlayerHealth.LifeStage.Young:
                ageStageValue = 0;
                break;
            case PlayerHealth.LifeStage.Adult:
                ageStageValue = 1;
                break;
            case PlayerHealth.LifeStage.Elder:
                ageStageValue = 2;
                break;
        }

        if (playerAnimator != null) playerAnimator.SetInteger("AgeStage", ageStageValue);
        ReportDebug($"Etapa de vida cambiada a {newStage}. Animator AgeStage seteado a {ageStageValue}.", 1);
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
        }

        ApplyGravity();

        //if (Input.GetKeyDown(KeyCode.Space) && dashCooldownTimer <= 0 && !IsDashing && !isDashDisabled)
        //{
        //    StartCoroutine(DashRoutine());
        //}
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

            RotateTowardsMovement();
        }
    }

    #endregion

    #region Custom Methods
    public void OnMove(InputAction.CallbackContext context)
    {
        currentInputVector = context.ReadValue<Vector2>();
    }


    // Maneja la entrada de movimiento del jugador y actualiza las animaciones.
    // Importante: si rotationLocked == true, NO actualizar lastMoveX/lastMoveY ni Xaxis/Yaxis del animator,
    // y NO permitir que la entrada WASD cambie la rotación (solo permite mover el personaje).
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

        bool isMoving = moveDirection.magnitude > 0.1f;

        // Siempre actualizar "Running" para que la animación de movimiento ocurra cuando se mueve.
        if (playerAnimator != null) playerAnimator.SetBool("Running", isMoving);

        // Si NO hay bloqueo de rotación, actualizamos los ejes del animator según la entrada WASD.
        if (!rotationLocked)
        {
            if (isMoving)
            {
                // Guardamos la última entrada directa del jugador (valores -1,0,1).
                lastMoveX = Mathf.Round(moveX);
                lastMoveY = Mathf.Round(moveY);
            }

            if (playerAnimator != null)
            {
                playerAnimator.SetFloat("Xaxis", lastMoveX);
                playerAnimator.SetFloat("Yaxis", lastMoveY);
            }
        }
        else
        {
            // Si está bloqueado: no tocar lastMoveX/Y ni Xaxis/Yaxis.
            // Esto asegura que la rotación del mouse (bloqueada a 8 direcciones) se mantenga hasta que termine el ataque.
        }
    }

    // Alinea al jugador en la dirección del movimiento o hacia la rotación bloqueada si existe.
    private void RotateTowardsMovement()
    {
        if (rotationLocked)
        {
            // Mantener la rotación bloqueada (suave)
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

    /// <summary>
    /// Método público llamado por PlayerCombatActionManager para ejecutar el dash.
    /// </summary>
    public IEnumerator ExecuteDashFromManager()
    {
        yield return StartCoroutine(DashRoutine());
    }

    private IEnumerator DashRoutine()
    {
        Vector3 dashDirection = moveDirection.magnitude > 0.1f ? moveDirection : transform.forward;

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
            prevAnimatorApplyRootMotion = playerAnimator.applyRootMotion;
            playerAnimator.applyRootMotion = false;
        }

        if (dashDustVFX != null) PlayDashVFX(true);
        if (afterimagePrefab != null) StartCoroutine(AfterimageRoutine());

        yield return StartCoroutine(PerformDash(targetDashPosition, dashDuration));

        float safetyPushSpeed = (dashDistance / dashDuration) * 0.75f;
        float maxStuckTime = 1.0f;
        float stuckTimer = 0f;

        Vector3 capsuleCenter = transform.position + controller.center;
        Vector3 p1 = capsuleCenter + Vector3.up * (controller.height / 2f - controller.radius);
        Vector3 p2 = capsuleCenter - Vector3.up * (controller.height / 2f - controller.radius);

        while (Physics.CheckCapsule(p1, p2, controller.radius, traversableLayers, QueryTriggerInteraction.Ignore) && stuckTimer < maxStuckTime)
        {
            controller.Move(dashDirection * safetyPushSpeed * Time.deltaTime);
            stuckTimer += Time.deltaTime;

            capsuleCenter = transform.position + controller.center;
            p1 = capsuleCenter + Vector3.up * (controller.height / 2f - controller.radius);
            p2 = capsuleCenter - Vector3.up * (controller.height / 2f - controller.radius);

            yield return null;
        }

        if (dashDustVFX != null) PlayDashVFX(false);
        ToggleLayerCollisions(false);
        if (playerHealth != null) playerHealth.IsInvulnerable = false;

        if (playerAnimator != null)
        {
            playerAnimator.applyRootMotion = prevAnimatorApplyRootMotion;
        }

        IsDashing = false;
        dashCooldownTimer = dashCooldown;
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

        if (controller != null)
        {
            prevStepOffset = controller.stepOffset;
            controller.stepOffset = 0f;
        }

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

    /// <summary>
    /// Sistema de detección de bordes estilo Hades.
    /// Previene que el jugador se caiga de plataformas durante movimiento normal.
    /// Si detecta un vacío adelante, anula el movimiento en esa dirección.
    /// </summary>
    /// <param name="movement">Vector de movimiento propuesto</param>
    /// <returns>Vector de movimiento ajustado (o cero si hay vacío)</returns>
    private Vector3 ApplyEdgeDetection(Vector3 movement)
    {
        // Solo aplicar detección de bordes si el jugador está en el suelo
        if (!controller.isGrounded)
        {
            return movement;
        }

        // Calcular posición de origen del raycast
        Vector3 horizontalMovement = new Vector3(movement.x, 0f, movement.z);
        if (horizontalMovement.magnitude < 0.01f)
        {
            return movement;
        }

        Vector3 movementDirection = horizontalMovement.normalized;
        Vector3 rayOrigin = transform.position + Vector3.up * edgeRaycastHeight;
        Vector3 checkPosition = rayOrigin + movementDirection * (controller.radius + edgeDetectionDistance);

        // Raycast hacia abajo para detectar suelo
        float rayDistance = controller.height + 0.5f;

        if (!Physics.Raycast(checkPosition, Vector3.down, rayDistance, groundLayerMask, QueryTriggerInteraction.Ignore))
        {
            ReportDebug("Borde detectado. Bloqueando movimiento para prevenir caída.", 1);

            // Anular movimiento horizontal
            return new Vector3(0f, movement.y, 0f);
        }

        // Hay suelo, permitir movimiento normal
        return movement;
    }

    /// <summary>
    /// Valida la ruta del dash para detectar obstáculos o vacíos cruzables.
    /// </summary>
    /// <param name="direction">La dirección normalizada del dash.</param>
    /// <param name="finalPosition">El punto de aterrizaje seguro parámetro de salida.</param>
    /// <returns>True si el dash es posible, false si está bloqueado.</returns>
    private bool ValidateDashPath(Vector3 direction, out Vector3 finalPosition)
    {
        Vector3 origin = transform.position;
        float playerRadius = controller.radius;
        float playerHeight = controller.height;

        // Puntos de la cápsula para el CapsuleCast
        Vector3 p1 = origin + controller.center + Vector3.up * (playerHeight / 2f - playerRadius);
        Vector3 p2 = origin + controller.center - Vector3.up * (playerHeight / 2f - playerRadius);

        // Paso 1: comprobar colisiones con capas explícitas de colisión del dash
        if (Physics.CapsuleCast(p1, p2, playerRadius, direction, out RaycastHit obstacleHit, dashDistance, dashCollisionLayers, QueryTriggerInteraction.Ignore))
        {
            // Punto antes del choque, dejando un margen igual al radio para evitar clipping
            finalPosition = origin + direction * Mathf.Max(0f, obstacleHit.distance - playerRadius);
            ReportDebug("El camino está bloqueado por un obstáculo. Ajustando la distancia.", 1);
            return true;
        }

        // Paso 2: intentar proyectar el punto objetivo base a dashDistance y buscar suelo bajo ese punto
        float scanHeight = 2.0f; // altura desde la que proyectar el raycast hacia abajo
        Vector3 baseTarget = origin + direction * dashDistance + Vector3.up * scanHeight;

        if (Physics.Raycast(baseTarget, Vector3.down, out RaycastHit baseGroundHit, playerHeight + scanHeight + 1f, groundLayerMask, QueryTriggerInteraction.Ignore))
        {
            // Hay suelo bajo el punto objetivo base, permitir dash hasta ese punto
            finalPosition = baseGroundHit.point;
            ReportDebug("Se ha encontrado terreno o seguro debajo del objetivo del Dash base. Utilizando distancia del Dash base.", 1);
            return true;
        }

        // Paso 3: no hay suelo bajo el target base. intentar buscar suelo entre dashDistance y dashDistance + gapDashBonusDistance
        float scanStart = dashDistance + 0.1f;
        float scanMax = dashDistance + gapDashBonusDistance;
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
            // Si el suelo está dentro del bonus permitimos completar el dash
            finalPosition = foundHit.point;
            ReportDebug("Se detectó una brecha/vacio. Se encontró un aterrizaje seguro utilizando la bonificación por brecha/vacio.", 1);
            return true;
        }

        // Paso 4: no hay suelo en rango permitido, cancelar dash
        finalPosition = origin;
        ReportDebug("La trayectoria del Dash no es segura, no hay terreno dentro del rango permitido. Dash cancelado.", 2);
        return false;
    }

    private IEnumerator AfterimageRoutine()
    {
        float interval = 0.05f;
        while (IsDashing)
        {
            if (afterimagePrefab != null)
            {
                // Usar el transform del modelo si PlayerHealth lo expone,
                // Sino usar el root transform del jugador.
                Transform modelTransform = (playerHealth != null && playerHealth.PlayerModelTransform != null)
                    ? playerHealth.PlayerModelTransform
                    : transform;

                // Instanciar el afterimage en la posición/rotación del modelo
                GameObject afterimage = Instantiate(afterimagePrefab, modelTransform.position, modelTransform.rotation);

                // Ajustar la escala mundial del afterimage para que coincida con la del modelo actual
                // Si el prefab está pensado para escala 1 en root, asignar la escala global del modelo.
                afterimage.transform.localScale = modelTransform.lossyScale;

                // Sincronizar visual (Sprite o Mesh)
                // Copiar sprite si existe
                var srcSprite = modelTransform.GetComponentInChildren<SpriteRenderer>();
                var dstSprite = afterimage.GetComponentInChildren<SpriteRenderer>();
                if (srcSprite != null && dstSprite != null)
                {
                    dstSprite.sprite = srcSprite.sprite;
                    dstSprite.flipX = srcSprite.flipX;
                    // No tocar srcSprite.color. Aplica transparencia solo al afterimage.
                    Color dstColor = srcSprite.color;
                    dstColor.a = afterimageAlpha;
                    dstSprite.color = dstColor;
                }
                else
                {
                    // Sino intenta, copiar mesh + material y aplica transparencia con MaterialPropertyBlock
                    var srcMeshRenderer = modelTransform.GetComponentInChildren<MeshRenderer>();
                    var srcMeshFilter = modelTransform.GetComponentInChildren<MeshFilter>();

                    var dstMeshRenderer = afterimage.GetComponentInChildren<MeshRenderer>();
                    var dstMeshFilter = afterimage.GetComponentInChildren<MeshFilter>();

                    if (srcMeshRenderer != null && dstMeshRenderer != null)
                    {
                        // Copiar mesh si existe
                        if (srcMeshFilter != null && dstMeshFilter != null)
                        {
                            dstMeshFilter.mesh = srcMeshFilter.sharedMesh;
                        }

                        // Aplicar transparencia usando MaterialPropertyBlock para no instanciar materiales
                        ApplyTransparencyToRenderer(dstMeshRenderer, afterimageAlpha);
                    }
                }

            Destroy(afterimage, afterimageLifetime);
            }
            yield return new WaitForSeconds(interval);
        }
    }

    /// <summary>
    /// Aplica un valor de alpha al renderer usando MaterialPropertyBlock (no instancia materials).
    /// Intenta propiedades comunes: "_BaseColor" (URP/HDRP) y "_Color" (legacy).
    /// </summary>
    private void ApplyTransparencyToRenderer(Renderer renderer, float alpha)
    {
        if (renderer == null) return;

        // Si el renderer tiene material con _BaseColor o _Color, intentar obtenerlo.
        Color baseColor = Color.white;
        Material mat = renderer.sharedMaterial;
        if (mat != null)
        {
            if (mat.HasProperty("_BaseColor"))
            {
                baseColor = mat.GetColor("_BaseColor");
            }
            else if (mat.HasProperty("_Color"))
            {
                baseColor = mat.GetColor("_Color");
            }
        }

        // Ajustar alpha
        baseColor.a = alpha;

        // Aplicar con MaterialPropertyBlock
        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        mpb.SetColor("_BaseColor", baseColor);
        mpb.SetColor("_Color", baseColor);
        renderer.SetPropertyBlock(mpb);
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

    public void SetCanMove(bool state)
    {
        canMove = state;
        if (!state)
        {
            moveDirection = Vector3.zero;
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

    public float GetMaxSafeDistance(Vector3 dir, float maxDesiredDistance)
    {
        if (!enableEdgeDetection || controller == null || !IsEffectivelyGrounded()) return maxDesiredDistance;

        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return 0f;
        dir.Normalize();

        int samples = Mathf.Clamp(Mathf.CeilToInt((maxDesiredDistance) / (controller.radius + edgeDetectionDistance)), 1, edgeSampleMax);
        float step = maxDesiredDistance / samples;
        Vector3 rayOriginBase = transform.position + Vector3.up * edgeRaycastHeight;
        float rayDistance = controller.height + 0.6f;

        for (int i = 1; i <= samples; i++)
        {
            float traveled = step * i;
            // samplePos se mueve a lo largo de la trayectoria que el jugador recorrería
            Vector3 samplePos = rayOriginBase + dir * Mathf.Max(0f, traveled + controller.radius + edgeDetectionDistance);

            if (!Physics.Raycast(samplePos, Vector3.down, rayDistance, groundLayerMask, QueryTriggerInteraction.Ignore))
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

    /// <summary>
    /// Aplica detección de bordes a un desplazamiento específico (usado en ataques).
    /// Similar a ApplyEdgeDetection pero trabaja con un vector de desplazamiento directo.
    /// </summary>
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
                // mantener Y original del desplazamiento
                return new Vector3(slide.x, displacement.y, slide.z);
            }

            return new Vector3(0f, displacement.y, 0f);
        }

        return displacement;
    }

    /// <summary>
    /// Mueve el personaje usando el CharacterController, respetando colisiones.
    /// Ideal para movimientos forzados como los de un combo de ataque.
    /// </summary>
    public void MoveCharacter(Vector3 displacement)
    {
        if (controller != null && controller.enabled)
        {
            // Aplicar detección de bordes al desplazamiento de ataque
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

    /// <summary>
    /// Lockea la rotación del jugador hacia la dirección mundial dada, pero ajustada a las 8 direcciones (octantes).
    /// Si setAnimatorAxes es true se actualizarán lastMoveX/lastMoveY y los parámetros del Animator para que la animación corresponda.
    /// </summary>
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
            playerAnimator.SetBool("Running", (x != 0f || y != 0f));
        }
    }

    /// <summary>
    /// Desbloquea la rotación permitiendo que el jugador vuelva a rotar según su movimiento.
    /// Además sincroniza los ejes del animator con la entrada de movimiento actual para evitar saltos.
    /// </summary>
    public void UnlockFacing()
    {
        // Al desbloquear, si el jugador está moviendo con WASD usamos esa dirección para los ejes,
        // si no, conservamos la última dirección del lock (para evitar saltos).
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
                // Si no se está moviendo, mantener los valores que impuso el lock (ya estaban en lastMoveX/Y).
            }

            playerAnimator.SetFloat("Xaxis", lastMoveX);
            playerAnimator.SetFloat("Yaxis", lastMoveY);
            playerAnimator.SetBool("Running", (moveDirection.magnitude > 0.1f));
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

    /// <summary>
    /// Devuelve la rotación objetivo del lock para que otros scripts puedan comparar.
    /// </summary>
    public Quaternion GetLockedRotation()
    {
        return lockedRotation;
    }

    /// <summary>
    /// Aplica inmediatamente la rotación lockeada (útil si quieres forzar el snapped rotation).
    /// </summary>
    public void ForceApplyLockedRotation()
    {
        transform.rotation = lockedRotation;
    }

    #endregion

    #region Debugging

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || controller == null) return;

        Vector3 origin = transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(origin, 0.15f);

        Vector3 dashDirection = moveDirection.magnitude > 0.1f ? moveDirection.normalized : transform.forward;

        Gizmos.color = Color.yellow;
        Vector3 baseEnd = origin + dashDirection * dashDistance;
        Gizmos.DrawLine(origin, baseEnd);
        Gizmos.DrawWireSphere(baseEnd, 0.15f);

        Gizmos.color = Color.magenta;
        Vector3 bonusEnd = origin + dashDirection * (dashDistance + gapDashBonusDistance);
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

            float totalDistance = dashDistance + gapDashBonusDistance;
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

#if UNITY_EDITOR
        if (lastTargetCheck != Vector3.zero)
        {
            Gizmos.color = lastTargetHit ? Color.green : Color.red;
            Gizmos.DrawWireSphere(lastTargetCheck, 0.12f);
            Gizmos.DrawLine(lastTargetCheck, lastTargetCheck + Vector3.down * (controller.height + 0.6f));

            UnityEditor.Handles.Label(lastTargetCheck + Vector3.up * 0.2f, lastTargetHit ? "lastTargetCheck hit" : "lastTargetCheck miss");
        }
#endif

#if UNITY_EDITOR
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
#endif
    }

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
