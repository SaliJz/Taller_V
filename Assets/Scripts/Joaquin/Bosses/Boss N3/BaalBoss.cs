using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class BaalBoss : MonoBehaviour, IAnimEventHandler
{
    #region Inspector – References

    [Header("Core References")]
    [SerializeField] private Transform hitPoint;
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private TheHostAnimCtrl animController;
    [SerializeField] private Transform player;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip exceptionAnticipationSFX;
    [SerializeField] private AudioClip bufferOverrunChargeSFX;
    [SerializeField] private AudioClip bufferOverrunImpactSFX;
    [SerializeField] private AudioClip teleportSFX;
    [SerializeField] private AudioClip damagedSFX;
    [SerializeField] private AudioClip deathSFX;

    [Header("VFX Prefabs")]
    [SerializeField] private GameObject exceptionProjectilePrefab;
    [SerializeField] private GameObject necroticClusterPrefab;
    [SerializeField] private GameObject larvaPrefab;
    [SerializeField] private GameObject memoryLeakPrefab;
    [SerializeField] private GameObject teleportVFXPrefab;
    [SerializeField] private GameObject bufferOverrunImpactVFXPrefab;
    [Tooltip("Prefab indicador de trayectoria en el suelo durante la embestida (opcional).")]
    [SerializeField] private GameObject bufferOverrunTrailIndicatorPrefab;
    [SerializeField] private GameObject nodePurulentosPrefab;
    [SerializeField] private ParticleSystem phaseTransitionVFX;

    #endregion

    #region Inspector – Base Statistics

    [Header("Base Stats")]
    [SerializeField] private float maxHealth = 120f;
    [SerializeField] private float moveSpeed = 3.5f;

    #endregion

    #region Inspector – NavMesh

    [Header("NavMesh")]
    [Tooltip("Radio seguro para instanciar larvas fuera del obstáculo.")]
    [SerializeField] private float safeSpawnRadius = 3f;

    #endregion

    #region Inspector – Excepción Fatal

    [Header("Excepción Fatal")]
    [Tooltip("Daño mínimo.")]
    [SerializeField] private float exceptionDamageMin = 2f;
    [Tooltip("Daño máximo.")]
    [SerializeField] private float exceptionDamageMax = 4f;
    [Tooltip("Velocidad de los proyectiles.")]
    [SerializeField] private float exceptionProjectileSpeed = 15f;
    [Tooltip("Distancia desde la que empieza a escalar el daño.")]
    [SerializeField] private float exceptionScaleStartDist = 6f;
    [Tooltip("Distancia en la que se alcanza el daño máximo.")]
    [SerializeField] private float exceptionScaleMaxDist = 20f;
    [Tooltip("Duración total del brillo de anticipación. Las flechas del suelo aparecen en los últimos exceptionDirPreviewTime.")]
    [SerializeField] private float exceptionAnticipationTime = 1.2f;
    [Tooltip("Segundos en que las trayectorias se proyectan en el suelo antes de disparar.")]
    [SerializeField] private float exceptionDirPreviewTime = 0.8f;
    [Tooltip("Pausa estática tras el disparo antes de continuar el ciclo.")]
    [SerializeField] private float exceptionShortRecovery = 1.5f;

    #endregion

    #region Inspector – Buffer Overrun + Clúster Necrótico

    [Header("Buffer Overrun")]
    [SerializeField] private float bufferOverrunDamage = 10f;
    [SerializeField] private float bufferOverrunDashDistance = 7.5f;
    [SerializeField] private float bufferOverrunAttackRange = 7.5f;
    [Tooltip("Tiempo de carga / anticipación.")]
    [SerializeField] private float bufferOverrunChargeDuration = 1f;
    [Tooltip("Tiempo de ejecución del dash.")]
    [SerializeField] private float bufferOverrunDashDuration = 1f;
    [Tooltip("Pausa larga tras el impacto mientras el clúster está activo.")]
    [SerializeField] private float bufferOverrunLongRecovery = 3f;
    [Tooltip("Espera adicional tras la recuperación larga antes de reiniciar el ciclo.")]
    [SerializeField] private float cycleRepeatDelay = 2f;
    [Tooltip("Cooldown de reutilización dinámica.")]
    [SerializeField] private float bufferOverrunCooldown = 8.5f;

    [Header("Clúster Necrótico")]
    [Tooltip("Segundos que dura el clúster antes de colapsar.")]
    [SerializeField] private float clusterDuration = 3f;
    [SerializeField] private float clusterDPS = 1f;
    [SerializeField] private float clusterRadius = 1.5f;
    [Tooltip("Larvas liberadas al colapsar el clúster.")]
    [SerializeField] private int clusterLarvaCount = 2;
    [Tooltip("Fracción de velocidad eliminada al jugador dentro del clúster (0.2 = 20%).")]
    [SerializeField] private float clusterSlowFraction = 0.2f;
    [Tooltip("Si false, el clúster ralentiza pero no aplica daño.")]
    [SerializeField] private bool clusterDealDamage = true;

    #endregion

    #region Inspector – Desfragmentación Evasiva

    [Header("Desfragmentación Evasiva")]
    [Tooltip("Golpes necesarios para activar la evasión.")]
    [SerializeField] private int defragHitThreshold = 3;
    [Tooltip("Ventana de tiempo en la que se cuentan los golpes.")]
    [SerializeField] private float defragHitWindow = 2f;
    [Tooltip("Distancia máxima del jugador al recibir los golpes.")]
    [SerializeField] private float defragActivationRange = 3f;
    [Tooltip("Distancia a la que el jefe se aleja al evadir.")]
    [SerializeField] private float defragTeleportDist = 7f;
    [SerializeField] private float defragCooldown = 6f;

    #endregion

    #region Inspector – Fuga de Memoria

    [Header("Fuga de Memoria")]
    [SerializeField] private float memoryLeakDuration = 4f;
    [SerializeField] private float memoryLeakDPS = 2f;
    [SerializeField] private float memoryLeakRadius = 2f;
    [Tooltip("Segundos tras el TP hasta que el charco aparece (da tiempo a ver el indicador).")]
    [SerializeField] private float memoryLeakFormationDelay = 0.35f;
    [Tooltip("Prefab indicador de suelo para el charco de Desfragmentación (opcional).")]
    [SerializeField] private GameObject memoryLeakIndicatorPrefab;

    #endregion

    #region Inspector – Latencia Cero

    [Header("Latencia Cero")]
    [Tooltip("Segundos fuera de rango para activar la habilidad.")]
    [SerializeField] private float latenciaActivationTime = 10f;
    [Tooltip("Distancia mínima del jugador que activa el contador.")]
    [SerializeField] private float latenciaActivationRange = 6f;
    [SerializeField] private float latenciaDamage = 5f;
    [SerializeField] private float latenciaCooldown = 8f;
    [Tooltip("Pausa de anticipación visible tras el TP antes del impacto (el indicador de suelo dura este tiempo).")]
    [SerializeField] private float latenciaAnticipationDuration = 0.6f;
    [Tooltip("Prefab indicador de suelo para la anticipación de Latencia Cero.")]
    [SerializeField] private GameObject latenciaGroundIndicatorPrefab;

    #endregion

    #region Inspector – Arquitectura Distribuida (50 %)

    [Header("Arquitectura Distribuida (50% HP)")]
    [Tooltip("Radio en que se distribuyen los nodos alrededor del jefe.")]
    [SerializeField] private float nodeSpawnRadius = 5f;
    [SerializeField] private int maxNodes = 3;
    [Tooltip("Porcentaje del blindaje que se quita al destruir un nodo.")]
    [SerializeField] private float shieldReductionPerNode = 0.33f;

    #endregion



    #region Inspector – Attack Anticipation

    [Header("Anticipación - Excepción Fatal")]
    [SerializeField] private AudioClip exceptionFatalAnticipationSFX;
    [SerializeField] private float exceptionFatalAnticipationDuration = 0.5f;

    [Header("Anticipación - Buffer Overrun")]
    [SerializeField] private AudioClip bufferOverrunAnticipationSFX;
    [SerializeField] private float bufferOverrunAnticipationDuration = 0.6f;

    [Header("Anticipación - Latencia Cero (post-TP)")]
    [SerializeField] private AudioClip latenciaAnticipationSFX;

    [Header("Anticipación - Enemy Visual Effects")]
    [SerializeField] private EnemyVisualEffects enemyVisualEffects;

    #endregion

    #region Inspector – Debug

    [Header("Debug")]
    [SerializeField] private bool showDebugGUI = false;

    #endregion

    #region Internal State

    private enum BossState
    {
        Idle,
        Attacking,
        Teleporting,
        Phase50,
        Dead
    }

    private BossState currentState = BossState.Idle;

    private float currentHealth;

    // Hitos únicos
    private bool phase50Triggered = false;

    // Estadísticas efectivas
    private float effectiveMoveSpeed;
    private float effectiveProjSpeed;
    private float effectiveBufferCooldown;
    private float effectiveExceptionRecovery;

    // Desfragmentación – contador de hits cuerpo a cuerpo
    private int defragHitCount = 0;
    private float defragLastHitTime = -999f;
    private bool defragOnCooldown = false;
    private bool defragInterruptPending = false;

    // Latencia Cero – temporizador fuera de rango
    private float timeOutOfRange = 0f;
    private bool latenciaOnCooldown = false;
    private bool latenciaInterruptPending = false;

    // Arquitectura Distribuida
    private Vector3 _roomCenter;
    private bool architecturePhaseActive = false;
    private int activeNodeCount = 0;
    private float currentShieldReduction = 0f;
    private readonly List<GameObject> spawnedNodes = new List<GameObject>();

    // Efectos instanciados y limpiados al morir
    private readonly List<GameObject> instantiatedEffects = new List<GameObject>();

    // Anticipación de ataques
    private bool _isInAnticipation = false;
    private Coroutine _anticipationCoroutine = null;

    // Caché de componentes del jugador
    private PlayerHealth playerHealth;
    private PlayerMovement playerMovement;



    #endregion

    #region Animation Hashes

    private static readonly int AnimID_Walking = Animator.StringToHash("Walking");
    private static readonly int AnimID_Death = Animator.StringToHash("Death");
    private static readonly int AnimID_ExceptionFatal = Animator.StringToHash("ExceptionFatal");
    private static readonly int AnimID_BufferOverrunCharge = Animator.StringToHash("BufferOverrunCharge");
    private static readonly int AnimID_BufferOverrunDash = Animator.StringToHash("BufferOverrunDash");
    private static readonly int AnimID_Teleport = Animator.StringToHash("Teleport");

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        InitializeComponents();
        InitializeEffectiveStats();
    }

    private void Start()
    {
        StartCoroutine(BossFlowSequence());
        StartCoroutine(DynamicBehaviorMonitor());
    }

    private void OnEnable()
    {
        if (enemyHealth == null) return;
        enemyHealth.OnDeath += HandleEnemyDeath;
        enemyHealth.OnHealthChanged += HandleHealthChanged;
        enemyHealth.OnDamaged += HandleDamaged;
    }

    private void OnDisable()
    {
        if (enemyHealth == null) return;
        enemyHealth.OnDeath -= HandleEnemyDeath;
        enemyHealth.OnHealthChanged -= HandleHealthChanged;
        enemyHealth.OnDamaged -= HandleDamaged;
    }

    #endregion

    #region Initialization

    private void InitializeComponents()
    {
        if (enemyHealth == null) enemyHealth = GetComponent<EnemyHealth>();
        if (enemyHealth != null) enemyHealth.SetMaxHealth(maxHealth);
        currentHealth = maxHealth;

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.speed = moveSpeed;

        if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>();

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
                playerHealth = playerObj.GetComponent<PlayerHealth>();
                playerMovement = playerObj.GetComponent<PlayerMovement>();
            }
        }

        if (player == null) ReportDebug("Jugador no encontrado en la escena.", 3);
    }

    private void InitializeEffectiveStats()
    {
        effectiveMoveSpeed = moveSpeed;
        effectiveProjSpeed = exceptionProjectileSpeed;
        effectiveBufferCooldown = bufferOverrunCooldown;
        effectiveExceptionRecovery = exceptionShortRecovery;
    }


    #endregion

    #region EnemyHealth Event Handlers

    private void HandleHealthChanged(float newCurrent, float newMax)
    {
        currentHealth = newCurrent;
        maxHealth = newMax;

        if (audioSource != null && damagedSFX != null)
            audioSource.PlayOneShot(damagedSFX, 0.75f);
    }

    /// <summary>
    /// Llamado por EnemyHealth.OnDamaged cada vez que el jefe recibe daño.
    /// Gestiona el contador del interruptor de agresión (Desfragmentación Evasiva).
    /// Solo cuenta golpes con el jugador dentro de defragActivationRange.
    /// Usa sqrMagnitude conforme al §3c del documento.
    /// </summary>
    private void HandleDamaged()
    {
        if (player == null || defragOnCooldown || defragInterruptPending) return;

        float sqrDist = (transform.position - player.position).sqrMagnitude;
        if (sqrDist > defragActivationRange * defragActivationRange) return;

        float now = Time.time;
        if (now - defragLastHitTime > defragHitWindow) defragHitCount = 0;

        defragHitCount++;
        defragLastHitTime = now;

        if (defragHitCount >= defragHitThreshold)
        {
            defragInterruptPending = true;
            defragHitCount = 0;
            ReportDebug("DESFRAGMENTACIÓN: Umbral de agresión alcanzado. Interrupción pendiente.", 1);
        }
    }

    private void HandleEnemyDeath(GameObject enemy)
    {
        if (enemy != gameObject) return;

        currentState = BossState.Dead;

        CancelAnticipation();
        StopAllCoroutines();
        CleanUpEffects();

        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        if (audioSource != null && deathSFX != null) audioSource.PlayOneShot(deathSFX);

        this.enabled = false;
    }

    #endregion

    #region Main AI Loop – Ciclo Base

    /// <summary>
    /// Corrutina principal del ciclo base.
    /// Evalúa hitos de salud e interrupciones dinámicas al inicio de cada ciclo
    /// y entre cada fase.
    /// </summary>
    private IEnumerator BossFlowSequence()
    {
        yield return new WaitForSeconds(1.5f);

        while (currentHealth > 0)
        {
            // Esperar si el jefe está aturdido
            while (enemyHealth != null && enemyHealth.IsStunned)
            {
                StopAgent();
                yield return null;
            }

            // Hito 50 %: Arquitectura Distribuida
            if (!phase50Triggered && currentHealth <= maxHealth * 0.5f)
            {
                phase50Triggered = true;
                yield return StartCoroutine(ExecuteArquitecturaDistribuida());
                if (currentHealth <= 0) yield break;
            }

            // Interrupciones dinámicas al inicio del ciclo
            if (HasPendingInterrupt())
            {
                yield return StartCoroutine(ResolveDynamicInterrupt());
                continue;
            }

            // FASE 1 – Excepción Fatal
            yield return StartCoroutine(ExecuteExcepcionFatal());
            if (currentHealth <= 0) yield break;
            if (HasPendingInterrupt()) { yield return StartCoroutine(ResolveDynamicInterrupt()); continue; }

            // FASE 2 – Recuperación corta
            yield return StartCoroutine(ShortRecovery());
            if (currentHealth <= 0) yield break;
            if (HasPendingInterrupt()) { yield return StartCoroutine(ResolveDynamicInterrupt()); continue; }

            // FASE 3 – Buffer Overrun
            yield return StartCoroutine(ExecuteBufferOverrun());
            if (currentHealth <= 0) yield break;
            if (HasPendingInterrupt()) { yield return StartCoroutine(ResolveDynamicInterrupt()); continue; }

            // FASE 4 – Recuperación larga
            yield return StartCoroutine(LongRecovery());
            if (currentHealth <= 0) yield break;
            if (HasPendingInterrupt()) { yield return StartCoroutine(ResolveDynamicInterrupt()); continue; }
        }
    }

    private bool HasPendingInterrupt() => defragInterruptPending || latenciaInterruptPending;

    /// <summary>
    /// Ejecuta y consume la interrupción dinámica de mayor prioridad.
    /// Desfragmentación tiene prioridad sobre Latencia Cero.
    /// </summary>
    private IEnumerator ResolveDynamicInterrupt()
    {
        if (defragInterruptPending)
        {
            defragInterruptPending = false;
            yield return StartCoroutine(ExecuteDesfragmentacion());
        }
        else if (latenciaInterruptPending)
        {
            latenciaInterruptPending = false;
            yield return StartCoroutine(ExecuteLatenciaCero());
        }
    }

    #endregion

    #region Dynamic Behavior Monitor – Corrutina Paralela

    /// <summary>
    /// Monitorea el interruptor de distancia (Latencia Cero) usando sqrMagnitude
    /// en el bucle principal
    /// </summary>
    private IEnumerator DynamicBehaviorMonitor()
    {
        float sqrActivationRange = latenciaActivationRange * latenciaActivationRange;

        while (currentHealth > 0)
        {
            if (currentState != BossState.Dead && player != null && !latenciaOnCooldown)
            {
                float sqrDist = (transform.position - player.position).sqrMagnitude;

                if (sqrDist > sqrActivationRange)
                {
                    timeOutOfRange += Time.deltaTime;
                    if (timeOutOfRange >= latenciaActivationTime && !latenciaInterruptPending)
                    {
                        latenciaInterruptPending = true;
                        timeOutOfRange = 0f;
                        ReportDebug("LATENCIA CERO: Jugador fuera de rango > 10 s. Interrupción pendiente.", 1);
                    }
                }
                else
                {
                    timeOutOfRange = 0f;
                }
            }

            yield return null;
        }
    }

    #endregion

    #region Fase Base 1 – Excepción Fatal

    private IEnumerator ExecuteExcepcionFatal()
    {
        ReportDebug("EXCEPCIÓN FATAL: Iniciando.", 1);
        currentState = BossState.Attacking;

        StopAgent();
        FacePlayer();

        if (audioSource != null && exceptionAnticipationSFX != null)
        {
            audioSource.PlayOneShot(exceptionAnticipationSFX);
        }

        if (animController != null) animController.PlayShotAttack();

        // Si el clip A_Shot tiene AnimEvent_AnticipationPause, la pausa llega aquí
        // de forma sincronizada. Si no existe el evento, el WaitForSeconds actúa
        // como fallback.
        float anticipationFallbackTimer = 0f;
        while (!_isInAnticipation && anticipationFallbackTimer < exceptionFatalAnticipationDuration + 1f)
        {
            anticipationFallbackTimer += Time.deltaTime;
            yield return null;
        }
        yield return new WaitUntil(() => !_isInAnticipation);

        float glowOnlyTime = Mathf.Max(0f, exceptionAnticipationTime - exceptionDirPreviewTime);
        yield return new WaitForSeconds(glowOnlyTime);

        ReportDebug("EXCEPCIÓN FATAL: Indicadores de dirección proyectados (0.8 s).", 1);
        yield return new WaitForSeconds(exceptionDirPreviewTime);

        FireEightDirectionProjectiles();

        ReportDebug("EXCEPCIÓN FATAL: 8 proyectiles disparados.", 1);
        currentState = BossState.Idle;
    }

    private void FireEightDirectionProjectiles()
    {
        if (exceptionProjectilePrefab == null)
        {
            ReportDebug("EXCEPCIÓN FATAL: Falta exceptionProjectilePrefab.", 2);
            return;
        }

        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 spawnPos = transform.position + Vector3.up * 0.5f;

            GameObject proj = Instantiate(exceptionProjectilePrefab, spawnPos, Quaternion.LookRotation(dir));
            instantiatedEffects.Add(proj);

            BaalProjectile projScript = proj.GetComponent<BaalProjectile>();
            if (projScript != null)
            {
                projScript.Initialize(
                    dir,
                    effectiveProjSpeed,
                    exceptionDamageMin,
                    exceptionDamageMax,
                    exceptionScaleStartDist,
                    exceptionScaleMaxDist,
                    transform.position
                );
            }
            else
            {
                Rigidbody rb = proj.GetComponent<Rigidbody>();
                if (rb != null) rb.linearVelocity = dir * effectiveProjSpeed;
                Destroy(proj, 3f);
            }
        }
    }

    #endregion

    #region Fase Base 2 – Recuperación Corta

    private IEnumerator ShortRecovery()
    {
        ReportDebug("RECUPERACIÓN CORTA: 1.5 s.", 1);
        float elapsed = 0f;
        while (elapsed < effectiveExceptionRecovery)
        {
            FacePlayer();
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    #endregion

    #region Fase Base 3 – Buffer Overrun

    private IEnumerator ExecuteBufferOverrun(bool calledByLatencia = false)
    {
        ReportDebug($"BUFFER OVERRUN: Iniciando{(calledByLatencia ? " (encadenado por Latencia Cero)" : "")}.", 1);
        currentState = BossState.Attacking;

        StopAgent();
        FacePlayer();

        if (audioSource != null && bufferOverrunChargeSFX != null)
        {
            audioSource.PlayOneShot(bufferOverrunChargeSFX);
        }

        if (animController != null) animController.PlayBufferPre();

        float bufferAnticipationFallback = 0f;
        while (!_isInAnticipation && bufferAnticipationFallback < bufferOverrunAnticipationDuration + 1f)
        {
            bufferAnticipationFallback += Time.deltaTime;
            yield return null;
        }
        yield return new WaitUntil(() => !_isInAnticipation);

        yield return new WaitForSeconds(bufferOverrunChargeDuration);

        if (animController != null) animController.PlayBufferAttack();

        Vector3 dashTarget = CalculateBufferOverrunTarget();
        yield return StartCoroutine(DashToPositionOrHit(dashTarget, bufferOverrunDashDuration));

        // Impacto
        Vector3 impactPoint = hitPoint != null ? hitPoint.position : transform.position;
        DealAreaDamage(impactPoint, bufferOverrunAttackRange, bufferOverrunDamage);

        if (audioSource != null && bufferOverrunImpactSFX != null)
        {
            audioSource.PlayOneShot(bufferOverrunImpactSFX);
        }

        if (bufferOverrunImpactVFXPrefab != null)
        {
            GameObject vfx = Instantiate(bufferOverrunImpactVFXPrefab, impactPoint, Quaternion.identity);
            instantiatedEffects.Add(vfx);
            Destroy(vfx, 2f);
        }

        SpawnNecroticCluster(impactPoint);

        ReportDebug("BUFFER OVERRUN: Impacto aplicado. Clúster Necrótico creado.", 1);
        currentState = BossState.Idle;
    }

    private Vector3 CalculateBufferOverrunTarget()
    {
        if (player == null)
        {
            return transform.position + transform.forward * bufferOverrunDashDistance;
        }

        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        dir.Normalize();

        return transform.position + dir * bufferOverrunDashDistance;
    }

    private void SpawnNecroticCluster(Vector3 position)
    {
        if (necroticClusterPrefab == null)
        {
            ReportDebug("BUFFER OVERRUN: Falta necroticClusterPrefab.", 2);
            return;
        }

        // Valida posición en el NavMesh
        Vector3 spawnPosition = position;
        NavMeshHit hit;

        // Buscam el punto más cercano en un radio de 10 unidades
        if (NavMesh.SamplePosition(position, out hit, 10.0f, NavMesh.AllAreas))
        {
            spawnPosition = hit.position;
        }
        else
        {
            // Si no encuentra suelo, cancela el spawn para evitar clusters flotantes
            ReportDebug("WARNING: No se encontró suelo NavMesh cerca de la posición.", 1);
            return;
        }

        // Instancia en la posición corregida
        GameObject cluster = Instantiate(necroticClusterPrefab, spawnPosition, Quaternion.identity);
        instantiatedEffects.Add(cluster);

        NecroticCluster clusterScript = cluster.GetComponent<NecroticCluster>();
        if (clusterScript != null)
        {
            clusterScript.Initialize(clusterDuration, clusterDPS, clusterRadius, larvaPrefab, clusterLarvaCount, clusterSlowFraction, clusterDealDamage);
        }
        else Destroy(cluster, clusterDuration);
    }

    #endregion

    #region Fase Base 4 – Recuperación Larga

    private IEnumerator LongRecovery()
    {
        ReportDebug("RECUPERACIÓN LARGA: 3 s (colapso del clúster → larvas) + 2 s.", 1);
        yield return new WaitForSeconds(bufferOverrunLongRecovery);
        yield return new WaitForSeconds(cycleRepeatDelay);
    }

    #endregion

    #region Comportamiento Dinámico – Desfragmentación Evasiva

    private IEnumerator ExecuteDesfragmentacion()
    {
        ReportDebug("DESFRAGMENTACIÓN EVASIVA: Ejecutando.", 1);
        currentState = BossState.Teleporting;
        defragOnCooldown = true;

        StopAgent();

        if (animController != null)
        {
            animController.PlayTP_pre();
            yield return new WaitForSeconds(animController.TPanitipacionTime);
        }

        Vector3 evasionPos = GetEvasionPosition(defragTeleportDist);
        PerformTeleport(evasionPos);

        // La Fuga de Memoria se forma en la posición de llegada con un breve
        // retraso visual para que coincida con la animación de impacto de TP.
        yield return new WaitForSeconds(memoryLeakFormationDelay);
        SpawnMemoryLeak(transform.position);

        if (memoryLeakIndicatorPrefab != null)
        {
            GameObject indicator = Instantiate(memoryLeakIndicatorPrefab, transform.position, Quaternion.identity);
            Destroy(indicator, memoryLeakFormationDelay + 0.1f);
        }

        yield return new WaitForSeconds(0.25f - Mathf.Min(0.25f, memoryLeakFormationDelay));

        FacePlayer();
        FireReprisalShot();

        ReportDebug("DESFRAGMENTACIÓN EVASIVA: Teleport y represalia completados.", 1);
        currentState = BossState.Idle;

        StartCoroutine(DefragCooldownRoutine());
    }

    /// <summary>
    /// Disparo de represalia: 3 proyectiles en arco frontal estrecho.
    /// </summary>
    private void FireReprisalShot()
    {
        if (exceptionProjectilePrefab == null) return;

        float[] angles = { -15f, 0f, 15f };
        foreach (float offset in angles)
        {
            Vector3 dir = Quaternion.Euler(0f, offset, 0f) * transform.forward;
            Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
            GameObject proj = Instantiate(exceptionProjectilePrefab, spawnPos, Quaternion.LookRotation(dir));
            instantiatedEffects.Add(proj);

            BaalProjectile ps = proj.GetComponent<BaalProjectile>();
            if (ps != null)
            {
                ps.Initialize(dir, effectiveProjSpeed,
                              exceptionDamageMin, exceptionDamageMax,
                              exceptionScaleStartDist, exceptionScaleMaxDist,
                              transform.position);
            }
            else
            {
                Rigidbody rb = proj.GetComponent<Rigidbody>();
                if (rb != null) rb.linearVelocity = dir * effectiveProjSpeed;
                Destroy(proj, 3f);
            }
        }
    }

    private IEnumerator DefragCooldownRoutine()
    {
        yield return new WaitForSeconds(defragCooldown);
        defragOnCooldown = false;
        ReportDebug("DESFRAGMENTACIÓN EVASIVA: Cooldown finalizado.", 1);
    }

    private Vector3 GetEvasionPosition(float distance)
    {
        if (player == null)
        {
            return transform.position + transform.right * distance;
        }

        Vector3 awayDir = (transform.position - player.position).normalized;
        Vector3 candidate = transform.position + awayDir * distance;

        NavMeshHit hit;
        return NavMesh.SamplePosition(candidate, out hit, distance * 0.6f, NavMesh.AllAreas)
               ? hit.position
               : candidate;
    }

    #endregion

    #region Comportamiento Dinámico – Latencia Cero

    private IEnumerator ExecuteLatenciaCero()
    {
        ReportDebug("LATENCIA CERO: Ejecutando.", 1);
        currentState = BossState.Teleporting;
        latenciaOnCooldown = true;

        StopAgent();

        if (animController != null)
        {
            animController.PlayTP_pre();
            yield return new WaitForSeconds(animController.TPanitipacionTime);
        }

        Vector3 blindFlank = GetPlayerBlindFlank();
        PerformTeleport(blindFlank);

        // Anticipación visible: indicador de suelo + Fuga de Memoria formándose.
        if (latenciaGroundIndicatorPrefab != null)
        {
            GameObject indicator = Instantiate(latenciaGroundIndicatorPrefab, transform.position, Quaternion.identity);
            Destroy(indicator, latenciaAnticipationDuration);
        }

        SpawnMemoryLeak(transform.position);

        if (animController != null) animController.IsWalking = false;
        yield return new WaitForSeconds(latenciaAnticipationDuration);

        DealAreaDamage(transform.position, bufferOverrunAttackRange * 0.5f, latenciaDamage);

        // Encadenar Buffer Overrun
        yield return StartCoroutine(ExecuteBufferOverrun(calledByLatencia: true));

        currentState = BossState.Idle;
        StartCoroutine(LatenciaCooldownRoutine());
    }

    private IEnumerator LatenciaCooldownRoutine()
    {
        yield return new WaitForSeconds(latenciaCooldown);
        latenciaOnCooldown = false;
        timeOutOfRange = 0f;
        ReportDebug("LATENCIA CERO: Cooldown finalizado.", 1);
    }

    private Vector3 GetPlayerBlindFlank()
    {
        if (player == null) return transform.position;

        float side = (Random.value > 0.5f) ? 1f : -1f;
        Vector3 candidate = player.position
                          - player.forward * 1.5f
                          + player.right * side;
        candidate.y = transform.position.y;

        NavMeshHit hit;
        return NavMesh.SamplePosition(candidate, out hit, 3f, NavMesh.AllAreas)
               ? hit.position
               : candidate;
    }

    #endregion

    #region Hito 50 % – Arquitectura Distribuida

    private IEnumerator ExecuteArquitecturaDistribuida()
    {
        ReportDebug("ARQUITECTURA DISTRIBUIDA: Hito 50 % alcanzado.", 1);
        currentState = BossState.Phase50;
        architecturePhaseActive = true;

        SetBossVisible(false);
        enemyHealth?.SetInvulnerable(true);
        currentShieldReduction = 1f;
        enemyHealth?.SetDynamicVulnerability(currentShieldReduction);

        if (phaseTransitionVFX != null)
        {
            phaseTransitionVFX.Play();
            VFXHelper.StopAndDestroy(phaseTransitionVFX, phaseTransitionVFX.main.duration);
        }

        // Spawn de hasta 3 Nodos Purulentos
        spawnedNodes.Clear();
        activeNodeCount = 0;
        _roomCenter = ComputeRoomCenter();

        for (int i = 0; i < maxNodes; i++)
        {
            if (nodePurulentosPrefab == null)
            {
                ReportDebug("ARQUITECTURA DISTRIBUIDA: Falta nodePurulentosPrefab.", 2);
                break;
            }

            Vector3 nodePos = GetNodeSpawnPosition(i);
            GameObject node = Instantiate(nodePurulentosPrefab, nodePos, Quaternion.identity);
            instantiatedEffects.Add(node);
            spawnedNodes.Add(node);
            activeNodeCount++;

            EnemyHealth nodeHealth = node.GetComponent<EnemyHealth>();
            if (nodeHealth != null) nodeHealth.OnDeath += OnNodeDestroyed;
        }

        ReportDebug($"ARQUITECTURA DISTRIBUIDA: {activeNodeCount} nodos instanciados.", 1);

        while (activeNodeCount > 0 && currentHealth > 0) yield return null;

        SetBossVisible(true);
        enemyHealth?.SetInvulnerable(false);
        currentShieldReduction = 0f;
        enemyHealth?.SetDynamicVulnerability(0f);
        architecturePhaseActive = false;

        ReportDebug("ARQUITECTURA DISTRIBUIDA: Todos los nodos destruidos. Jefe reaparece.", 1);
        currentState = BossState.Idle;
    }

    private void OnNodeDestroyed(GameObject node)
    {
        activeNodeCount = Mathf.Max(0, activeNodeCount - 1);
        spawnedNodes.Remove(node);

        currentShieldReduction = Mathf.Max(0f, currentShieldReduction - shieldReductionPerNode);
        enemyHealth?.SetDynamicVulnerability(currentShieldReduction);

        SpawnLarva(node.transform.position);

        ReportDebug($"ARQUITECTURA DISTRIBUIDA: Nodo destruido. " +
            $"Escudo: {currentShieldReduction * 100f:F0}%  Nodos: {activeNodeCount}", 1);

        EnemyHealth nodeHealth = node.GetComponent<EnemyHealth>();
        if (nodeHealth != null) nodeHealth.OnDeath -= OnNodeDestroyed;
    }

    private Vector3 GetNodeSpawnPosition(int index)
    {
        // Calcula las esquinas de la sala usando el NavMesh para encontrar los
        // puntos más alejados en cada dirección cardinal desde el centro de la sala.
        // Si la sala no tiene un centro definido, usa la posición del jefe.
        Vector3 roomCenter = _roomCenter != Vector3.zero ? _roomCenter : transform.position;

        // 4 direcciones de esquina; si hay menos de 4 nodos se usan las primeras N.
        Vector3[] cornerDirs = {
            new Vector3( 1f, 0f,  1f).normalized,
            new Vector3(-1f, 0f,  1f).normalized,
            new Vector3(-1f, 0f, -1f).normalized,
            new Vector3( 1f, 0f, -1f).normalized,
        };

        int dirIndex = index % cornerDirs.Length;
        Vector3 dir = cornerDirs[dirIndex];
        Vector3 candidate = roomCenter + dir * nodeSpawnRadius;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(candidate, out hit, nodeSpawnRadius * 1.5f, NavMesh.AllAreas))
            return hit.position;

        // Fallback: punto en el NavMesh más cercano al candidato
        return NavMesh.SamplePosition(transform.position + dir * nodeSpawnRadius * 0.5f,
                                       out hit, nodeSpawnRadius, NavMesh.AllAreas)
               ? hit.position
               : candidate;
    }

    /// <summary>
    /// Calcula el centro de la sala usando los límites del NavMesh alcanzable
    /// desde la posición del jefe. Se llama una sola vez al iniciar la fase.
    /// </summary>
    private Vector3 ComputeRoomCenter()
    {
        // Muestrea puntos en 8 direcciones y promedia para estimar el centro.
        Vector3 sum = transform.position;
        int count = 1;
        float probeDistance = nodeSpawnRadius * 3f;

        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 probe = transform.position + dir * probeDistance;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(probe, out hit, probeDistance * 0.5f, NavMesh.AllAreas))
            {
                sum += hit.position;
                count++;
            }
        }

        return sum / count;
    }

    #endregion

    #region Utility – Combate

    private const string ANIM_EVENT_ANTICIPATION_PAUSE = "AnimEvent_AnticipationPause";

    public void HandleAnimEvents(string eventName)
    {
        if (eventName == ANIM_EVENT_ANTICIPATION_PAUSE)
        {
            StartAnticipationPause();
        }
    }

    private void StartAnticipationPause()
    {
        if (_anticipationCoroutine != null) StopCoroutine(_anticipationCoroutine);
        _anticipationCoroutine = StartCoroutine(AnticipationRoutine());
    }

    private IEnumerator AnticipationRoutine()
    {
        _isInAnticipation = true;

        float duration;
        AudioClip sfx;

        if (currentState == BossState.Attacking)
        {
            // Distingue Buffer Overrun (tiene animación de carga) de Excepción Fatal
            duration = bufferOverrunAnticipationDuration;
            sfx = bufferOverrunAnticipationSFX;
        }
        else if (currentState == BossState.Teleporting)
        {
            duration = latenciaAnticipationDuration;
            sfx = latenciaAnticipationSFX;
        }
        else
        {
            duration = exceptionFatalAnticipationDuration;
            sfx = exceptionFatalAnticipationSFX;
        }

        if (animController != null) animController.PauseAnimation();
        if (audioSource != null && sfx != null) audioSource.PlayOneShot(sfx);
        if (enemyVisualEffects != null) enemyVisualEffects.PlayAnticipationBlink(duration);

        yield return new WaitForSeconds(duration);

        if (animController != null) animController.ResumeAnimation();
        _isInAnticipation = false;
        _anticipationCoroutine = null;
    }

    private void CancelAnticipation()
    {
        if (_anticipationCoroutine != null) StopCoroutine(_anticipationCoroutine);
        _anticipationCoroutine = null;
        if (animController != null) animController.ResumeAnimation();
        if (enemyVisualEffects != null) enemyVisualEffects.CancelAnticipationBlink();
        _isInAnticipation = false;
    }

    private void DealAreaDamage(Vector3 center, float radius, float damage)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                ExecuteAttack(hit.gameObject, damage);
                break;
            }
        }
    }

    private void ExecuteAttack(GameObject target, float damage)
    {
        if (target.TryGetComponent(out PlayerBlockSystem blockSystem) &&
            target.TryGetComponent(out PlayerHealth health))
        {
            if (blockSystem.IsBlocking && blockSystem.CanBlockAttack(transform.position))
            {
                float remaining = blockSystem.ProcessBlockedAttack(damage);
                if (remaining > 0f) health.TakeDamage(remaining, false, AttackDamageType.Melee);
                ReportDebug("Ataque bloqueado por el jugador.", 1);
                return;
            }
            health.TakeDamage(damage, false, AttackDamageType.Melee);
        }
        else if (target.TryGetComponent(out PlayerHealth healthOnly))
        {
            healthOnly.TakeDamage(damage, false, AttackDamageType.Melee);
        }
    }

    #endregion

    #region Utility – Movimiento y Teleport

    /// <summary>
    /// Embestida que se detiene en el primer frame en que el jugador entra en
    /// el radio de impacto, igual que la Ola de Lodo del jefe 1.
    /// </summary>
    private IEnumerator DashToPositionOrHit(Vector3 target, float duration)
    {
        Vector3 start = transform.position;
        float elapsed = 0f;
        bool hitPlayer = false;

        FaceDirection((target - start).normalized);

        // Indicador de trayectoria en el suelo durante la carga
        if (bufferOverrunTrailIndicatorPrefab != null)
        {
            float dist = Vector3.Distance(start, target);
            Vector3 mid = (start + target) * 0.5f;
            mid.y = start.y;
            GameObject trail = Instantiate(bufferOverrunTrailIndicatorPrefab, mid,
                                           Quaternion.LookRotation((target - start).normalized));
            trail.transform.localScale = new Vector3(5f, 0.05f, dist);
            Destroy(trail, duration + 0.1f);
        }

        while (elapsed < duration && !hitPlayer)
        {
            Vector3 next = Vector3.Lerp(start, target, elapsed / duration);

            NavMeshHit navHit;
            if (NavMesh.SamplePosition(next, out navHit, 2f, NavMesh.AllAreas))
                agent.Warp(navHit.position);
            else
                transform.position = next;

            // Comprueba colisión con el jugador
            Collider[] hits = Physics.OverlapSphere(transform.position,
                                                     bufferOverrunAttackRange * 0.5f,
                                                     LayerMask.GetMask("Player"));
            if (hits.Length > 0)
            {
                hitPlayer = true;
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void PerformTeleport(Vector3 target)
    {
        if (teleportVFXPrefab != null)
        {
            GameObject vfxOut = Instantiate(teleportVFXPrefab, transform.position, Quaternion.identity);
            Destroy(vfxOut, 1.5f);
        }

        NavMeshHit hit;
        if (NavMesh.SamplePosition(target, out hit, 3f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }
        else transform.position = target;

        if (teleportVFXPrefab != null)
        {
            GameObject vfxIn = Instantiate(teleportVFXPrefab, transform.position, Quaternion.identity);
            Destroy(vfxIn, 1.5f);
        }

        if (audioSource != null && teleportSFX != null)
        {
            audioSource.PlayOneShot(teleportSFX);
        }

        if (animController != null) animController.PlayTP_Impact();

        ReportDebug($"TELEPORT: {transform.position}", 1);
    }

    #endregion

    #region Utility – Spawns

    private void SpawnMemoryLeak(Vector3 position)
    {
        if (memoryLeakPrefab == null)
        {
            ReportDebug("Falta memoryLeakPrefab.", 2);
            return;
        }

        GameObject leak = Instantiate(memoryLeakPrefab, position, Quaternion.identity);
        instantiatedEffects.Add(leak);

        MemoryLeak leakScript = leak.GetComponent<MemoryLeak>();
        if (leakScript != null)
        {
            leakScript.Initialize(memoryLeakDuration, memoryLeakDPS, memoryLeakRadius);
        }
        else Destroy(leak, memoryLeakDuration);
    }

    private void SpawnLarva(Vector3 position)
    {
        if (larvaPrefab == null) return;

        Vector2 randomDir = Random.insideUnitCircle.normalized * safeSpawnRadius;
        Vector3 candidatePos = position + new Vector3(randomDir.x, 0f, randomDir.y);

        Vector3 finalSpawnPos = candidatePos;

        if (NavMesh.SamplePosition(candidatePos, out NavMeshHit hit, safeSpawnRadius * 2, NavMesh.AllAreas))
        {
            finalSpawnPos = hit.position;
        }

        GameObject larvaGO = Instantiate(larvaPrefab, finalSpawnPos, Quaternion.identity);

        if (larvaGO.TryGetComponent(out Larva larvaScript))
        {
            larvaScript.Initialize(this.player);
            instantiatedEffects.Add(larvaGO);
        }
        else
        {
            ReportDebug("Larva instanciada sin script Larva.", 2);
            Destroy(larvaGO);
        }
    }

    #endregion

    #region Utility – Agente y Orientación

    private void StopAgent()
    {
        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        if (animController != null) animController.IsWalking = false;
    }

    private void FacePlayer()
    {
        if (player == null) return;
        FaceDirection((player.position - transform.position).normalized);
    }

    private void FaceDirection(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(dir);
    }

    private void SetBossVisible(bool visible)
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            r.enabled = visible;
        }

        foreach (Collider c in GetComponentsInChildren<Collider>())
        {
            if (c is not CharacterController) c.enabled = visible;
        }
    }

    private void CleanUpEffects()
    {
        for (int i = instantiatedEffects.Count - 1; i >= 0; i--)
        {
            if (instantiatedEffects[i] != null)
            {
                Destroy(instantiatedEffects[i]);
            }
        }

        instantiatedEffects.Clear();

        // Matar larvas huérfanas que no estén registradas en instantiatedEffects.
        foreach (Larva larva in FindObjectsByType<Larva>(FindObjectsSortMode.None))
        {
            if (larva != null && larva.gameObject != null)
            {
                larva.Die();
            }
        }
    }

    #endregion

    #region Debug – Gizmos y OnGUI

    private void OnDrawGizmos()
    {
        if (!showDebugGUI) return;

        Gizmos.color = new Color(1f, 0f, 0f, 0.18f);
        Gizmos.DrawWireSphere(transform.position, bufferOverrunAttackRange);

        Gizmos.color = new Color(0f, 1f, 1f, 0.18f);
        Gizmos.DrawWireSphere(transform.position, latenciaActivationRange);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.18f);
        Gizmos.DrawWireSphere(transform.position, defragActivationRange);

        Gizmos.color = new Color(0f, 1f, 0f, 0.18f);
        Gizmos.DrawWireSphere(transform.position, nodeSpawnRadius);

#if UNITY_EDITOR
        Color labelColor = currentState switch
        {
            BossState.Idle => Color.white,
            BossState.Attacking => Color.red,
            BossState.Teleporting => Color.cyan,
            BossState.Phase50 => Color.yellow,
            BossState.Dead => Color.grey,
            _ => Color.white
        };

        GUIStyle style = new GUIStyle
        {
            normal = { textColor = labelColor },
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        string label = $"[BaalBoss]\n{currentState}  HP: {(int)currentHealth}";
        if (architecturePhaseActive) label += $"\nNODOS: {activeNodeCount}";
        if (defragInterruptPending) label += "\nDEFRAG PENDING";
        if (latenciaInterruptPending) label += "\nLATENCIA PENDING";

        UnityEditor.Handles.Label(transform.position + Vector3.up * 3.2f, label, style);
#endif
    }

    private void OnGUI()
    {
        if (!showDebugGUI) return;
        GUILayout.BeginArea(new Rect(10, 10, 260, 165));
        GUILayout.Label($"[BaalBoss] State      : {currentState}");
        GUILayout.Label($"HP                    : {currentHealth:F0} / {maxHealth:F0}");
        GUILayout.Label($"Phase50               : {phase50Triggered}");
        GUILayout.Label($"TimeOutOfRange        : {timeOutOfRange:F1} s");
        GUILayout.Label($"ActiveNodes / Shield  : {activeNodeCount} / {currentShieldReduction * 100f:F0}%");
        GUILayout.Label($"DefragHits            : {defragHitCount} / {defragHitThreshold}");
        GUILayout.EndArea();
    }

    #endregion

    #region Logging

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void ReportDebug(string message, int priority)
    {
        switch (priority)
        {
            case 1: Debug.Log($"[BaalBoss] {message}"); break;
            case 2: Debug.LogWarning($"[BaalBoss] {message}"); break;
            case 3: Debug.LogError($"[BaalBoss] {message}"); break;
            default: Debug.Log($"[BaalBoss] {message}"); break;
        }
    }

    #endregion
}