// PlayerMeleeAttack.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Clase que maneja el ataque cuerpo a cuerpo del jugador.
/// Ahora: primero se rota (snap a 8 direcciones), espera la rotación, y luego ejecuta el ataque.
/// </summary>
public class PlayerMeleeAttack : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerStatsManager statsManager;
    [SerializeField] private GameObject visualSphereHit;
    [SerializeField] private GameObject visualBoxHit;
    [SerializeField] private PlayerShieldController playerShieldController;
    [SerializeField] private PlayerAudioController playerAudioController;
    [SerializeField] private Animator playerAnimator;

    [Header("Attack Configuration")]
    [SerializeField] private Transform hitPoint;
    [Header("Fallback stats (if no StatsManager)")]
    [HideInInspector] private float fallbackHitRadius = 0.8f;
    [HideInInspector] private float fallbackAttackDamage = 10;
    [HideInInspector] private float fallbackAttackSpeed = 1f;
    [Header("Calculated stats")]
    [SerializeField] private float hitRadius = 0.8f;
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private float attackSpeed = 1f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private bool canShowHitGizmo = false;

    [Header("Combo Configuration")]
    [SerializeField] private float comboResetTime = 2f; // Tiempo sin atacar para resetear combo
    [SerializeField] private float[] comboMovementForces = new float[3] { 1.5f, 2f, 1.8f }; // Fuerza de empuje por ataque
    [SerializeField] private float[] comboLockDurations = new float[3] { 0.4f, 0.6f, 0.8f }; // Duración del lock por ataque
    [SerializeField] private float[] comboStunDurations = new float[3] { 0.5f, 0.5f, 1f }; // Duración de aturdimiento por ataque

    [Header("Attack 1 (Basic)")]
    [SerializeField] private ParticleSystem vfxAttack1Slash;
    [SerializeField] private float attack1Duration = 0.4f;

    [Header("Attack 2 (Area/Spin)")]
    [SerializeField] private ParticleSystem vfxAttack2Slash;
    [SerializeField] private float attack2MovementDuration = 0.6f;
    [SerializeField] private float attack2SpinDuration = 0.4f;
    [SerializeField] private float attack2SpinSpeed = 900f;
    [SerializeField] private float attack2TargetSpinAngle = 360f;

    [Header("Attack 3 (Heavy/Charge)")]
    [SerializeField] private ParticleSystem vfxAttack3Slash;
    [SerializeField] private float attack3PreChargeDuration = 0.3f;
    [SerializeField] private float attack3ChargeDuration = 0.3f;
    [SerializeField] private float attack3SpinSpeed = 90f;

    [Header("knockback Configuration")]
    [SerializeField] private float knockbackYoung = 0.25f;
    [SerializeField] private float knockbackAdult = 0.5f;
    [SerializeField] private float knockbackElder = 0.75f;
    [SerializeField] private float knockbackMaxDistance = 3f; // Distancia máxima de knockback

    [Header("Melee Impact VFX")]
    [SerializeField] private GameObject meleeImpactVFX;
    //[SerializeField] private int impactParticleCount = 20;

    [Header("Debug")]
    [SerializeField] private bool useBoxCollider = false;
    [SerializeField] private bool showGizmo = false;
    [SerializeField] private float gizmoDuration = 0.2f;

    private float attackCooldown = 0f;
    private int finalAttackDamage;
    private float finalAttackSpeed;

    private float damageMultiplier = 1f;
    private float speedMultiplier = 1f;

    private bool isAttacking = false;

    private int comboCount = 0;
    private float lastAttackTime = 0f;
    private int currentAttackIndex = -1;

    private HashSet<Collider> hitEnemiesThisCombo = new HashSet<Collider>();
    private GamepadPointer gamepadPointer;
    private Collider[] hitBuffer = new Collider[64]; // Buffer para detección de enemigos

    public int AttackDamage
    {
        get { return attackDamage; }
        set { attackDamage = value; }
    }

    public bool IsAttacking => isAttacking;
    public int ComboCount => comboCount;

    private PlayerHealth playerHealth;
    private PlayerMovement playerMovement;
    private Coroutine cleanupCoroutine;
    private Coroutine showGizmoRoutine = null;

    public event Action<bool> OnAttacked;

    private void Awake()
    {
        if (visualSphereHit != null) visualSphereHit.SetActive(false);
        if (visualBoxHit != null) visualBoxHit.SetActive(false);

        //if (attack1Slash != null) attack1Slash.SetActive(false);
        //if (attack2Slash != null) attack2Slash.SetActive(false);
        //if (attack3Slash != null) attack3Slash.SetActive(false);

        if (statsManager == null) statsManager = GetComponent<PlayerStatsManager>();
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
        if (playerShieldController == null) playerShieldController = GetComponent<PlayerShieldController>();
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
        if (playerAnimator == null) playerAnimator = GetComponentInChildren<Animator>();
        if (playerAudioController == null) playerAudioController = GetComponent<PlayerAudioController>();
        if (gamepadPointer == null) gamepadPointer = FindAnyObjectByType<GamepadPointer>();
 
        if (statsManager == null) ReportDebug("StatsManager no está asignado en PlayerMeleeAttack. Usando valores de fallback.", 2);
        if (playerHealth == null) ReportDebug("PlayerHealth no se encuentra en el objeto.", 3);
        if (playerShieldController == null) ReportDebug("PlayerShieldController no se encuentra en el objeto.", 3);
        if (playerMovement == null) ReportDebug("PlayerMovement no se encuentra en el objeto. Lock de rotación no funcionará.", 2);
        if (playerAnimator == null) ReportDebug("Animator no se encuentra en los hijos del objeto.", 2);
        if (playerAudioController == null) ReportDebug("PlayerAudioController no se encuentra en el objeto.", 3);
        if (gamepadPointer == null) ReportDebug("GamepadPointer no se encuentra en la escena. La detección de dispositivo activo para el ataque podría fallar.", 2);
    }

    private void OnEnable()
    {
        PlayerStatsManager.OnStatChanged += HandleStatChanged;
    }

    private void OnDestroy()
    {
        PlayerStatsManager.OnStatChanged -= HandleStatChanged;

        if (cleanupCoroutine != null)
        {
            StopCoroutine(cleanupCoroutine);
            cleanupCoroutine = null;
        }

        //CleanupVFXImmediate();
        StopAllCoroutines();
    }

    private void Start()
    {
        showGizmo = false;

        float hitRadiusStat = statsManager != null ? statsManager.GetStat(StatType.MeleeRadius) : fallbackHitRadius;
        hitRadius = hitRadiusStat;

        float attackDamageStat = statsManager != null ? statsManager.GetStat(StatType.MeleeAttackDamage) : fallbackAttackDamage;
        attackDamage = Mathf.RoundToInt(attackDamageStat);

        float attackSpeedStat = statsManager != null ? statsManager.GetStat(StatType.MeleeAttackSpeed) : fallbackAttackSpeed;
        attackSpeed = attackSpeedStat;

        float damageMultiplierStat = statsManager != null ? statsManager.GetStat(StatType.AttackDamage) : 1f;
        damageMultiplier = damageMultiplierStat;

        float speedMultiplierStat = statsManager != null ? statsManager.GetStat(StatType.AttackSpeed) : 1f;
        speedMultiplier = speedMultiplierStat;

        CalculateStats();
    }

    private void HandleStatChanged(StatType statType, float newValue)
    {
        switch (statType)
        {
            case StatType.AttackDamage:
                damageMultiplier = newValue;
                break;
            case StatType.AttackSpeed:
                speedMultiplier = newValue;
                break;
            case StatType.MeleeAttackDamage:
                attackDamage = Mathf.RoundToInt(newValue);
                break;
            case StatType.MeleeAttackSpeed:
                attackSpeed = newValue;
                break;
            case StatType.MeleeRadius:
                hitRadius = newValue;
                break;
            default:
                return;
        }

        CalculateStats();
        ReportDebug($"Estadistica {statType} actualizada a {newValue}.", 1);
    }

    private void CalculateStats()
    {
        finalAttackDamage = Mathf.RoundToInt(attackDamage + damageMultiplier);
        finalAttackSpeed = attackSpeed + speedMultiplier;

        ReportDebug($"Estadisticas recalculadas: Daño Final = {finalAttackDamage}, Velocidad de Ataque Final = {finalAttackSpeed}", 1);
    }

    private void Update()
    {
        if (attackCooldown > 0f) attackCooldown -= Time.deltaTime;

        if (Time.time - lastAttackTime > comboResetTime) comboCount = 0;

        //if (Input.GetMouseButtonDown(0) && attackCooldown <= 0f && !isAttacking)
        //{
        //    if (!CanAttack()) return;

        //    lastAttackTime = Time.time;
        //    StartCoroutine(AttackSequence(comboCount));
        //    comboCount = (comboCount + 1) % 3; // Ciclo: 0 -> 1 -> 2 -> 0
        //}
    }

    // Verifica si el jugador puede atacar (tiene escudo y no está lanzándolo).
    public bool CanAttack()
    {
        if (isAttacking) return false;
        if (playerShieldController != null)
        {
            if (!playerShieldController.HasShield)
            {
                ReportDebug("No se puede atacar: escudo no disponible.", 1);
                return false;
            }

            if (playerShieldController.IsThrowingShield)
            {
                ReportDebug("No se puede atacar: escudo está siendo lanzado.", 1);
                return false;
            }
        }

        return true;
    }

    public void CancelAttack()
    {
        if (!isAttacking) return;

        StopAllCoroutines();

        isAttacking = false;

        // Resetear combo para que no guarde el estado intermedio
        comboCount = 0;
        lastAttackTime = 0; // Forzar reset de tiempo

        // Limpiar listas de impacto
        hitEnemiesThisCombo.Clear();

        // Limpieza visual y de movimiento
        if (playerMovement != null)
        {
            playerMovement.StopForcedMovement();
            playerMovement.UnlockFacing();
            playerMovement.SetCanMove(true);
        }

        if (playerAnimator != null)
        {
            playerAnimator.SetBool("IsAttacking", false);
        }

        // Ocultar gizmos de debug si se quedaron encendidos
        if (visualBoxHit != null) visualBoxHit.SetActive(false);
        if (visualSphereHit != null) visualSphereHit.SetActive(false);

        ReportDebug("Ataque cancelado por bloqueo.", 1);
    }

    /// <summary>
    /// Método público llamado por PlayerCombatActionManager para ejecutar el ataque.
    /// </summary>
    public IEnumerator ExecuteAttackFromManager()
    {
        if (!CanAttack()) yield break;

        lastAttackTime = Time.time;
        yield return StartCoroutine(AttackSequence(comboCount));
        comboCount = (comboCount + 1) % 3; // Ciclo: 0 -> 1 -> 2 -> 0
    }

    /// <summary>
    /// Secuencia de ataque:
    /// 1) Obtener dirección del mouse (o forward)
    /// 2) Lockear rotación snapped a 8 direcciones
    /// 3) Esperar a que la rotación alcance el objetivo (o timeout)
    /// 4) Ejecutar el ataque (PerformHitDetection)
    /// 5) Mantener bloqueo durante la duración del ataque y luego desbloquear
    /// </summary>
    private IEnumerator AttackSequence(int attackIndex)
    {
        currentAttackIndex = attackIndex;

        if (playerAnimator != null) playerAnimator.SetInteger("ComboNum", currentAttackIndex);

        isAttacking = true;

        OnAttacked?.Invoke(true);

        if (playerAnimator != null) playerAnimator.SetBool("IsAttacking", true);

        if (playerMovement != null)
        {
            playerMovement.SetCanMove(false);
        }

        // Limpiar enemigos golpeados para este nuevo ataque
        hitEnemiesThisCombo.Clear();

        // 1) Dirección objetivo
        Vector3 targetDir;
        bool isGamepadActive = false;
        if (gamepadPointer != null)
        {
            isGamepadActive = (gamepadPointer.GetCurrentActiveDevice() == gamepadPointer.GetCurrentGamepad());
        }

        if (isGamepadActive)
        {
            // Con mando, usa la dirección actual del jugador
            targetDir = transform.forward;
            // O mejor: targetDir = playerMovement.GetCurrentMovementDirection(); si existiera.
        }
        else if (!TryGetMouseWorldDirection(out targetDir))
        {
            targetDir = transform.forward;
        }

        // 2) Lockear rotación snapped
        if (playerMovement != null)
        {
            playerMovement.LockFacingTo8Directions(targetDir, true);
        }
        else
        {
            // fallback: aplicar rotación instantánea snappeada
            RotateTowardsMouseInstant();
        }

        // Esperar a que se alcance la rotación
        yield return StartCoroutine(WaitForRotationLock());

        if (playerMovement != null) playerMovement.StartForcedMovement(true);

        switch (attackIndex)
        {
            case 0: yield return StartCoroutine(ExecuteAttack1()); break;
            case 1: yield return StartCoroutine(ExecuteAttack2()); break;
            case 2: yield return StartCoroutine(ExecuteAttack3()); break;
        }

        if (playerMovement != null)
        {
            playerMovement.StopForcedMovement();
            playerMovement.UnlockFacing();
            playerMovement.SetCanMove(true);
        }

        isAttacking = false;

        OnAttacked?.Invoke(false);

        if (playerAnimator != null) playerAnimator.SetBool("IsAttacking", false);

        hitEnemiesThisCombo.Clear();
    }

    public void OnAttackAnimationEnd()
    {
        //if (playerMovement != null)
        //{
        //    playerMovement.StopForcedMovement();
        //    playerMovement.UnlockFacing();
        //}

        //isAttacking = false;
        //if (playerAnimator != null) playerAnimator.SetBool("IsAttacking", false);

        //hitEnemiesThisCombo.Clear();
    }

    private IEnumerator WaitForRotationLock()
    {
        float maxWait = 0.25f;
        float start = Time.time;
        float angleThreshold = 2f;

        while (Time.time - start < maxWait)
        {
            if (playerMovement != null)
            {
                Quaternion target = playerMovement.GetLockedRotation();
                float angle = Quaternion.Angle(transform.rotation, target);
                if (angle <= angleThreshold) break;
            }
            else
            {
                break;
            }
            yield return null;
        }

        if (playerMovement != null) playerMovement.ForceApplyLockedRotation();
    }

    private IEnumerator ExecuteAttack1()
    {
        float movementDuration = attack1Duration;
        float elapsedTime = 0f;

        float desiredTotalDistance = comboMovementForces[0];
        Vector3 forward = transform.forward;

        float safeDistance = desiredTotalDistance;
        if (playerMovement != null)
        {
            if (!playerMovement.IsMovementSafeDirection(forward, desiredTotalDistance))
            {
                safeDistance = playerMovement.GetMaxSafeDistance(forward, desiredTotalDistance);
                ReportDebug($"ExecuteAttack1: fast-path falló. safeDistance recortada a {safeDistance}", 1);
            }
        }

        if (safeDistance > 0.001f && playerMovement != null)
        {
            // Verificar que el punto final sea seguro para la cápsula
            Vector3 finalPos = transform.position + forward * safeDistance;
            if (!playerMovement.IsPositionSafeForCapsule(finalPos))
            {
                safeDistance = 0f;
                ReportDebug("ExecuteAttack1: Posición final insegura. Movimiento cancelado.", 2);
            }
        }

        Vector3 attackMoveVelocity = (forward * safeDistance) / Mathf.Max(0.0001f, movementDuration);

        float accumulated = 0f;

        while (elapsedTime < movementDuration)
        {
            elapsedTime += Time.deltaTime;
            Vector3 frameDesired = attackMoveVelocity * Time.deltaTime;

            float frameHorMag = new Vector3(frameDesired.x, 0f, frameDesired.z).magnitude;
            float remaining = Mathf.Max(0f, safeDistance - accumulated);
            if (frameHorMag > remaining)
            {
                if (remaining <= 0f) frameDesired = new Vector3(0f, frameDesired.y, 0f);
                else
                {
                    Vector3 hor = new Vector3(frameDesired.x, 0f, frameDesired.z).normalized * remaining;
                    frameDesired = new Vector3(hor.x, frameDesired.y, hor.z);
                }
            }

            if (playerMovement != null)
            {
                if (safeDistance <= 0.001f)
                {
                    frameDesired = Vector3.zero;
                    ReportDebug("ExecuteAttack1: No se aplica movimiento de carga por espacio insuficiente.", 1);
                }
                else
                {
                    playerMovement.MoveCharacter(frameDesired);
                    accumulated += new Vector3(frameDesired.x, 0f, frameDesired.z).magnitude;
                }
            }
            else
            {
                transform.position += frameDesired;
                accumulated += new Vector3(frameDesired.x, 0f, frameDesired.z).magnitude;
            }

            PerformHitDetectionWithTracking();

            yield return null;
        }

        // Mantener lock
        float lockDuration = comboLockDurations[0];
        attackCooldown = lockDuration;

        float remainingTime = Mathf.Max(0, lockDuration - attack1Duration);
        if (remainingTime > 0)
        {
            yield return new WaitForSeconds(remainingTime);
        }
    }

    public void ActiveAttack1Slash()
    {
        if (vfxAttack1Slash != null) vfxAttack1Slash.Play();
        if (playerAudioController != null)
        {
            playerAudioController.PlayMeleeSound("BasicSlash");
        }
    }

    public void DesactiveAttack1Slash()
    {
        //attack1Slash.gameObject.SetActive(false);
    }

    private IEnumerator ExecuteAttack2()
    {
        float movementDuration = Mathf.Max(0f, attack2MovementDuration);
        float spinDuration = Mathf.Max(0f, attack2SpinDuration);

        float desiredTotalDistance = comboMovementForces[1];
        Vector3 forward = transform.forward;

        float safeDistance = desiredTotalDistance;
        if (playerMovement != null)
        {
            if (!playerMovement.IsMovementSafeDirection(forward, desiredTotalDistance))
            {
                safeDistance = playerMovement.GetMaxSafeDistance(forward, desiredTotalDistance);
                ReportDebug($"ExecuteAttack2: fast-path falló. safeDistance recortada a {safeDistance}", 1);
            }
        }

        if (safeDistance > 0.001f && playerMovement != null)
        {
            // Verificar que el punto final sea seguro para la cápsula
            Vector3 finalPos = transform.position + forward * safeDistance;
            if (!playerMovement.IsPositionSafeForCapsule(finalPos))
            {
                safeDistance = 0f;
                ReportDebug("ExecuteAttack2: Posición final insegura. Movimiento cancelado.", 2);
            }
        }

        float elapsedTime = 0f;
        Vector3 attackMoveVelocity = (forward * safeDistance) / Mathf.Max(0.0001f, movementDuration);

        float accumulated = 0f;

        while (elapsedTime < movementDuration)
        {
            elapsedTime += Time.deltaTime;
            Vector3 frameDesired = attackMoveVelocity * Time.deltaTime;

            float frameHorMag = new Vector3(frameDesired.x, 0f, frameDesired.z).magnitude;
            float remaining = Mathf.Max(0f, safeDistance - accumulated);
            if (frameHorMag > remaining)
            {
                if (remaining <= 0f) frameDesired = new Vector3(0f, frameDesired.y, 0f);
                else
                {
                    Vector3 hor = new Vector3(frameDesired.x, 0f, frameDesired.z).normalized * remaining;
                    frameDesired = new Vector3(hor.x, frameDesired.y, hor.z);
                }
            }

            if (playerMovement != null)
            {
                if (safeDistance <= 0.001f)
                {
                    frameDesired = Vector3.zero;
                    ReportDebug("ExecuteAttack2: No se aplica movimiento de carga por espacio insuficiente.", 1);
                }
                else
                {
                    playerMovement.MoveCharacter(frameDesired);
                    accumulated += new Vector3(frameDesired.x, 0f, frameDesired.z).magnitude;
                }
            }
            else
            {
                transform.position += frameDesired;
                accumulated += new Vector3(frameDesired.x, 0f, frameDesired.z).magnitude;
            }

            yield return null;
        }

        if (playerMovement != null)
        {
            playerMovement.UnlockFacing();
        }

        if (spinDuration > 0f && Mathf.Abs(attack2TargetSpinAngle) > 0.0001f)
        {
            try
            {
                if (playerMovement != null)
                {
                    playerMovement.IsRotationExternallyControlled = true;
                }

                float targetAngle = Mathf.Abs(attack2TargetSpinAngle);
                float sign = Mathf.Sign(attack2TargetSpinAngle);

                // velocidad angular necesaria para cubrir targetAngle en spinDuration
                float requiredAngularSpeed = targetAngle / spinDuration; // grados por segundo

                float angularSpeed = Mathf.Min(requiredAngularSpeed, Mathf.Abs(attack2SpinSpeed));

                float rotated = 0f;
                float elapsedSpin = 0f;

                while (elapsedSpin < spinDuration && rotated < targetAngle - 0.0001f)
                {
                    float dt = Time.deltaTime;
                    elapsedSpin += dt;

                    float angleThisFrame = angularSpeed * dt;
                    float remaining = targetAngle - rotated;
                    if (angleThisFrame > remaining) angleThisFrame = remaining;

                    transform.Rotate(0f, sign * angleThisFrame, 0f, Space.Self);
                    rotated += angleThisFrame;

                    PerformHitDetectionWithTracking();

                    yield return null;
                }

                // corrección final por errores numéricos
                if (Mathf.Abs(rotated - targetAngle) > 0.01f)
                {
                    float correction = (targetAngle - rotated) * sign;
                    transform.Rotate(0f, correction, 0f, Space.Self);
                    rotated = targetAngle;
                }
            }
            finally
            {
                if (playerMovement != null)
                {
                    playerMovement.IsRotationExternallyControlled = false;
                }
            }
        }
        else
        {
            ReportDebug("ExecuteAttack2: spinDuration o targetSpinAngle inválidos, se omite giro.", 1);
        }

        float lockDuration = comboLockDurations[1];
        attackCooldown = lockDuration;

        float totalAttackDuration = movementDuration + spinDuration;
        float remainingTime = Mathf.Max(0f, lockDuration - totalAttackDuration);
        if (remainingTime > 0)
        {
            yield return new WaitForSeconds(remainingTime);
        }
    }

    public void ActiveAttack2Slash()
    {
        if (vfxAttack2Slash != null) vfxAttack2Slash.Play();
        if (playerAudioController != null)
        {
            playerAudioController.PlayMeleeSound("SpinSlash");
        }
    }

    public void DesactiveAttack2Slash()
    {
    }

    private IEnumerator ExecuteAttack3()
    {
        float preChargeElapsed = 0f;

        while (preChargeElapsed < attack3PreChargeDuration)
        {
            preChargeElapsed += Time.deltaTime;
            float spinAmount = attack3SpinSpeed * Time.deltaTime;
            transform.Rotate(0f, spinAmount, 0f, Space.Self);
            yield return null;
        }

        float chargeElapsed = 0f;
        float desiredTotalDistance = comboMovementForces[2];
        Vector3 forward = transform.forward;

        float safeDistance = desiredTotalDistance;
        if (playerMovement != null)
        {
            if (!playerMovement.IsMovementSafeDirection(forward, desiredTotalDistance))
            {
                safeDistance = playerMovement.GetMaxSafeDistance(forward, desiredTotalDistance);
                ReportDebug($"ExecuteAttack3: fast-path falló. safeDistance recortada a {safeDistance}", 1);
            }
        }

        if (safeDistance > 0.001f && playerMovement != null)
        {
            // Verificar que el punto final sea seguro para la cápsula
            Vector3 finalPos = transform.position + forward * safeDistance;
            if (!playerMovement.IsPositionSafeForCapsule(finalPos))
            {
                safeDistance = 0f;
                ReportDebug("ExecuteAttack3: Posición final insegura. Movimiento cancelado.", 2);
            }
        }

        Vector3 attackMoveVelocity = (forward * safeDistance) / Mathf.Max(0.0001f, attack3ChargeDuration);

        float accumulated = 0f;

        while (chargeElapsed < attack3ChargeDuration)
        {
            chargeElapsed += Time.deltaTime;
            Vector3 frameDesired = attackMoveVelocity * Time.deltaTime;

            float frameHorMag = new Vector3(frameDesired.x, 0f, frameDesired.z).magnitude;
            float remaining = Mathf.Max(0f, safeDistance - accumulated);
            if (frameHorMag > remaining)
            {
                if (remaining <= 0f) frameDesired = new Vector3(0f, frameDesired.y, 0f);
                else
                {
                    Vector3 hor = new Vector3(frameDesired.x, 0f, frameDesired.z).normalized * remaining;
                    frameDesired = new Vector3(hor.x, frameDesired.y, hor.z);
                }
            }

            if (playerMovement != null)
            {
                if (safeDistance <= 0.001f)
                {
                    frameDesired = Vector3.zero;
                    ReportDebug("ExecuteAttack3: No se aplica movimiento de carga por espacio insuficiente.", 1);
                }
                else
                {
                    playerMovement.MoveCharacter(frameDesired);
                    accumulated += new Vector3(frameDesired.x, 0f, frameDesired.z).magnitude;
                }
            }
            else
            {
                transform.position += frameDesired;
                accumulated += new Vector3(frameDesired.x, 0f, frameDesired.z).magnitude;
            }

            PerformHitDetectionWithTracking();
            yield return null;
        }

        float lockDuration = comboLockDurations[2];
        attackCooldown = lockDuration;

        float remainingTime = Mathf.Max(0f, lockDuration - (attack3PreChargeDuration + attack3ChargeDuration));
        if (remainingTime > 0f)
        {
            yield return new WaitForSeconds(remainingTime);
        }
    }

    public void ActiveAttack3Slash()
    {
        if (vfxAttack3Slash != null) vfxAttack3Slash.Play();
        if (playerAudioController != null)
        {
            playerAudioController.PlayMeleeSound("HeavySlash");
        }
    }

    public void DesactiveAttack3Slash()
    {
        //attack3Slash.gameObject.SetActive(false);
    }

    // Rota instantaneamente al mouse proyectado en el plano horizontal (y = transform.position.y), con snap a 8 direcciones.
    private void RotateTowardsMouseInstant()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.up, transform.position);
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 worldPoint = ray.GetPoint(enter);
            Vector3 dir = worldPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                float snapped = Mathf.Round(angle / 45f) * 45f;
                Quaternion snappedRot = Quaternion.Euler(0f, snapped, 0f);
                transform.rotation = snappedRot;
            }
        }
    }

    private bool TryGetMouseWorldDirection(out Vector3 outDir)
    {
        outDir = Vector3.zero;
        Camera cam = Camera.main;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.up, transform.position);
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 worldPoint = ray.GetPoint(enter);
            Vector3 dir = worldPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                outDir = dir.normalized;
                return true;
            }
        }
        return false;
    }

    public void PerformHitDetectionWithTracking()
    {
        bool hitAnyEnemy = false;

        Vector3 damageSourcePos = hitPoint != null ? hitPoint.position : transform.position;

        Collider[] hitEnemies = useBoxCollider
            ? Physics.OverlapBox(hitPoint.position, new Vector3(hitRadius, hitRadius, hitRadius), Quaternion.identity, enemyLayer)
            : Physics.OverlapSphere(hitPoint.position, hitRadius, enemyLayer);

        const DamageType damageTypeForDummy = DamageType.Melee;
        const AttackDamageType meleeDamageType = AttackDamageType.Melee;

        foreach (Collider enemy in hitEnemies)
        {
            if (hitEnemiesThisCombo.Contains(enemy))
            {
                continue;
            }

            hitEnemiesThisCombo.Add(enemy);
            hitAnyEnemy = true;

            if (hitAnyEnemy && playerAudioController != null)
            {
                playerAudioController.PlayHitSound();
            }

            ApplyKnockbackSafe(enemy);

            bool isCritical;
            float finalDamage = CriticalHitSystem.CalculateDamage(finalAttackDamage, transform, enemy.transform, out isCritical);

            IDamageable damageable = enemy.GetComponent<IDamageable>();
            
            if (damageable != null)
            {
                const string TUTORIAL_DUMMY_TAG = "TutorialDummy";

                if (damageable is MonoBehaviour monoBehaviour && monoBehaviour.gameObject.CompareTag(TUTORIAL_DUMMY_TAG))
                {
                    IDamageable iDamageable = monoBehaviour.GetComponent<IDamageable>();

                    TutorialCombatDummy tutorialDummy = damageable as TutorialCombatDummy;

                    if (tutorialDummy != null)
                    {
                        tutorialDummy.TakeDamage(finalDamage, false, meleeDamageType);
                    }
                    else if (iDamageable != null)
                    {
                        damageable.TakeDamage(Mathf.RoundToInt(finalDamage), isCritical, meleeDamageType);
                        ReportDebug($"Golpe a {enemy.name}: DUMMY DE TUTORIAL DETECTADO (Tag). Enviando {finalDamage:F2} de daño de {damageTypeForDummy}", 1);
                    }
                }
                else
                {
                    bool attackSuccessful = ExecuteAttack(enemy.gameObject, finalAttackDamage);

                    if (attackSuccessful)
                    {
                        EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
                        if (enemyHealth != null)
                        {
                            switch (currentAttackIndex)
                            {
                                case 0:
                                    enemyHealth.ApplyStun(comboStunDurations[0]);
                                    break;
                                case 1:
                                    enemyHealth.ApplyStun(comboStunDurations[1]);
                                    break;
                                case 2:
                                    enemyHealth.ApplyStun(comboStunDurations[2]);
                                    break;
                            }
                        }
                    }
                    else
                    {
                        ReportDebug($"Efectos secundarios (Stun) cancelados en {enemy.name} porque el ataque fue bloqueado.", 1);
                    }
                }
            }

            MeatPillar meatPillar = enemy.GetComponent<MeatPillar>();
            if (meatPillar != null)
            {
                meatPillar.TakeDamage();
            }

            CombatEventsManager.TriggerPlayerHitEnemy(enemy.gameObject, true);

            PlayImpactVFX(enemy.transform.position);
            ReportDebug($"Golpe a {enemy.name} por {finalDamage} de daño.", 1);
        }

        if (!hitAnyEnemy)
        {
            //ReportDebug("Ataque no golpeó a ningún enemigo.", 1);
        }

        float displayDuration = gizmoDuration;

        switch (currentAttackIndex)
        {
            case 0: // ataque 1
                if (attack1Duration > 0f) displayDuration = attack1Duration;
                break;

            case 1: // ataque 2
                if (attack2SpinDuration > 0f) displayDuration = attack2SpinDuration;
                break;

            case 2: // ataque 3
                if (attack3ChargeDuration > 0f) displayDuration = attack3ChargeDuration;
                break;

            default:
                // fallback
                break;
        }

        if (currentAttackIndex == 0 || currentAttackIndex == 1)
        {
            float quickMax = 0.25f;
            if (displayDuration > quickMax) displayDuration = quickMax;
        }

        if (showGizmoRoutine != null)
        {
            StopCoroutine(showGizmoRoutine);
            showGizmoRoutine = null;
        }

        showGizmoRoutine = StartCoroutine(ShowGizmoCoroutine(displayDuration));
    }

    private bool ExecuteAttack(GameObject target, float damageAmount)
    {
        if (target.TryGetComponent<DrogathEnemy>(out var blockSystem) && target.TryGetComponent<EnemyHealth>(out var health))
        {
            // Verificar si el ataque es bloqueado
            if (blockSystem.ShouldBlockDamage(hitPoint.transform.position))
            {
                ReportDebug("Ataque bloqueado por DrogathEnemy.", 1);
                return false;
            }

            health.TakeDamage(damageAmount, false, AttackDamageType.Melee);
            return true;
        }
        else if (target.TryGetComponent<EnemyHealth>(out var enemyHealth))
        {
            enemyHealth.TakeDamage(damageAmount, false, AttackDamageType.Melee);
            return true;
        }

        return true;
    }

    private void ApplyKnockbackSafe(Collider enemy)
    {
        EnemyKnockbackHandler knockbackHandler = enemy.GetComponent<EnemyKnockbackHandler>();
        if (knockbackHandler == null || playerHealth == null) return;

        float knockbackForce = 0f;

        switch (playerHealth.CurrentLifeStage)
        {
            case PlayerHealth.LifeStage.Young:
                knockbackForce = knockbackYoung;
                break;
            case PlayerHealth.LifeStage.Adult:
                knockbackForce = knockbackAdult;
                break;
            case PlayerHealth.LifeStage.Elder:
                knockbackForce = knockbackElder;
                break;
        }

        if (knockbackForce > 0)
        {
            Vector3 knockbackDirection = (enemy.transform.position - transform.position).normalized;
            knockbackDirection.y = 0;

            float limitedForce = Mathf.Min(knockbackForce, knockbackMaxDistance);
            knockbackHandler.TriggerKnockback(knockbackDirection, limitedForce, 0.25f);
        }
    }

    private IEnumerator ShowGizmoCoroutine(float duration)
    {
        if (!canShowHitGizmo) yield break;
        
        float time = (duration > 0f) ? duration : gizmoDuration;

        showGizmo = true;
        if (useBoxCollider && visualBoxHit != null) visualBoxHit.SetActive(true);
        else if (visualSphereHit != null) visualSphereHit.SetActive(true);

        yield return new WaitForSeconds(time);

        showGizmo = false;
        if (useBoxCollider && visualBoxHit != null) visualBoxHit.SetActive(false);
        else if (visualSphereHit != null) visualSphereHit.SetActive(false);

        showGizmoRoutine = null;
    }

    #region VFX Methods

    /// <summary>
    /// Reproduce el efecto de impacto en una posición específica.
    /// </summary>
    private void PlayImpactVFX(Vector3 position)
    {
        if (meleeImpactVFX == null) return;

        Instantiate(meleeImpactVFX, position, Quaternion.identity);
    }

    #endregion

    private void OnDrawGizmos()
    {
        if (hitPoint == null || !showGizmo) return;

        Gizmos.color = Color.red;

        if (useBoxCollider)
        {
            Vector3 boxCenter = hitPoint.position;
            Vector3 boxHalfExtents = new Vector3(hitRadius, hitRadius, hitRadius);
            Quaternion boxRotation = Quaternion.identity;

            Gizmos.matrix = Matrix4x4.TRS(boxCenter, boxRotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, boxHalfExtents * 2f);
        }
        else
        {
            Gizmos.DrawWireSphere(hitPoint.position, hitRadius);
        }
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[PlayerMeleeAttack] {message}");
                break;
            case 2:
                Debug.LogWarning($"[PlayerMeleeAttack] {message}");
                break;
            case 3:
                Debug.LogError($"[PlayerMeleeAttack] {message}");
                break;
            default:
                Debug.Log($"[PlayerMeleeAttack] {message}");
                break;
        }
    }
}