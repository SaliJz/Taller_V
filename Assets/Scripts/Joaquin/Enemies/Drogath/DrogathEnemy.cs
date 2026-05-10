using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador del enemigo Drogath, un tanque de apoyo que vincula aliados
/// otorgandoles regeneracion de superarmor mientras esten en rango.
/// Al morir libera una onda que otorga superarmor temporal.
/// </summary>
public class DrogathEnemy : MonoBehaviour
{
    #region Enums & Structs

    private class BondInfo
    {
        public GameObject ally;
        public EnemyToughness toughness;
        public LineRenderer lineRenderer;
        public float currentRegen;
    }

    #endregion

    #region Inspector - References

    [Header("Core References")]
    [SerializeField] private Transform hitPoint;
    [SerializeField] private bool showVisualHit = false;
    [SerializeField] private GameObject visualHit;
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private EnemyToughness enemyToughness;
    [SerializeField] private NavMeshAgent navAgent;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Animator animator;

    [Header("Layers")]
    [SerializeField] private LayerMask allyLayers = ~0;
    [SerializeField] private LayerMask playerLayer;

    #endregion

    #region Inspector - Bond System

    [Header("Bond System")]
    [Tooltip("Radio maximo para vincular aliados")]
    [SerializeField] private float bondRadius = 15f;
    [Tooltip("Maximo de vinculos simultaneos")]
    [SerializeField] private int maxBonds = 2;
    [Tooltip("Regeneracion de superarmor por segundo para aliados vinculados")]
    [SerializeField] private float toughnessRegenPerSecond = 6f;
    [Tooltip("Maximo de superarmor que puede regenerar en un aliado")]
    [SerializeField] private float maxToughnessRegen = 30f;
    [Tooltip("Cooldown antes de poder revincular al mismo aliado")]
    [SerializeField] private float rebondCooldown = 4f;
    [Tooltip("Intervalo para chequear vinculos y regenerar superarmor")]
    [SerializeField] private float bondUpdateInterval = 0.25f;
    [Tooltip("Si esta activo, puede activar la dureza en aliados que la tengan desactivada")]
    [SerializeField] private bool canEnableToughnessOnAllies = false;

    #endregion

    #region Inspector - Shield System

    [Header("Shield System")]
    [Tooltip("Angulo frontal del escudo (en grados, desde el centro)")]
    [SerializeField] private float frontalBlockAngle = 75f;
    [SerializeField] private Transform shieldForwardOverride = null;

    #endregion

    #region Inspector - Movement

    [Header("Movement")]
    [Tooltip("Velocidad de movimiento hacia el jugador")]
    [SerializeField] private float moveSpeed = 3.5f;
    [Tooltip("Distancia de parada respecto al jugador")]
    [SerializeField] private float stoppingDistance = 2f;
    [Tooltip("Distancia para considerar que debe atacar")]
    [SerializeField] private float distanceToAttack = 2.5f;

    #endregion

    #region Inspector - Combat

    [Header("Combat")]
    [Tooltip("Dano del ataque cuerpo a cuerpo")]
    [SerializeField] private float meleeDamage = 10f;
    [Tooltip("Intervalo entre ataques")]
    [SerializeField] private float attackInterval = 1.5f;
    [Tooltip("Rango de ataque cuerpo a cuerpo")]
    [SerializeField] private float attackRange = 2.5f;
    [Tooltip("Empuje aplicado al jugador al ser golpeado")]
    [SerializeField] private float knockbackForce = 4f;
    [Tooltip("Tiempo de espera desde que inicia la animacion hasta el impacto")]
    [SerializeField] private float attackImpactDelay = 1.25f;
    [Tooltip("Tiempo de recuperacion tras un ataque")]
    [SerializeField] private float attackRecoveryTime = 0.75f;

    #endregion

    #region Inspector - Death Effect

    [Header("Death Effect")]
    [Tooltip("Radio de la onda demoniaca al morir")]
    [SerializeField] private float deathEffectRadius = 15f;
    [Tooltip("Cantidad de superarmor otorgado al morir")]
    [SerializeField] private float deathSuperArmor = 10f;
    [Tooltip("Duracion del superarmor otorgado al morir")]
    [SerializeField] private float deathSuperArmorDuration = 5f;

    #endregion

    #region Inspector - Visual Feedback

    [Header("Visual Feedback")]
    [SerializeField] private LineRenderer bondLineRendererPrefab;
    [SerializeField] private Color bondLineColor = Color.cyan;
    [SerializeField] private float bondLineWidth = 0.1f;

    #endregion

    #region Inspector - Sound FX

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;

    [Header("SFX Ambiente/Movimiento")]
    [Tooltip("Sonido aleatorio cuando esta quieto.")]
    [SerializeField] private AudioClip idleSFX;
    [Tooltip("Sonido de pasos al moverse.")]
    [SerializeField] private AudioClip runSFX;

    [Header("SFX Combate")]
    [SerializeField] private AudioClip attackSFX;
    [SerializeField] private AudioClip blockSFX;
    [SerializeField] private AudioClip deathSFX;

    [Header("SFX Habilidades (Vinculo)")]
    [Tooltip("Suena una vez al establecer un vinculo nuevo.")]
    [SerializeField] private AudioClip bondActivateSFX;
    [Tooltip("Suena ritmicamente mientras regenera armadura a aliados.")]
    [SerializeField] private AudioClip bondRegenSFX;
    [SerializeField] private AudioClip bondBreakSFX;

    #endregion

    #region Inspector - Debugging

    [Header("Debugging")]
    [SerializeField] private bool canDebug = false;

    [Header("QuickSheet Balance")]
    [SerializeField] private Enemies enemiesSheet;
    [SerializeField] private int ENEMY_ID = 19;

    #endregion

    #region Internal State

    private List<BondInfo> activeBonds = new List<BondInfo>();
    private Dictionary<GameObject, float> rebondCooldowns = new Dictionary<GameObject, float>();
    private Coroutine bondUpdateRoutine;
    private Coroutine combatRoutine;
    private bool hasHitPlayer = false;
    private bool isAttacking = false;
    private bool isDead = false;
    private bool isPerformingAttackAnim = false;
    private float attackTimer = 0f;
    private float idleTimer;
    private float idleInterval;
    private float stepTimer;
    private float stepRate = 0.5f;
    private float regenSoundCooldown;

    private static readonly int animIdWalk = Animator.StringToHash("Walk");
    private static readonly int animIdAttack = Animator.StringToHash("Attack");

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (enemyHealth == null) enemyHealth = GetComponent<EnemyHealth>();
        if (enemyToughness == null) enemyToughness = GetComponent<EnemyToughness>();
        if (navAgent == null) navAgent = GetComponent<NavMeshAgent>();
        if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        LoadStatsFromSheet();

        if (enemyHealth == null)
        {
            ReportDebug("EnemyHealth no encontrado. Drogath requiere este componente.", 3);
        }

        if (navAgent == null)
        {
            ReportDebug("NavMeshAgent no encontrado. Drogath requiere este componente.", 3);
        }

        navAgent.stoppingDistance = stoppingDistance;
        navAgent.acceleration = 8f;
        navAgent.angularSpeed = 120f;
    }

    private void Start()
    {
        // Buscar jugador si no esta asignado
        if (playerTransform == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        if (playerTransform == null)
        {
            ReportDebug("Jugador no encontrado en la escena.", 2);
        }

        // Verificar que el agente este en NavMesh antes de iniciar
        if (IsNavAgentValid())
        {
            bondUpdateRoutine = StartCoroutine(BondUpdateRoutine());
            combatRoutine = StartCoroutine(CombatRoutine());
            ReportDebug($"Drogath inicializado. Vida: {enemyHealth.MaxHealth}, Radio: {bondRadius}m", 1);
        }
        else
        {
            ReportDebug("NavMeshAgent no esta en NavMesh valido. Esperando posicionamiento...", 2);
            StartCoroutine(WaitForNavMeshPlacement());
        }

        ResetIdleTimer();
    }

    private void OnEnable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath += HandleDeath;
            enemyHealth.OnDamaged += OnDamageTaken;
        }
    }

    private void OnDisable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleDeath;
            enemyHealth.OnDamaged -= OnDamageTaken;
        }
    }

    private void OnDestroy()
    {
        // Desuscribirse del evento
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleDeath;
            enemyHealth.OnDamaged -= OnDamageTaken;
        }

        // Limpiar vinculos
        ClearAllBonds();
    }

    private void Update()
    {
        if (isDead || !enabled) return;

        HandleAudioLogic();
        UpdateAnimationState();
    }

    #endregion

    #region Initialization & Data Sync

    private void LoadStatsFromSheet()
    {
        if (enemiesSheet == null) return;

        foreach (var row in enemiesSheet.dataArray)
        {
            if (row.ID != ENEMY_ID) continue;

            if (enemyHealth != null)
            {
                enemyHealth.SetMaxHealth(row.Health);
            }

            if (enemyToughness != null)
            {
                if (row.Superarmor > 0)
                {
                    enemyToughness.SetUseToughness(true);
                    enemyToughness.SetMaxToughness(row.Superarmor);
                }
                else enemyToughness.SetUseToughness(false);
            }

            moveSpeed = row.Movespeed;

            if (navAgent != null)
            {
                navAgent.speed = moveSpeed;
            }

            toughnessRegenPerSecond = row.Superarmorregen;

            Debug.Log($"[Drogath] ID {ENEMY_ID} cargado. Regen: {toughnessRegenPerSecond}");
            return;
        }
    }

    private IEnumerator WaitForNavMeshPlacement()
    {
        int maxAttempts = 60; // 3 segundos maximo (60 frames a 20fps)
        int attempts = 0;

        while (attempts < maxAttempts && !IsNavAgentValid())
        {
            attempts++;
            yield return new WaitForSeconds(0.05f);
        }

        if (IsNavAgentValid())
        {
            bondUpdateRoutine = StartCoroutine(BondUpdateRoutine());
            combatRoutine = StartCoroutine(CombatRoutine());
            ReportDebug("Drogath colocado en NavMesh correctamente", 1);
        }
        else
        {
            ReportDebug("ERROR: Drogath no pudo ser colocado en NavMesh despues de varios intentos", 3);
        }
    }

    #endregion

    #region Movement & Animation

    /// <summary>
    /// Controla el parametro de movimiento en el Animator
    /// </summary>
    private void UpdateAnimationState()
    {
        if (animator == null || navAgent == null) return;

        // Comprobamos si se mueve calculando la velocidad del NavMeshAgent
        bool isMoving = navAgent.velocity.sqrMagnitude > 0.1f && !navAgent.isStopped;

        animator.SetBool(animIdWalk, isMoving);
    }

    private void HandleAudioLogic()
    {
        if (navAgent != null && navAgent.enabled && !navAgent.isStopped && navAgent.velocity.sqrMagnitude > 0.2f)
        {
            stepTimer += Time.deltaTime;
            if (stepTimer >= stepRate)
            {
                if (audioSource != null && runSFX != null)
                {
                    audioSource.pitch = Random.Range(0.9f, 1.05f);
                    audioSource.PlayOneShot(runSFX, 0.35f);
                    audioSource.pitch = 1f;
                }
                stepTimer = 0f;
            }

            ResetIdleTimer();
        }
        else
        {
            stepTimer = stepRate;

            idleTimer += Time.deltaTime;
            if (idleTimer >= idleInterval)
            {
                if (audioSource != null && idleSFX != null)
                {
                    audioSource.PlayOneShot(idleSFX);
                }
                ResetIdleTimer();
            }
        }

        if (regenSoundCooldown > 0) regenSoundCooldown -= Time.deltaTime;
    }

    private void ResetIdleTimer()
    {
        idleTimer = 0f;
        idleInterval = Random.Range(4f, 8f);
    }

    #endregion

    #region NavMesh Validation

    /// <summary>
    /// Verifica si el NavMeshAgent esta en condiciones validas para ser usado
    /// </summary>
    private bool IsNavAgentValid()
    {
        return navAgent != null
            && navAgent.isActiveAndEnabled
            && navAgent.isOnNavMesh;
    }

    /// <summary>
    /// Intenta detener el NavMeshAgent de forma segura
    /// </summary>
    private void SafeStopAgent()
    {
        if (!IsNavAgentValid()) return;

        try
        {
            if (!navAgent.isStopped)
            {
                navAgent.isStopped = true;
            }
        }
        catch (System.Exception e)
        {
            ReportDebug($"Error al detener NavMeshAgent: {e.Message}", 2);
        }
    }

    /// <summary>
    /// Intenta reanudar el NavMeshAgent de forma segura
    /// </summary>
    private void SafeResumeAgent()
    {
        if (!IsNavAgentValid()) return;

        try
        {
            if (navAgent.isStopped)
            {
                navAgent.isStopped = false;
            }
        }
        catch (System.Exception e)
        {
            ReportDebug($"Error al reanudar NavMeshAgent: {e.Message}", 2);
        }
    }

    /// <summary>
    /// Intenta resetear el path del NavMeshAgent de forma segura
    /// </summary>
    private void SafeResetPath()
    {
        if (!IsNavAgentValid()) return;

        try
        {
            navAgent.ResetPath();
        }
        catch (System.Exception e)
        {
            ReportDebug($"Error al resetear path: {e.Message}", 2);
        }
    }

    /// <summary>
    /// Intenta establecer un destino de forma segura
    /// </summary>
    private void SafeSetDestination(Vector3 destination)
    {
        if (!IsNavAgentValid()) return;

        try
        {
            navAgent.SetDestination(destination);
        }
        catch (System.Exception e)
        {
            ReportDebug($"Error al establecer destino: {e.Message}", 2);
        }
    }

    #endregion

    #region Bond System

    private IEnumerator BondUpdateRoutine()
    {
        while (!isDead)
        {
            UpdateRebondCooldowns();
            UpdateExistingBonds();
            TryCreateNewBonds();
            RegenerateBondedAlliesToughness();

            yield return new WaitForSeconds(bondUpdateInterval);
        }
    }

    private void UpdateRebondCooldowns()
    {
        List<GameObject> toRemove = new List<GameObject>();
        List<GameObject> keys = new List<GameObject>(rebondCooldowns.Keys);

        foreach (var key in keys)
        {
            if (key == null)
            {
                toRemove.Add(key);
                continue;
            }

            rebondCooldowns[key] -= bondUpdateInterval;
            if (rebondCooldowns[key] <= 0)
            {
                toRemove.Add(key);
            }
        }

        foreach (var key in toRemove)
        {
            rebondCooldowns.Remove(key);
            if (key != null)
            {
                ReportDebug($"Cooldown de revinculo completado para {key.name}", 1);
            }
        }
    }

    private void UpdateExistingBonds()
    {
        for (int i = activeBonds.Count - 1; i >= 0; i--)
        {
            var bond = activeBonds[i];

            // Verificar si el aliado sigue existiendo
            if (bond.ally == null)
            {
                RemoveBond(i, "Aliado destruido");
                continue;
            }

            // Verificar si el aliado murio
            var allyHealth = bond.ally.GetComponent<EnemyHealth>();
            if (allyHealth != null && allyHealth.IsDead)
            {
                RemoveBond(i, "Aliado murio");
                continue;
            }

            // Verificar distancia
            float distance = Vector3.Distance(transform.position, bond.ally.transform.position);
            if (distance > bondRadius)
            {
                RemoveBond(i, "Fuera de rango");
                continue;
            }

            // Actualizar LineRenderer
            if (bond.lineRenderer != null)
            {
                bond.lineRenderer.SetPosition(0, transform.position + Vector3.up);
                bond.lineRenderer.SetPosition(1, bond.ally.transform.position + Vector3.up);
            }
        }
    }

    private void TryCreateNewBonds()
    {
        if (activeBonds.Count >= maxBonds) return;

        // Buscar aliados en rango
        Collider[] hits = Physics.OverlapSphere(transform.position, bondRadius, allyLayers, QueryTriggerInteraction.Ignore);

        foreach (var hit in hits)
        {
            if (activeBonds.Count >= maxBonds) break;

            GameObject rootObj = hit.transform.root.gameObject;

            // No vincularse a si mismo
            if (rootObj == gameObject) continue;

            // No vincularse con otros Drogath
            if (rootObj.GetComponent<DrogathEnemy>() != null) continue;

            // No vincular si esta en cooldown
            if (rebondCooldowns.ContainsKey(rootObj)) continue;

            // No vincular si ya esta vinculado
            if (activeBonds.Exists(b => b.ally == rootObj)) continue;

            // Verificar que el aliado no este muerto
            var allyHealth = rootObj.GetComponent<EnemyHealth>();
            if (allyHealth != null && allyHealth.IsDead) continue;

            // Buscar componente EnemyToughness
            EnemyToughness toughness = rootObj.GetComponent<EnemyToughness>();
            if (toughness == null) continue;

            // Si no puede activar dureza en otros, verificar que ya este activa
            if (!canEnableToughnessOnAllies && !toughness.HasToughness)
            {
                continue;
            }

            // Si puede activar dureza, hacerlo
            if (canEnableToughnessOnAllies && !toughness.HasToughness)
            {
                toughness.SetUseToughness(true);
                ReportDebug($"Dureza activada en {rootObj.name} por vinculo de Drogath", 1);
            }

            // Crear vinculo
            CreateBond(rootObj, toughness);
        }
    }

    private void CreateBond(GameObject ally, EnemyToughness toughness)
    {
        BondInfo bond = new BondInfo
        {
            ally = ally,
            toughness = toughness,
            currentRegen = 0f
        };

        // Crear LineRenderer visual
        if (bondLineRendererPrefab != null)
        {
            LineRenderer lr = Instantiate(bondLineRendererPrefab, transform);
            lr.positionCount = 2;
            lr.startWidth = bondLineWidth;
            lr.endWidth = bondLineWidth;
            lr.startColor = bondLineColor;
            lr.endColor = bondLineColor;
            lr.SetPosition(0, transform.position + Vector3.up);
            lr.SetPosition(1, ally.transform.position + Vector3.up);
            bond.lineRenderer = lr;
        }

        activeBonds.Add(bond);

        if (audioSource != null && bondActivateSFX != null)
        {
            audioSource.PlayOneShot(bondActivateSFX, 0.75f);
        }

        ReportDebug($"Vinculo creado con {ally.name}. Vinculos activos: {activeBonds.Count}/{maxBonds}", 1);
    }

    private void RemoveBond(int index, string reason)
    {
        if (index < 0 || index >= activeBonds.Count) return;

        var bond = activeBonds[index];

        // Anadir cooldown
        if (bond.ally != null)
        {
            rebondCooldowns[bond.ally] = rebondCooldown;
            ReportDebug($"Vinculo roto con {bond.ally.name} ({reason}). Cooldown: {rebondCooldown}s", 1);
        }

        // Destruir LineRenderer
        if (bond.lineRenderer != null)
        {
            Destroy(bond.lineRenderer.gameObject);
        }

        activeBonds.RemoveAt(index);

        if (audioSource != null && bondBreakSFX != null)
        {
            audioSource.PlayOneShot(bondBreakSFX, 0.75f);
        }
    }

    private void ClearAllBonds()
    {
        for (int i = activeBonds.Count - 1; i >= 0; i--)
        {
            var bond = activeBonds[i];
            if (bond.lineRenderer != null)
            {
                Destroy(bond.lineRenderer.gameObject);
            }
        }
        activeBonds.Clear();
    }

    private void RegenerateBondedAlliesToughness()
    {
        // Calcular regeneracion
        float regenThisFrame = toughnessRegenPerSecond * bondUpdateInterval;
        bool anyRegenerationHappened = false;

        foreach (var bond in activeBonds)
        {
            if (bond.toughness == null || bond.ally == null) continue;

            // Verificar limite de regeneracion total
            if (bond.currentRegen + regenThisFrame > maxToughnessRegen)
            {
                regenThisFrame = maxToughnessRegen - bond.currentRegen;
            }

            if (regenThisFrame > 0)
            {
                // Calcular cuanto puede regenerar realmente
                float currentToughness = bond.toughness.CurrentToughness;
                float maxToughness = bond.toughness.MaxToughness;
                float possibleRegen = Mathf.Min(regenThisFrame, maxToughness - currentToughness);

                if (possibleRegen > 0)
                {
                    float addedToughness = bond.toughness.AddCurrentToughness(regenThisFrame);
                    if (addedToughness > 0)
                    {
                        bond.currentRegen += addedToughness;
                        anyRegenerationHappened = true;
                    }

                    if (Mathf.FloorToInt(bond.currentRegen) % 6 == 0 && bond.currentRegen > 0)
                    {
                        ReportDebug($"{bond.ally.name} regenero {bond.currentRegen:F1}/{maxToughnessRegen} de superarmor", 1);
                    }
                }
            }
        }

        if (anyRegenerationHappened && regenSoundCooldown <= 0)
        {
            if (audioSource != null && bondRegenSFX != null)
            {
                audioSource.PlayOneShot(bondRegenSFX, 0.75f);
                regenSoundCooldown = 1.0f; // Suena maximo 1 vez por segundo
            }
        }
    }

    #endregion

    #region Combat System

    private IEnumerator CombatRoutine()
    {
        while (!isDead)
        {
            bool shouldAttack = activeBonds.Count == 0;

            if (shouldAttack)
            {
                if (!isAttacking)
                {
                    StartAttackMode();
                }

                UpdateCombat();
            }
            else
            {
                if (isAttacking)
                {
                    StopAttackMode();
                }
            }

            yield return null;
        }
    }

    private void StartAttackMode()
    {
        isAttacking = true;
        attackTimer = 0f;

        SafeResumeAgent();

        ReportDebug("Modo ofensivo activado (sin aliados vinculados)", 1);
    }

    private void StopAttackMode()
    {
        isAttacking = false;
        attackTimer = 0f;

        SafeStopAgent();
        SafeResetPath();

        ReportDebug("Modo ofensivo desactivado (vinculos activos)", 1);
    }

    private void UpdateCombat()
    {
        if (playerTransform == null || navAgent == null) return;

        if (isPerformingAttackAnim)
        {
            SafeStopAgent(); // Frenar para dar el golpe
            return;
        }

        // Verificar si esta aturdido
        if (enemyHealth != null && enemyHealth.IsStunned)
        {
            SafeStopAgent();
            return;
        }
        else
        {
            // Solo reanudar si esta en modo ataque
            if (isAttacking)
            {
                SafeResumeAgent();
            }
        }

        // Mover hacia el jugador solo si el agente esta valido y no detenido
        if (IsNavAgentValid() && !navAgent.isStopped)
        {
            SafeSetDestination(playerTransform.position);
        }

        // Verificar si esta en rango de ataque
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer <= distanceToAttack)
        {
            Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
            directionToPlayer.y = 0;
            if (directionToPlayer != Vector3.zero)
            {
                transform.forward = Vector3.Slerp(transform.forward, directionToPlayer, Time.deltaTime * 5f);
            }

            attackTimer += Time.deltaTime;

            if (attackTimer >= attackInterval)
            {
                hasHitPlayer = false;
                StartCoroutine(ExecuteAttackSequence());
                attackTimer = 0f;
            }
        }
    }

    private IEnumerator ExecuteAttackSequence()
    {
        isPerformingAttackAnim = true;
        hasHitPlayer = false;

        if (animator != null) animator.SetTrigger(animIdAttack);

        // if (audioSource != null) audioSource.PlayOneShot(attackWindupSFX); 

        yield return new WaitForSeconds(attackImpactDelay);

        if (isDead || (enemyHealth != null && enemyHealth.IsStunned))
        {
            isPerformingAttackAnim = false;
            yield break;
        }

        if (audioSource != null && attackSFX != null)
        {
            audioSource.PlayOneShot(attackSFX, 0.75f);
        }

        CheckMeleeHitbox();

        yield return new WaitForSeconds(attackRecoveryTime);

        isPerformingAttackAnim = false;
    }

    private void CheckMeleeHitbox()
    {
        if (playerTransform == null) return;

        Vector3 impactPos = hitPoint != null ? hitPoint.position : transform.position + transform.forward;

        Collider[] hitPlayer = Physics.OverlapSphere(impactPos, attackRange, playerLayer);

        foreach (var hit in hitPlayer)
        {
            if (hasHitPlayer) break;

            var hitTransform = hit.transform;

            // Ejecutar ataque
            ExecuteAttack(hit.gameObject, meleeDamage);

            // Aplicar empuje
            ApplyKnockback(hitTransform);

            ReportDebug($"Drogath golpeo al jugador por {meleeDamage} de dano tras {attackImpactDelay}s de delay", 1);

            hasHitPlayer = true;
        }

        StartCoroutine(ShowGizmoCoroutine());
    }

    private void ExecuteAttack(GameObject target, float damageAmount)
    {
        if (target.TryGetComponent<PlayerBlockSystem>(out var blockSystem) && target.TryGetComponent<PlayerHealth>(out var health))
        {
            if (blockSystem.IsBlocking && blockSystem.CanBlockAttack(transform.position))
            {
                float remainingDamage = blockSystem.ProcessBlockedAttack(damageAmount);

                if (remainingDamage > 0f)
                {
                    health.TakeDamage(remainingDamage, false, AttackDamageType.Melee);
                }

                return;
            }

            health.TakeDamage(damageAmount, false, AttackDamageType.Melee);
        }
    }

    private void ApplyKnockback(Transform target)
    {
        // Calcula la direccion del empuje desde Kronus hacia el jugador
        Vector3 knockbackDirection = (target.position - transform.position).normalized;
        knockbackDirection.y = 0f;

        CharacterController cc = target.GetComponent<CharacterController>();
        Rigidbody rb = target.GetComponent<Rigidbody>();

        if (cc != null)
        {
            StartCoroutine(ApplyKnockbackOverTime(cc, knockbackDirection * knockbackForce));
        }
        else if (rb != null)
        {
            rb.AddForce(knockbackDirection * knockbackForce * 10f, ForceMode.Impulse);
        }

        ReportDebug($"Empuje aplicado al jugador en direccion {knockbackDirection}", 1);
    }

    private IEnumerator ApplyKnockbackOverTime(CharacterController cc, Vector3 knockbackVelocity)
    {
        float duration = 0.2f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (cc != null && cc.enabled)
            {
                cc.Move(knockbackVelocity * Time.deltaTime);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator ShowGizmoCoroutine()
    {
        if (visualHit == null || hitPoint == null) yield break;
        if (!showVisualHit) yield break;

        Vector3 originalScale = visualHit.transform.localScale;

        if (visualHit != null && hitPoint != null)
        {
            visualHit.transform.localScale = Vector3.one * attackRange * 2f;
            visualHit.SetActive(true);
        }

        yield return new WaitForSeconds(0.5f);

        if (visualHit != null && hitPoint != null)
        {
            visualHit.SetActive(false);
            visualHit.transform.localScale = originalScale;
        }
    }

    #endregion

    #region Shield System

    /// <summary>
    /// Verifica si un ataque desde una direccion dada debe ser bloqueado por el escudo.
    /// </summary>
    public bool ShouldBlockDamage(Vector3 attackerPosition)
    {
        Vector3 toAttacker = attackerPosition - transform.position;
        toAttacker.y = 0;

        if (toAttacker.sqrMagnitude < 0.1f)
        {
            ReportDebug("Atacante demasiado cerca o en misma posicion", 2);
            return false;
        }

        toAttacker.Normalize();

        // Obtener direccion frontal del escudo en plano XZ
        Vector3 shieldForward = GetShieldForward();
        shieldForward.y = 0;
        shieldForward.Normalize();

        // Calcular angulo entre el forward del escudo y la direccion hacia el atacante
        float angle = Vector3.Angle(shieldForward, toAttacker);

        if (canDebug)
        {
            Debug.DrawRay(transform.position, shieldForward * 3f, Color.green, 0.5f);
            Debug.DrawRay(transform.position, toAttacker * 2.5f, Color.red, 0.5f);
            Debug.DrawLine(transform.position, attackerPosition, Color.yellow, 0.5f);
            DrawBlockCone(shieldForward, frontalBlockAngle * 0.5f);
        }

        // El escudo bloquea si el atacante esta en el arco frontal
        bool isBlocked = angle <= (frontalBlockAngle * 0.5f);

        if (isBlocked)
        {
            if (audioSource != null && blockSFX != null)
            {
                audioSource.PlayOneShot(blockSFX);
            }
        }

        ReportDebug($"Atacante a {angle:F1} grados del frente, Cobertura: +-{frontalBlockAngle * 0.5f} grados, {(isBlocked ? "BLOQUEADO" : "NO BLOQUEADO")}", 1);

        return isBlocked;
    }

    private Vector3 GetShieldForward()
    {
        if (shieldForwardOverride != null)
        {
            return shieldForwardOverride.forward;
        }
        return transform.forward;
    }

    #endregion

    #region Death Effect

    private void OnDamageTaken()
    {
    }

    private void HandleDeath(GameObject deadEnemy)
    {
        if (isDead) return;
        isDead = true;

        if (audioSource != null && deathSFX != null)
        {
            audioSource.PlayOneShot(deathSFX);
        }

        ReportDebug("Drogath murio. Activando Armadura Demoniaca...", 1);

        SafeStopAgent();
        SafeResetPath();

        if (bondUpdateRoutine != null)
        {
            StopCoroutine(bondUpdateRoutine);
            bondUpdateRoutine = null;
        }
        if (combatRoutine != null)
        {
            StopCoroutine(combatRoutine);
            combatRoutine = null;
        }

        StopAllCoroutines();

        ClearAllBonds();

        // Aplicar efecto de muerte
        ApplyDemonicArmorEffect();

        if (animator != null) animator.SetBool(animIdWalk, false);
    }

    private void ApplyDemonicArmorEffect()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, deathEffectRadius, allyLayers, QueryTriggerInteraction.Ignore);

        int affectedCount = 0;

        foreach (var hit in hits)
        {
            GameObject rootObj = hit.transform.root.gameObject;

            if (rootObj == gameObject) continue; // Verificar que el aliado no sea el mismo

            if (rootObj.GetComponent<DrogathEnemy>() != null) continue;

            var allyHealth = rootObj.GetComponent<EnemyHealth>();
            if (allyHealth != null && allyHealth.IsDead) continue;

            EnemyToughness allyToughness = rootObj.GetComponent<EnemyToughness>();
            if (allyToughness == null) continue;

            // Activar dureza si esta permitido
            if (canEnableToughnessOnAllies && !allyToughness.HasToughness)
            {
                allyToughness.SetUseToughness(true);
            }

            if (allyToughness.HasToughness)
            {
                allyToughness.ApplyToughnessBuff(deathSuperArmor, deathSuperArmorDuration);
            }

            affectedCount++;
        }

        ReportDebug($"Armadura Demoniaca aplicada a {affectedCount} aliados (+{deathSuperArmor} superarmor por {deathSuperArmorDuration}s)", 1);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Obtiene el numero de vinculos activos actuales
    /// </summary>
    public int GetActiveBondsCount()
    {
        return activeBonds.Count;
    }

    /// <summary>
    /// Verifica si Drogath esta en modo ataque
    /// </summary>
    public bool IsInAttackMode()
    {
        return isAttacking;
    }

    /// <summary>
    /// Fuerza la rotura de todos los vinculos activos
    /// </summary>
    public void ForceBreakAllBonds()
    {
        for (int i = activeBonds.Count - 1; i >= 0; i--)
        {
            RemoveBond(i, "Forzado manualmente");
        }

        ReportDebug("Todos los vinculos fueron rotos manualmente", 2);
    }

    #endregion

    #region Logging

    private void DrawBlockCone(Vector3 forward, float halfAngle)
    {
        int segments = 20;
        float totalAngle = frontalBlockAngle;
        float segmentAngle = totalAngle / segments;

        for (int i = 0; i <= segments; i++)
        {
            float currentAngle = -halfAngle + (i * segmentAngle);
            Vector3 dir = Quaternion.Euler(0, currentAngle, 0) * forward;
            Debug.DrawRay(transform.position, dir * 2f, Color.cyan, 0.5f);
        }
    }

    private void OnDrawGizmos()
    {
        if (!canDebug) return;

        // Radio de vinculacion
        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position + transform.up * 1.875f, bondRadius);

        // Radio de efecto de muerte
        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position + transform.up * 1.875f, deathEffectRadius);

        // Rango de parada
        Gizmos.color = new Color(0f, 1f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position + transform.up * 1.875f, stoppingDistance);

        // Rango de distancia para atacar
        Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position + transform.up * 1.875f, distanceToAttack);

        // Rango de ataque
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position + transform.up * 1.875f, attackRange);

        // Visualizar angulo del escudo
        Vector3 forward = transform.forward;
        Vector3 right = Quaternion.Euler(0, frontalBlockAngle / 2, 0) * forward;
        Vector3 left = Quaternion.Euler(0, -frontalBlockAngle / 2, 0) * forward;

        Gizmos.color = new Color(0f, 0.5f, 1f, 0.5f);
        Gizmos.DrawLine(transform.position, transform.position + forward * 3f);
        Gizmos.DrawLine(transform.position, transform.position + right * 3f);
        Gizmos.DrawLine(transform.position, transform.position + left * 3f);

        // Dibujar area del escudo
        int segments = 20;
        Vector3 prevPoint = transform.position + left * 3f;
        for (int i = 1; i <= segments; i++)
        {
            float angle = Mathf.Lerp(-frontalBlockAngle / 2, frontalBlockAngle / 2, i / (float)segments);
            Vector3 dir = Quaternion.Euler(0, angle, 0) * forward;
            Vector3 point = transform.position + dir * 3f;
            Gizmos.DrawLine(prevPoint, point);
            prevPoint = point;
        }

        // Dibujar vinculos activos
        if (Application.isPlaying)
        {
            Gizmos.color = bondLineColor;
            foreach (var bond in activeBonds)
            {
                if (bond.ally != null)
                {
                    Gizmos.DrawLine(transform.position + Vector3.up, bond.ally.transform.position + Vector3.up);

                    // Dibujar esfera pequena en el aliado vinculado
                    Gizmos.DrawSphere(bond.ally.transform.position + Vector3.up, 0.3f);
                }
            }
        }
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    /// <summary> 
    /// Funcion de depuracion para reportar mensajes en la consola de Unity. 
    /// </summary> 
    /// <param name="message">Mensaje a reportar.</param>
    /// <param name="reportPriorityLevel">Nivel de prioridad: Debug, Warning, Error.</param>
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[DrogathEnemy] {message}");
                break;
            case 2:
                Debug.LogWarning($"[DrogathEnemy] {message}");
                break;
            case 3:
                Debug.LogError($"[DrogathEnemy] {message}");
                break;
            default:
                Debug.Log($"[DrogathEnemy] {message}");
                break;
        }
    }

    #endregion
}