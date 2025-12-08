using UnityEngine;
using System.Collections;

/// <summary>
/// Clase que maneja el lanzamiento y recuperación del escudo del jugador.
/// </summary>
public class PlayerShieldController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayerStatsManager statsManager;
    [SerializeField] private GameObject shieldPrefab;
    [SerializeField] private Transform shieldSpawnPoint;
    [SerializeField] private PlayerMeleeAttack playerMeleeAttack;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerAudioController playerAudioController;
    [SerializeField] private ShieldSkill shieldSkill;
    [SerializeField] private Animator playerAnimator;

    [Header("Stats")]
    [Tooltip("Daño de ataque por defecto si no se encuentra PlayerStatsManager.")]
    [HideInInspector] private int fallbackshieldDamage = 10;
    [SerializeField] private int shieldDamage = 10;
    [Tooltip("Velocidad del escudo por defecto si no se encuentra PlayerStatsManager.")]
    [HideInInspector] private float fallbackshieldSpeed = 25f;
    [SerializeField] private float shieldSpeed = 25f;
    [Tooltip("Distancia máxima del escudo por defecto si no se encuentra PlayerStatsManager.")]
    [HideInInspector] private float fallbackshieldMaxDistance = 30f;
    [SerializeField] private float shieldMaxDistance = 30f;
    [Tooltip("Cantidad máxima de rebotes del escudo por defecto si no se encuentra PlayerStatsManager.")]
    [HideInInspector] private int fallbackshieldMaxRebounds = 2;
    [SerializeField] private int shieldMaxRebounds = 2;
    [Tooltip("Radio de rebote del escudo por defecto si no se encuentra PlayerStatsManager.")]
    [HideInInspector] private float fallbackshieldReboundRadius = 15f;
    [SerializeField] private float shieldReboundRadius = 15f;
    [SerializeField] private bool canShieldRebound = true;

    [Header("Configuración por Etapa - JOVEN")]
    [SerializeField] private int youngShieldDamage = 4;
    [SerializeField] private float youngShieldSpeed = 30f;
    [SerializeField] private int youngMaxRebounds = 2;
    [SerializeField] private float youngReboundRadius = 15f;

    [Header("Configuración por Etapa - ADULTO")]
    [SerializeField] private int adultShieldDamage = 7;
    [SerializeField] private float adultShieldSpeed = 25f;
    [SerializeField] private float adultKnockbackForce = 1.5f;

    [Header("Configuración por Etapa - VIEJO")]
    [SerializeField] private int elderShieldDamage = 12;
    [SerializeField] private float elderShieldSpeed = 20f;
    [SerializeField] private bool elderCanPierce = true;
    [SerializeField] private int elderMaxPierceTargets = 5;

    [Header("Animación")]
    [SerializeField] private float throwAnimationDuration = 0.15f;

    private int finalAttackDamage;
    private float finalAttackSpeed;
    private float damageMultiplier = 1f;
    private float speedMultiplier = 1f;

    public bool CanShieldRebound => canShieldRebound;

    private bool hasShield = true;
    private bool isThrowingShield = false;

    private int currentShieldDamage;
    private float currentShieldSpeed;

    public int ShieldDamage
    {
        get { return shieldDamage; }
        set { shieldDamage = value; }
    }

    public bool HasShield 
    { 
        get { return hasShield; }
        set { hasShield = value; }
    }

    public bool IsThrowingShield => isThrowingShield;

    private void Awake()
    {
        if (statsManager == null) statsManager = GetComponent<PlayerStatsManager>();
        if (playerMeleeAttack == null) playerMeleeAttack = GetComponent<PlayerMeleeAttack>();
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
        if (playerAnimator == null) playerAnimator = GetComponentInChildren<Animator>();
        if (playerAudioController == null) playerAudioController = GetComponent<PlayerAudioController>();
        if (shieldSkill == null) shieldSkill = GetComponent<ShieldSkill>();

        if (statsManager == null) ReportDebug("StatsManager no está asignado en PlayerShieldController. Usando valores de fallback.", 2);
        if (playerMeleeAttack == null) ReportDebug("PlayerMeleeAttack no encontrado. No se podrá verificar estado de ataque melee.", 2);
        if (playerMovement == null) ReportDebug("PlayerMovement no encontrado. Lock de rotación no funcionará.", 2);
        if (playerHealth == null) ReportDebug("PlayerHealth no encontrado. Configuración por etapa no funcionará.", 2);
        if (playerAnimator == null) ReportDebug("Animator no encontrado en hijos. Las animaciones de escudo no funcionarán.", 2);
        if (playerAudioController == null) ReportDebug("PlayerAudioController no encontrado. Los sonidos de escudo no funcionarán.", 2);
        if (shieldSkill == null) ReportDebug("ShieldSkill no encontrado. Los sonidos de escudo no funcionarán.", 2);
    }

    private void OnEnable()
    {
        PlayerStatsManager.OnStatChanged += HandleStatChanged;
    }

    private void OnDisable()
    {
        PlayerStatsManager.OnStatChanged -= HandleStatChanged;
    }

    private void OnDestroy()
    {
        PlayerStatsManager.OnStatChanged -= HandleStatChanged;
    }

    private void Start()
    {
        // Inicializar estadísticas del ataque a distancia desde PlayerStatsManager o usar valores de fallback
        float shieldDamageStat = statsManager != null ? statsManager.GetStat(StatType.ShieldAttackDamage) : fallbackshieldDamage;
        shieldDamage = Mathf.RoundToInt(shieldDamageStat);

        float shieldSpeedStat = statsManager != null ? statsManager.GetStat(StatType.ShieldSpeed) : fallbackshieldSpeed;
        shieldSpeed = shieldSpeedStat;

        float shieldMaxDistanceStat = statsManager != null ? statsManager.GetStat(StatType.ShieldMaxDistance) : fallbackshieldMaxDistance;
        shieldMaxDistance = shieldMaxDistanceStat;

        float shieldMaxReboundsStat = statsManager != null ? statsManager.GetStat(StatType.ShieldMaxRebounds) : fallbackshieldMaxRebounds;
        shieldMaxRebounds = Mathf.RoundToInt(shieldMaxReboundsStat);

        float shieldReboundRadiusStat = statsManager != null ? statsManager.GetStat(StatType.ShieldReboundRadius) : fallbackshieldReboundRadius;
        shieldReboundRadius = shieldReboundRadiusStat;

        // Inicializar estadísticas globales que afectan a todos los ataques desde PlayerStatsManager o usar valores fallback
        float damageMultiplierStat = statsManager != null ? statsManager.GetStat(StatType.AttackDamage) : 1f;
        damageMultiplier = damageMultiplierStat;

        float speedMultiplierStat = statsManager != null ? statsManager.GetStat(StatType.AttackSpeed) : 1f;
        speedMultiplier = speedMultiplierStat;

        if (shieldPrefab != null) shieldPrefab.SetActive(false);

        CalculateStats();
    }

    private void HandleStatChanged(StatType statType, float newValue)
    {
        switch (statType)
        {
            case StatType.ShieldAttackDamage:
                shieldDamage = Mathf.RoundToInt(newValue);
                break;
            case StatType.ShieldSpeed:
                shieldSpeed = newValue;
                break;
            case StatType.ShieldMaxDistance:
                shieldMaxDistance = newValue;
                break;
            case StatType.ShieldMaxRebounds:
                shieldMaxRebounds = Mathf.RoundToInt(newValue);
                break;
            case StatType.ShieldReboundRadius:
                shieldReboundRadius = newValue;
                break;
            case StatType.AttackDamage:
                damageMultiplier = newValue;
                break;
            case StatType.AttackSpeed:
                speedMultiplier = newValue;
                break;
            default:
                return;
        }

        CalculateStats();
        ReportDebug($"Estadística {statType} actualizada a {newValue}.", 1);
    }

    // Metodo para recalcular las estadísticas del escudo
    private void CalculateStats()
    {
        finalAttackDamage = Mathf.RoundToInt(shieldDamage * damageMultiplier);
        finalAttackSpeed = shieldSpeed * speedMultiplier;

        currentShieldDamage = finalAttackDamage;
        currentShieldSpeed = finalAttackSpeed;

        ReportDebug($"Estadísticas recalculadas: " +
                    $"Daño = {shieldDamage} x {damageMultiplier} = {currentShieldDamage}, " +
                    $"Velocidad = {shieldSpeed} x {speedMultiplier} = {currentShieldSpeed}", 1);
    }

    private void Update()
    {
        //if (Input.GetMouseButtonDown(1) && hasShield && !isThrowingShield)
        //{
        //    // Verificar que no esté en medio de un ataque melee
        //    if (playerMeleeAttack != null && playerMeleeAttack.IsAttacking)
        //    {
        //        ReportDebug("No se puede lanzar el escudo mientras se ataca en melee.", 1);
        //        return;
        //    }

        //    StartCoroutine(ThrowShieldSequence());
        //}
    }

    /// <summary>
    /// Método público llamado por PlayerCombatActionManager para ejecutar el lanzamiento del escudo.
    /// </summary>
    public IEnumerator ExecuteShieldThrowFromManager()
    {
        if (!hasShield || isThrowingShield) yield break;

        if (playerMeleeAttack != null && playerMeleeAttack.IsAttacking)
        {
            ReportDebug("No se puede lanzar el escudo mientras se ataca en melee.", 1);
            yield break;
        }

        isThrowingShield = true;

        if (playerMovement != null)
        {
            playerMovement.SetCanMove(false);
            playerMovement.StopForcedMovement();
        }

        Vector3 aimDir;
        bool aimFound = TryGetAimDirection(out aimDir);

        if (!aimFound)
        {
            aimDir = transform.forward;
        }
        else
        {
            RotateTowardsAimInstant();
        }

        if (playerMovement != null)
        {
            playerMovement.LockFacingTo8Directions(aimDir, true);
            yield return StartCoroutine(WaitForRotationLock());
            playerMovement.ForceApplyLockedRotation();
        }

        if (playerAnimator != null) playerAnimator.SetTrigger("ThrowShield");

        ThrowShield();

        yield return new WaitForSeconds(throwAnimationDuration);

        if (playerMovement != null)
        {
            playerMovement.UnlockFacing();
            playerMovement.SetCanMove(true);
        }

        float remainingActionTime = 0.25f - throwAnimationDuration;
        if (remainingActionTime > 0) yield return new WaitForSeconds(remainingActionTime);

        isThrowingShield = false;
    }

    /// <summary>
    /// Secuencia de lanzamiento del escudo (similar a AttackSequence del melee):
    /// 1) Obtener dirección del mouse
    /// 2) Lockear rotación snapped a 8 direcciones
    /// 3) Esperar a que la rotación alcance el objetivo
    /// 4) Lanzar el escudo
    /// 5) Mantener bloqueo hasta que el escudo regrese
    /// </summary>
    private IEnumerator ThrowShieldSequence()
    {
        if (!hasShield || isThrowingShield) yield break;

        // Verificar que no esté en medio de un ataque melee
        if (playerMeleeAttack != null && playerMeleeAttack.IsAttacking)
        {
            ReportDebug("No se puede lanzar el escudo mientras se ataca en melee.", 1);
            yield break;
        }

        isThrowingShield = true;

        // Dirección objetivo
        Vector3 mouseWorldDir;
        if (!TryGetAimDirection(out mouseWorldDir))
        {
            mouseWorldDir = transform.forward;
        }
        else
        {
            // fallback: aplicar rotación instantánea snappeada
            RotateTowardsAimInstant();
        }

        if (playerMovement != null)
        {
            playerMovement.LockFacingTo8Directions(mouseWorldDir, true);
            yield return StartCoroutine(WaitForRotationLock());
            playerMovement.ForceApplyLockedRotation();
        }

        // Ejecutar lanzamiento
        ThrowShield();

        yield return new WaitForSeconds(0.25f);

        // Desbloquear rotación inmediatamente después del lanzamiento
        // (el jugador puede moverse mientras el escudo vuela)
        if (playerMovement != null) playerMovement.UnlockFacing();

        isThrowingShield = false;
    }

    private IEnumerator WaitForRotationLock()
    {
        float maxWait = 0.25f;
        float start = Time.time;
        while (Time.time - start < maxWait)
        {
            if (playerMovement != null)
            {
                if (Quaternion.Angle(transform.rotation, playerMovement.GetLockedRotation()) <= 2f)
                    break;
            }
            else break;
            yield return null;
        }
    }

    // Rota instantaneamente al mouse proyectado en el plano horizontal (y = transform.position.y), con snap a 8 direcciones.
    private void RotateTowardsAimInstant()
    {
        if (!TryGetAimDirection(out Vector3 dir)) return;

        transform.rotation = Quaternion.LookRotation(dir);
    }

    /// <summary>
    /// Función que Lanza el escudo en la dirección del mouse y lo instancia en el punto y altura del spawn point.
    /// </summary>
    private void ThrowShield()
    {
        hasShield = false;
        if (playerAnimator != null) playerAnimator.SetBool("HaveShield", false);

        bool isBerserker = shieldSkill != null && shieldSkill.IsActive;

        if (playerAudioController != null)
        {
            playerAudioController.PlayThrowShieldSound(isBerserker);
        }

        Vector3 direction;
        if (!TryGetAimDirection(out direction))
        {
            direction = transform.forward;
        }

        GameObject shieldInstance = ShieldPooler.Instance.GetPooledObject();

        if (shieldInstance != null)
        {
            shieldInstance.transform.position = shieldSpawnPoint.position;
            shieldInstance.transform.rotation = Quaternion.LookRotation(direction);

            ShieldConfig config = GetShieldConfigForCurrentStage();

            Shield shieldScript = shieldInstance.GetComponent<Shield>();
            shieldScript.Throw(
                this,
                direction,
                config.canRebound,
                config.maxRebounds,
                config.reboundRadius,
                config.damage,
                config.speed,
                config.maxDistance,
                config.canPierce,
                config.maxPierceTargets,
                config.knockbackForce,
                playerHealth.CurrentLifeStage,
                isBerserker
            );

            ReportDebug($"Escudo lanzado ({playerHealth.CurrentLifeStage}): Daño={config.damage}, Velocidad={config.speed}", 1);
        }
        else
        {
            hasShield = true;
            if (playerAnimator != null) playerAnimator.SetBool("HaveShield", true);
            ReportDebug("No se pudo obtener instancia del escudo del pool.", 2);
        }
    }

    private bool TryGetAimDirection(out Vector3 outDir)
    {
        outDir = transform.forward;

        if (GamepadPointer.Instance != null && GamepadPointer.Instance.GetCurrentActiveDevice() == GamepadPointer.Instance.GetCurrentGamepad())
        {
            Vector2 stickAim = GamepadPointer.Instance.GetAimDirectionValue();

            if (stickAim.magnitude > 0.0001f)
            {
                Camera camera = Camera.main;
                if (camera == null) return false;

                Vector3 camForward = camera.transform.forward;
                camForward.y = 0f;
                camForward.Normalize();

                Vector3 camRight = camera.transform.right;
                camRight.y = 0f;
                camRight.Normalize();

                Vector3 targetDirection = camForward * stickAim.y + camRight * stickAim.x;

                if (targetDirection.sqrMagnitude > 0.0001f)
                {
                    outDir = targetDirection.normalized;
                    return true;
                }
            }

            outDir = transform.forward;
            return true;
        }

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

        outDir = transform.forward;
        return false;
    }

    private ShieldConfig GetShieldConfigForCurrentStage()
    {
        if (playerHealth == null)
        {
            return new ShieldConfig
            {
                damage = finalAttackDamage,
                speed = finalAttackSpeed,
                maxDistance = shieldMaxDistance,
                canRebound = true,
                maxRebounds = 2,
                reboundRadius = 15f,
                canPierce = false,
                maxPierceTargets = 0,
                knockbackForce = 0f
            };
        }

        switch (playerHealth.CurrentLifeStage)
        {
            case PlayerHealth.LifeStage.Young:
                return new ShieldConfig
                {
                    damage = Mathf.RoundToInt(youngShieldDamage * damageMultiplier),
                    speed = youngShieldSpeed * speedMultiplier,
                    maxDistance = shieldMaxDistance,
                    canRebound = true,
                    maxRebounds = youngMaxRebounds,
                    reboundRadius = youngReboundRadius,
                    canPierce = false,
                    maxPierceTargets = 0,
                    knockbackForce = 0f
                };

            case PlayerHealth.LifeStage.Adult:
                return new ShieldConfig
                {
                    damage = Mathf.RoundToInt(youngShieldDamage * damageMultiplier),
                    speed = youngShieldSpeed * speedMultiplier,
                    maxDistance = shieldMaxDistance,
                    canRebound = false,
                    maxRebounds = 0,
                    reboundRadius = 0f,
                    canPierce = false,
                    maxPierceTargets = 0,
                    knockbackForce = adultKnockbackForce
                };

            case PlayerHealth.LifeStage.Elder:
                return new ShieldConfig
                {
                    damage = Mathf.RoundToInt(youngShieldDamage * damageMultiplier),
                    speed = youngShieldSpeed * speedMultiplier,
                    maxDistance = shieldMaxDistance,
                    canRebound = false,
                    maxRebounds = 0,
                    reboundRadius = 0f,
                    canPierce = elderCanPierce,
                    maxPierceTargets = elderMaxPierceTargets,
                    knockbackForce = 0f
                };

            default:
                return new ShieldConfig
                {
                    damage = finalAttackDamage,
                    speed = finalAttackSpeed,
                    maxDistance = shieldMaxDistance,
                    canRebound = true,
                    maxRebounds = 2,
                    reboundRadius = 15f,
                    canPierce = false,
                    maxPierceTargets = 0,
                    knockbackForce = 0f
                };
        }
    }

    public void CancelThrow()
    {
        if (!isThrowingShield) return;

        StopAllCoroutines();

        isThrowingShield = false;

        if (playerMovement != null)
        {
            playerMovement.UnlockFacing();
        }

        if (hasShield)
        {
            if (playerAnimator != null)
            {
                playerAnimator.SetBool("HaveShield", true);
                playerAnimator.ResetTrigger("ThrowShield");
            }
        }

        ReportDebug("Lanzamiento de escudo cancelado.", 1);
    }

    // El escudo llama a esta función cuando regresa
    public void CatchShield()
    {
        hasShield = true;
        
        if (playerAnimator != null) playerAnimator.SetBool("HaveShield", true);

        bool isBerserker = shieldSkill != null && shieldSkill.IsActive;

        if (playerAudioController != null)
        {
            playerAudioController.PlayCatchShieldSound(isBerserker);
        }

        ReportDebug("Escudo recuperado.", 1);
    }

    public bool CanThrowShield()
    {
        return hasShield && !isThrowingShield;
    }

    // Permite cambiar si el escudo puede rebotar o no
    public void SetRebound(bool value) => canShieldRebound = value;

    private struct ShieldConfig
    {
        public int damage;
        public float speed;
        public float maxDistance;
        public bool canRebound;
        public int maxRebounds;
        public float reboundRadius;
        public bool canPierce;
        public int maxPierceTargets;
        public float knockbackForce;
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
                Debug.Log($"[PlayerShieldController] {message}");
                break;
            case 2:
                Debug.LogWarning($"[PlayerShieldController] {message}");
                break;
            case 3:
                Debug.LogError($"[PlayerShieldController] {message}");
                break;
            default:
                Debug.Log($"[PlayerShieldController] {message}");
                break;
        }
    }
}