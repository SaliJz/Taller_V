using System.Collections;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
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
    [SerializeField] private float dashSpeed = 15f;
    [SerializeField] private float dashDuration = 0.3f;
    [SerializeField] private float dashCooldown = 0.3f;
    [SerializeField] private LayerMask traversableLayers;

    [Header("Effects")]
    [Header("Afterimage Settings")]
    [SerializeField] private GameObject afterimagePrefab;
    [SerializeField, Range(0f, 1f)] private float afterimageAlpha = 0.5f;
    [SerializeField] private float afterimageLifetime = 0.5f;

    [Header("Dash VFX")]
    [SerializeField] private ParticleSystem dashDustVFX;

    private int playerLayer;
    private float dashCooldownTimer = 0f;
    public bool IsDashing { get; private set; }
    public float MoveSpeed
    {
        get { return moveSpeed; }
        set { moveSpeed = value; }
    }

    private Vector3 moveDirection;
    private float yVelocity;
    private bool canMove = true;
    private float lastMoveX;
    private float lastMoveY;

    private bool rotationLocked = false;
    private Quaternion lockedRotation = Quaternion.identity;

    private bool isDashDisabled = false;
    public bool IsDashDisabled
    {
        get { return isDashDisabled; }
        set { isDashDisabled = value; }
    }

    private Material dashVFXMaterialInstance;

    #endregion

    #region Unity Methods

    private void OnEnable()
    {
        PlayerStatsManager.OnStatChanged += HandleStatChanged;
        PlayerHealth.OnLifeStageChanged += HandleLifeStageChanged;
    }

    private void OnDisable()
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

        if (Input.GetKeyDown(KeyCode.Space) && dashCooldownTimer <= 0 && !IsDashing && !isDashDisabled)
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

    // Maneja la entrada de movimiento del jugador y actualiza las animaciones.
    // Importante: si rotationLocked == true, NO actualizar lastMoveX/lastMoveY ni Xaxis/Yaxis del animator,
    // y NO permitir que la entrada WASD cambie la rotación (solo permite mover el personaje).
    private void HandleMovementInput()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");

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

    private IEnumerator DashRoutine()
    {
        IsDashing = true;
        if (playerHealth != null) playerHealth.IsInvulnerable = true;
        ToggleLayerCollisions(true);
        if (dashDustVFX != null) PlayDashVFX(true);
        if (afterimagePrefab != null) StartCoroutine(AfterimageRoutine());

        Vector3 dashDirection = moveDirection.magnitude > 0.1f ? moveDirection : transform.forward;

        yield return StartCoroutine(PerformDash(dashDirection, dashDuration));

        float safetyPushSpeed = dashSpeed * 0.75f;
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


    private IEnumerator PerformDash(Vector3 direction, float duration)
    {
        float startTime = Time.time;
        while (Time.time < startTime + duration)
        {
            controller.Move(direction * dashSpeed * Time.deltaTime);
            yield return null;
        }
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

    private IEnumerator AfterimageRoutine()
    {
        float interval = 0.05f;
        while (IsDashing)
        {
            if (afterimagePrefab != null)
            {
                // Usa el transform del modelo si PlayerHealth lo expone,
                // Sino usa el root transform del jugador.
                Transform modelTransform = (playerHealth != null && playerHealth.PlayerModelTransform != null)
                    ? playerHealth.PlayerModelTransform
                    : transform;

                // Instancia el afterimage en la posición/rotación del modelo
                GameObject afterimage = Instantiate(afterimagePrefab, modelTransform.position, modelTransform.rotation);

                // Ajusta escala mundial del afterimage para que coincida con la del modelo actual
                // Si el prefab está pensado para escala 1 en root, asigna la escala global del modelo.
                afterimage.transform.localScale = modelTransform.lossyScale;

                // Sincronizar visual (Sprite o Mesh)
                // Copiar sprite si existe
                var srcSprite = modelTransform.GetComponentInChildren<SpriteRenderer>();
                var dstSprite = afterimage.GetComponentInChildren<SpriteRenderer>();
                if (srcSprite != null && dstSprite != null)
                {
                    dstSprite.sprite = srcSprite.sprite;
                    dstSprite.flipX = srcSprite.flipX;
                    // No toca srcSprite.color. Aplica transparencia solo al afterimage.
                    Color dstColor = srcSprite.color;
                    dstColor.a = afterimageAlpha;
                    dstSprite.color = dstColor;
                }
                else
                {
                    // Sino intenta copiar mesh + material y aplica transparencia con MaterialPropertyBlock
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

    /// <summary>
    /// Mueve el personaje usando el CharacterController, respetando colisiones.
    /// Ideal para movimientos forzados como los de un combo de ataque.
    /// </summary>
    public void MoveCharacter(Vector3 displacement)
    {
        if (controller != null && controller.enabled)
        {
            controller.Move(displacement);
        }
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
            // Dot produce valores entre -1 y 1; round los convertirá a -1,0,1 (octantes alineados con la cámara)
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
        if (!Application.isPlaying) return;

        Gizmos.color = Color.cyan;
        Vector3 dashDirection = moveDirection.magnitude > 0.1f ? moveDirection.normalized : transform.forward;
        float dashDistance = dashSpeed * dashDuration;

        Gizmos.DrawRay(transform.position, dashDirection * dashDistance);

        if (Physics.Raycast(transform.position, dashDirection, out RaycastHit hit, dashDistance, ~traversableLayers))
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(hit.point, 0.5f);
        }
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
