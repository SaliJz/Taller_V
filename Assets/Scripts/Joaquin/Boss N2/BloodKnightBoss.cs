using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;

public class BloodKnightBoss : MonoBehaviour
{
    #region Statistics and Configuration

    [Header("References")]
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private NavMeshAgent agent;

    [Header("Boss Configuration")]
    [SerializeField] private float maxHealth = 300f;
    [SerializeField] private float currentHealth;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float lowHPSpeedMultiplier = 1.3f;

    [Header("Ciclo 1: Apocalipsis")]
    [SerializeField] private float apocalipsisDuration = 10f;
    [SerializeField] private float apocalipsisDamage = 7f;
    [SerializeField] private int apocalipsisTargetDashes = 10;
    [SerializeField] private float apocalipsisRange = 4f;

    [Header("Ciclo 1: Tiempos de Animación")]
    [Tooltip("Duración del desplazamiento")]
    [SerializeField] private float apocalipsisDashDuration = 0.43f;
    [Tooltip("Tiempo desde que inicia el ataque hasta que golpea")]
    [SerializeField] private float apocalipsisImpactDelay = 0.2f;
    [Tooltip("Tiempo restante de la animación tras el impacto")]
    [SerializeField] private float apocalipsisRecoveryTime = 0.3f;

    [Header("Ciclo 2: Pausa Posicionamiento")]
    [SerializeField] private float positioningDuration = 2f;

    [Header("Ciclo 3: Sodoma y Gomorra")]
    [SerializeField] private float sodomaDamage = 35f;
    [SerializeField] private float sodomaCutRange = 8f;
    [SerializeField] private float fireTrailDamagePerSecond = 5f;
    [SerializeField] private float fireTrailLifeTime = 2.5f;
    [SerializeField] private float sodomaChargeTime = 1.5f;
    [SerializeField] private float sodomaBackwardDistance = 3f;

    [Header("Ciclo 3: Tiempos de Animación")]
    [Tooltip("Tiempo que tarda el jefe en recuperarse del Dash antes de cargar el ataque")]
    [SerializeField] private float sodomaDashDuration = 0.66f;
    [Tooltip("Momento exacto del impacto tras iniciar SwordStack")]
    [SerializeField] private float sodomaImpactDelay = 0.63f;
    [Tooltip("Tiempo de espera tras el impacto para finalizar la pose")]
    [SerializeField] private float attackEndDuration = 0.56f;

    [Header("Ciclo 4: Fase Cooldown / Necio Pecador")]
    [SerializeField] private float necioAttackWindow = 12f;
    [SerializeField] private float necioVulnerableWindow = 3f;
    [SerializeField] private float necioPecadorDamage = 15f;
    [SerializeField] private float necioPecadorRadius = 1.5f;
    [SerializeField] private float necioPecadorWarningTime = 1.5f;

    [Header("Special - Embestida del Fornido")]
    [Tooltip("Probabilidad de intentar la embestida antes de iniciar un ciclo nuevo")]
    [SerializeField, Range(0f, 1f)] private float chargeProbability = 0.15f;
    [SerializeField] private float chargeAbilitySpeed = 15f;
    //[SerializeField] private float chargeAbilityKnockbackForce = 15f;
    [SerializeField] private float playerStunDuration = 2.5f;
    [SerializeField] private LayerMask chargeObstacleLayers;

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform swordTransform;

    [Header("VFX Prefabs")]
    [SerializeField] private GameObject greenFireTrailPrefab;
    [SerializeField] private GameObject soulHandPrefab;
    [SerializeField] private GameObject necioPecadorWarningPrefab;
    [SerializeField] private ParticleSystem armorGlowVFX;
    [SerializeField] private ParticleSystem greenFireVFX;
    [SerializeField] private GameObject darkWindTrailPrefab;
    [SerializeField] private GameObject stunEffectPrefab;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [Space(5)]
    [SerializeField] private AudioClip damageReceivedSFX;

    [Space(5)]
    [SerializeField] private AudioClip sodomaChargeSFX;
    [SerializeField] private AudioClip sodomaAttackSFX;
    [SerializeField] private AudioClip apocalipsisSlashSFX;
    [SerializeField] private AudioClip necioPecadorSummonSFX;
    [SerializeField] private AudioClip necioPecadorExplosionSFX;
    [SerializeField] private AudioClip chargeAbilitySFX;

    [Space(5)]
    [SerializeField] private AudioClip stunSFX;
    [SerializeField] private AudioClip deathSFX;

    [Header("Audio - Ambient & Movement")]
    [SerializeField] private AudioSource ambientAudioSource;

    [Space(5)]
    [SerializeField] private AudioClip idleClip;
    [SerializeField] private float idleInterval = 3.0f;
    [SerializeField] private float idleIntervalVariance = 0.5f;

    [Space(5)]
    [SerializeField] private AudioClip movementClip;
    [SerializeField] private float movementInterval = 0.45f;

    [Header("Debug Options")]
    [SerializeField] private bool showDebugGUI = false;

    #endregion

    #region Internal Variables

    private enum BossState
    {
        Idle,
        Chasing,
        Attacking,
        Stunned,
        Charging,
        Vulnerable
    }

    private BossState currentState = BossState.Idle;

    private PlayerHealth playerHealth;
    private CharacterController playerController;

    private bool isInLowHealthPhase = false;
    private bool speedBuffApplied = false;
    private bool forceApocalipsisNext = false;

    private Coroutine bossAICoroutine;
    private List<GameObject> instantiatedEffects = new List<GameObject>();

    private MeshRenderer[] armorRenderers;
    private MaterialPropertyBlock armorPropertyBlock;
    private Color originalEmissionColor;

    private int groundLayerMask;

    #endregion

    #region Camera Shake

    [SerializeField] private CinemachineCamera vcam;
    private CinemachineBasicMultiChannelPerlin noise;

    #endregion

    #region Animation Hashes

    private static readonly int AnimID_Walking = Animator.StringToHash("Walking");
    private static readonly int AnimID_Death = Animator.StringToHash("Death");
    private static readonly int AnimID_AttackHand = Animator.StringToHash("AttackHand");

    private static readonly int AnimID_SoloDash = Animator.StringToHash("SoloDash");
    private static readonly int AnimID_AttackDash = Animator.StringToHash("AttackDash");

    private static readonly int AnimID_SwordStack = Animator.StringToHash("SwordStack");
    private static readonly int AnimID_AttackEnded = Animator.StringToHash("AttackEnded");

    #endregion

    private void Awake()
    {
        InitializeComponents();
        InitializeVFXCache();

        groundLayerMask = LayerMask.GetMask("Ground");
    }

    private void Start()
    {
        if (bossAICoroutine == null)
        {
            bossAICoroutine = StartCoroutine(BossFlowSequence());
        }

        StartCoroutine(AmbientSoundRoutine());
    }

    private void InitializeComponents()
    {
        if (enemyHealth == null) enemyHealth = GetComponent<EnemyHealth>();
        if (enemyHealth != null) enemyHealth.SetMaxHealth(maxHealth);
        currentHealth = maxHealth;

        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
                playerHealth = player.GetComponent<PlayerHealth>();
                playerController = player.GetComponent<CharacterController>();
            }
        }

        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (audioSource == null)
        {
            audioSource = GetComponentInChildren<AudioSource>();
            if (audioSource == null) ReportDebug("Warning: No AudioSource found on Boss.", 2);
        }

        if (ambientAudioSource == null)
        {
            ambientAudioSource = GetComponentInChildren<AudioSource>();
            if (ambientAudioSource == null) ReportDebug("Warning: No Ambient AudioSource found on Boss.", 2);
        }

        // Setup Camera Shake
        if (vcam == null) vcam = Object.FindFirstObjectByType<CinemachineCamera>();
        if (vcam != null) noise = vcam.GetCinemachineComponent(CinemachineCore.Stage.Noise) as CinemachineBasicMultiChannelPerlin;
    }

    private void InitializeVFXCache()
    {
        armorRenderers = GetComponentsInChildren<MeshRenderer>();
        armorPropertyBlock = new MaterialPropertyBlock();
        if (armorRenderers.Length > 0 && armorRenderers[0].sharedMaterial.HasProperty("_EmissionColor"))
        {
            originalEmissionColor = armorRenderers[0].sharedMaterial.GetColor("_EmissionColor");
        }
    }

    private void OnEnable()
    {
        if (enemyHealth != null) enemyHealth.OnDeath += HandleEnemyDeath;
        if (enemyHealth != null) enemyHealth.OnHealthChanged += HandleEnemyHealthChange;
    }

    private void OnDisable()
    {
        if (enemyHealth != null) enemyHealth.OnDeath -= HandleEnemyDeath;
        if (enemyHealth != null) enemyHealth.OnHealthChanged -= HandleEnemyHealthChange;
    }

    private void HandleEnemyHealthChange(float newCurrent, float newMax)
    {
        currentHealth = newCurrent;
        maxHealth = newMax;

        if (audioSource != null && damageReceivedSFX != null)
        {
            audioSource.PlayOneShot(damageReceivedSFX, 0.75f);
        }
    }

    private void HandleEnemyDeath(GameObject enemy)
    {
        if (enemy != gameObject) return;

        if (AsyncMusicController.Instance != null)
        {
            AsyncMusicController.Instance.PlayMusic(MusicState.Calm);
        }

        isInLowHealthPhase = false;
        speedBuffApplied = false;
        forceApocalipsisNext = false;
        
        StopAllCoroutines();
        CleanUpEffects();

        if (agent != null) agent.enabled = false;
        if (ambientAudioSource != null) ambientAudioSource.Stop();
        if (animator != null) animator.SetTrigger(AnimID_Death);

        this.enabled = false;
    }

    private void CleanUpEffects()
    {
        for (int i = instantiatedEffects.Count - 1; i >= 0; i--)
        {
            if (instantiatedEffects[i] != null) Destroy(instantiatedEffects[i]);
        }
        instantiatedEffects.Clear();
    }

    private IEnumerator AmbientSoundRoutine()
    {
        while (currentHealth > 0)
        {
            bool isMoving = agent != null && agent.enabled && !agent.isStopped && agent.velocity.sqrMagnitude > 0.1f;

            if (currentState == BossState.Stunned || currentState == BossState.Vulnerable || currentState == BossState.Attacking || currentState == BossState.Charging)
            {
                yield return null;
                continue;
            }

            if (isMoving)
            {
                if (movementClip != null && ambientAudioSource != null)
                {
                    ambientAudioSource.pitch = Random.Range(0.9f, 1.1f);
                    ambientAudioSource.PlayOneShot(movementClip, 0.5f);
                }

                float currentInterval = isInLowHealthPhase ? movementInterval * 0.7f : movementInterval;

                yield return new WaitForSeconds(currentInterval);
            }
            else
            {
                if (idleClip != null && ambientAudioSource != null)
                {
                    ambientAudioSource.pitch = Random.Range(0.95f, 1.05f);
                    ambientAudioSource.PlayOneShot(idleClip, 0.5f);
                }

                float waitTime = idleInterval + Random.Range(-idleIntervalVariance, idleIntervalVariance);
                yield return new WaitForSeconds(Mathf.Max(0.5f, waitTime));
            }
        }
    }

    #region Main AI Loop (The Flow Chart)

    /// <summary>
    /// Corrutina principal que maneja el Ciclo de 29 segundos.
    /// </summary>
    private IEnumerator BossFlowSequence()
    {
        yield return new WaitForSeconds(1f); // Breve espera inicial

        while (currentHealth > 0)
        {
            while (enemyHealth != null && enemyHealth.IsStunned)
            {
                if (agent != null && agent.enabled)
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                }
                yield return null;
            }

            CheckLowHPPhase();

            if (!forceApocalipsisNext && Random.value < chargeProbability)
            {
                yield return StartCoroutine(ExecuteChargeAbility());
            }

            forceApocalipsisNext = false;

            yield return StartCoroutine(ExecuteApocalipsisSequence());

            yield return StartCoroutine(ExecutePositioningPhase());

            yield return StartCoroutine(ExecuteSodomaYGomorra());

            yield return StartCoroutine(ExecuteCooldownPhase());
        }
    }

    private void CheckLowHPPhase()
    {
        if (!isInLowHealthPhase && currentHealth <= maxHealth * 0.25f)
        {
            isInLowHealthPhase = true;
            ReportDebug("FASE DE VIDA BAJA: VELOCIDAD AUMENTADA", 1);

            // Aplicar aumento de velocidad permanente
            if (!speedBuffApplied)
            {
                agent.speed *= lowHPSpeedMultiplier;
                if (animator != null) animator.speed *= 1.2f; // Acelerar animaciones ligeramente
                speedBuffApplied = true;
            }
        }
    }

    #endregion

    /*
    #region ANIMATION EVENTS (Nuevo Sistema)

    public void AnimEvent_SlashImpact()
    {
        if (currentState != BossState.Attacking) return;

        SpawnSlashVFX();
        if (audioSource) audioSource.PlayOneShot(apocalipsisSlashSFX, 0.5f);
        DealAreaDamage(swordTransform.position, apocalipsisRange, apocalipsisDamage);

    }

    public void AnimEvent_SodomaImpact()
    {
        if (currentState != BossState.Attacking) return;
        
        SpawnSlashVFX();
        if (audioSource) audioSource.PlayOneShot(sodomaAttackSFX);
        DealAreaDamage(swordTransform.position, sodomaCutRange, sodomaDamage);
    }

    #endregion
    */

    #region Cycle 1: Apocalipsis Logic

    private IEnumerator ExecuteApocalipsisSequence()
    {
        ReportDebug("ETAPA 1: APOCALIPSIS", 1);

        currentState = BossState.Attacking;

        float totalAnimTime = apocalipsisDashDuration + apocalipsisImpactDelay + apocalipsisRecoveryTime;
        float timePerDash = apocalipsisDuration / (float)apocalipsisTargetDashes;
        float waitTimeBetweenDashes = Mathf.Max(0f, timePerDash - totalAnimTime);

        if (animator != null)
        {
            animator.ResetTrigger(AnimID_AttackDash);
            animator.ResetTrigger(AnimID_SoloDash);
            animator.SetBool(AnimID_AttackEnded, false);
        }

        int dashesPerformed = 0;

        while (dashesPerformed < apocalipsisTargetDashes)
        {
            float dashCycleStart = Time.time;

            // 1. Persecución breve
            if (agent != null && agent.enabled)
            {
                agent.isStopped = false;
                agent.velocity = Vector3.zero;
            }
            if (animator != null) animator.SetBool(AnimID_Walking, true);

            agent.SetDestination(player.position);

            yield return new WaitForSeconds(0.25f);

            // 2. Preparar Dash
            if (agent != null && agent.enabled)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
            if (animator != null) animator.SetBool(AnimID_Walking, false);

            Vector3 targetPos = GetZigZagDashPosition();
            targetPos.y = transform.position.y;

            if (animator != null) animator.SetTrigger(AnimID_AttackDash);

            // 3. Ejecutar Dash
            yield return MoveToPositionFast(targetPos, apocalipsisDashDuration);

            // 4. Esperar momento de impacto
            yield return new WaitForSeconds(apocalipsisImpactDelay);

            // 5. Aplicar daño y vfx
            SpawnSlashVFX();

            if (audioSource) audioSource.PlayOneShot(apocalipsisSlashSFX, 0.5f);

            DealAreaDamage(swordTransform.position + transform.forward, apocalipsisRange, apocalipsisDamage);

            yield return new WaitForSeconds(apocalipsisRecoveryTime);

            // 6. Retroceso
            Vector3 retreatPos = transform.position - transform.forward * 2.5f;
            yield return MoveToPositionFast(retreatPos, 0.25f);

            // Encarar al jugador
            if (player != null)
            {
                Vector3 lookPos = player.position;
                lookPos.y = transform.position.y;
                transform.LookAt(lookPos);
            }

            dashesPerformed++;

            if (waitTimeBetweenDashes > 0) yield return new WaitForSeconds(waitTimeBetweenDashes);
            else yield return null;
        }

        if (animator != null) animator.SetBool(AnimID_AttackEnded, true);

        currentState = BossState.Idle;
    }

    private Vector3 GetZigZagDashPosition()
    {
        if (player == null) return transform.position;

        Vector3 dirToPlayer = (player.position - transform.position).normalized;
        Vector3 sideStep = Vector3.Cross(dirToPlayer, Vector3.up) * (Random.value > 0.5f ? 1 : -1);

        // Intentar flanquear
        return player.position + (dirToPlayer * -2f) + (sideStep * 3f);
    }

    #endregion

    #region Cycle 2: Positioning Phase

    private IEnumerator ExecutePositioningPhase()
    {
        ReportDebug("ETAPA 2: POSICIONAMIENTO", 1);

        float timer = 0f;
        if (agent != null && agent.enabled)
        {
            agent.isStopped = false;
        }

        if (animator != null) animator.SetBool(AnimID_Walking, true);

        while (timer < positioningDuration)
        {
            if (player != null)
            {
                Vector3 direction = (transform.position - player.position).normalized;
                Vector3 targetPos = player.position + direction * 7f; // Buscar estar a 7m
                agent.SetDestination(targetPos);
            }

            timer += Time.deltaTime;

            yield return null;
        }

        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        if (animator != null) animator.SetBool(AnimID_Walking, false);
    }

    #endregion

    #region Cycle 3: Sodoma y Gomorra Logic

    private IEnumerator ExecuteSodomaYGomorra()
    {
        ReportDebug("ETAPA 3: SODOMA Y GOMORRA", 1);
        currentState = BossState.Attacking;

        if (player != null)
        {
            Vector3 lookPos = player.position;
            lookPos.y = transform.position.y;
            transform.LookAt(lookPos);
        }

        if (animator != null)
        {
            animator.ResetTrigger(AnimID_SwordStack);
            animator.ResetTrigger(AnimID_SoloDash);
            animator.SetBool(AnimID_AttackEnded, false);
        }

        yield return new WaitForSeconds(0.25f);

        Vector3 backwardPos = transform.position - transform.forward * sodomaBackwardDistance;

        // 1. Moverse hacia atrás
        if (animator != null) animator.SetBool(AnimID_Walking, true);
        yield return MoveToPositionFast(backwardPos, 0.5f);
        if (animator != null) animator.SetBool(AnimID_Walking, false);

        // 2. Cargar
        StartArmorGlow();
        if (audioSource != null) audioSource.PlayOneShot(sodomaChargeSFX);

        // Cálculo de posición de impacto
        Vector3 dirToPlayer = (player.position - transform.position).normalized;
        float stopDistance = 1.2f;
        Vector3 dashDestination = player.position - (dirToPlayer * stopDistance);
        dashDestination.y = transform.position.y;

        yield return new WaitForSeconds(sodomaChargeTime);

        StopArmorGlow();

        if (animator != null) animator.SetTrigger(AnimID_SoloDash);

        // 3. Movimiento y Ataque
        yield return MoveToPositionFast(dashDestination, 0.25f);

        SpawnGreenFireTrail(backwardPos, dashDestination);

        yield return new WaitForSeconds(sodomaDashDuration);

        if (animator != null) animator.SetTrigger(AnimID_SwordStack);

        Vector3 impactCenter = swordTransform.position + transform.forward;
        Vector3 activeWarningCenter = GetGroundPosition(impactCenter);
        GameObject activeWarning = SpawnSodomaWarning(activeWarningCenter, sodomaCutRange);

        // 4. Esperar impacto
        yield return new WaitForSeconds(sodomaImpactDelay);

        if (activeWarning != null) Destroy(activeWarning);

        // 5. Aplicar daño y vfx
        SpawnSlashVFX();
        if (audioSource) audioSource.PlayOneShot(sodomaAttackSFX);
        DealAreaDamage(impactCenter, sodomaCutRange, sodomaDamage);

        if (animator != null) animator.SetBool(AnimID_AttackEnded, true);

        yield return new WaitForSeconds(attackEndDuration);

        currentState = BossState.Idle;
    }

    #endregion

    #region Cycle 4: Cooldown Phase (Necio Pecador + Vulnerable)

    private IEnumerator ExecuteCooldownPhase()
    {
        ReportDebug("ETAPA 4: NECIO PECADOR", 1);

        float phaseTimer = 0f;

        if (agent != null && agent.enabled)
        {
            agent.isStopped = false;
        }

        agent.speed = isInLowHealthPhase ? (moveSpeed * lowHPSpeedMultiplier * 0.8f) : (moveSpeed * 0.7f);

        float currentWarningTime = isInLowHealthPhase ? (necioPecadorWarningTime * 0.6f) : necioPecadorWarningTime;
        int attackCount = 0;

        while (phaseTimer < necioAttackWindow)
        {
            if (player != null)
            {
                float dist = Vector3.Distance(transform.position, player.position);
                if (dist > 12f) agent.SetDestination(player.position);
                else
                {
                    Vector3 orbit = transform.position + transform.right * 3f;
                    agent.SetDestination(orbit);
                }
            }

            if (animator != null)
            {
                bool isMoving = agent.velocity.magnitude > 0.1f && !agent.isStopped;
                animator.SetBool(AnimID_Walking, isMoving);
            }

            if (animator != null) animator.SetTrigger(AnimID_AttackHand);
            if (audioSource != null) audioSource.PlayOneShot(necioPecadorSummonSFX);

            Vector3 targetPos = player.position;
            if (playerController != null)
            {
                float predictionFactor = Mathf.Clamp(attackCount * 0.3f, 0f, 2.0f);
                targetPos += playerController.velocity * predictionFactor;
            }

            Vector3 finalTargetPos = GetGroundPosition(targetPos);

            GameObject warning = SpawnNecioPecadorWarning(finalTargetPos, necioPecadorRadius);

            yield return new WaitForSeconds(currentWarningTime);
            if (warning != null) Destroy(warning);

            SpawnSoulHand(finalTargetPos);

            float interval = isInLowHealthPhase ? 1.5f : 2.5f;
            float remainingWait = interval - currentWarningTime;

            if (remainingWait > 0) yield return new WaitForSeconds(remainingWait);

            phaseTimer += interval;
            attackCount++;
        }

        agent.speed = isInLowHealthPhase ? (moveSpeed * lowHPSpeedMultiplier) : moveSpeed;

        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        if (animator != null) animator.SetBool(AnimID_Walking, false);

        currentState = BossState.Vulnerable;
        yield return new WaitForSeconds(necioVulnerableWindow);

        currentState = BossState.Idle;
    }

    #endregion

    #region Special Ability: Embestida del Fornido

    private IEnumerator ExecuteChargeAbility()
    {
        ReportDebug("EJECUTANDO EMBESTIDA DEL FORNIDO", 1);
        currentState = BossState.Charging;

        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        yield return new WaitForSeconds(1f);

        StartEyeGlow();

        if (player != null)
        {
            Vector3 chargeDir = (player.position - transform.position).normalized;
            chargeDir.y = 0; // Mantener en plano horizontal
            transform.rotation = Quaternion.LookRotation(chargeDir);
        }

        if (audioSource != null) audioSource.PlayOneShot(chargeAbilitySFX);

        GameObject chargeTrail = null;
        if (darkWindTrailPrefab != null)
        {
            chargeTrail = Instantiate(darkWindTrailPrefab, transform.position, Quaternion.identity);
            instantiatedEffects.Add(chargeTrail);
            chargeTrail.transform.SetParent(transform);
            if (chargeTrail != null) Destroy(chargeTrail, 3f);
        }

        float chargeTime = 0f;
        bool hitPlayer = false;

        while (chargeTime < 2.0f && !hitPlayer)
        {
            agent.Move(transform.forward * chargeAbilitySpeed * Time.deltaTime);

            // Detectar colisión con Jugador
            Collider[] hits = Physics.OverlapSphere(transform.position + transform.forward * 1.5f, 1.5f);
            foreach (Collider col in hits)
            {
                if (col.CompareTag("Player"))
                {
                    hitPlayer = true;
                    ApplyEmbestidaEffect(col.gameObject);
                    break;
                }
            }

            // Detectar Pared (Frenar)
            Vector3 boxCenter = transform.position + Vector3.up * 3f;
            Vector3 boxSize = new Vector3(1.5f, 3f, 1.3f);

            if (Physics.BoxCast(boxCenter, boxSize, transform.forward, transform.rotation, 1.5f, chargeObstacleLayers))
            {
                ReportDebug("Embestida choco con obstaculo", 1);
                break;
            }

            chargeTime += Time.deltaTime;
            yield return null;
        }

        StopEyeGlow();

        if (chargeTrail != null) Destroy(chargeTrail);

        if (hitPlayer)
        {
            ReportDebug("Embestida conectó. Encadenando Apocalipsis", 1);
            forceApocalipsisNext = true; // Forzar Apocalipsis en el siguiente ciclo
            yield return new WaitForSeconds(0.5f);
        }
        else
        {
            yield return new WaitForSeconds(1.5f);
        }

        currentState = BossState.Idle;
    }

    private void ApplyEmbestidaEffect(GameObject playerObj)
    {
        PlayerHealth ph = playerObj.GetComponent<PlayerHealth>();
        if (ph != null)
        {
            ph.ApplyStun(playerStunDuration); 
            ReportDebug($"Jugador aturdido por Embestida durante {playerStunDuration}s", 1);
        }

        if (stunEffectPrefab != null)
        {
            Instantiate(stunEffectPrefab, playerObj.transform.position + Vector3.up * 2, Quaternion.identity, playerObj.transform);
        }
    }

    #endregion

    #region Utility & VFX Methods

    private void DealAreaDamage(Vector3 center, float radius, float damage)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player") && playerHealth != null)
            {
                ExecuteAttack(hit.gameObject, damage);
                break;
            }
        }
    }

    private void ExecuteAttack(GameObject target, float damageAmount)
    {
        if (target.TryGetComponent<PlayerBlockSystem>(out var blockSystem) && target.TryGetComponent<PlayerHealth>(out var health))
        {
            // Verificar si el ataque es bloqueado
            if (blockSystem.IsBlocking && blockSystem.CanBlockAttack(transform.position))
            {
                float remainingDamage = blockSystem.ProcessBlockedAttack(damageAmount);

                if (remainingDamage > 0f)
                {
                    health.TakeDamage(remainingDamage, false, AttackDamageType.Melee);
                }

                ReportDebug("Ataque bloqueado por el jugador.", 1);
                return;
            }

            health.TakeDamage(damageAmount, false, AttackDamageType.Melee);
        }
        else if (target.TryGetComponent<PlayerHealth>(out var healthOnly))
        {
            healthOnly.TakeDamage(damageAmount, false, AttackDamageType.Melee);
        }
    }

    private IEnumerator MoveToPositionFast(Vector3 target, float duration)
    {
        // Mover manualmente el agente sin pathfinding para movimientos rápidos/dashes
        Vector3 start = transform.position;
        float elapsed = 0;
        while (elapsed < duration)
        {
            // Usar Lerp para movimiento lineal (dash)
            Vector3 nextPos = Vector3.Lerp(start, target, elapsed / duration);

            // Validar con NavMesh para no salir del mapa
            NavMeshHit hit;
            if (NavMesh.SamplePosition(nextPos, out hit, 2.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
            else
            {
                transform.position = nextPos;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private Vector3 GetGroundPosition(Vector3 targetPos)
    {
        if (Physics.Raycast(targetPos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f, groundLayerMask))
        {
            return hit.point + Vector3.up * 0.05f;
        }

        if (NavMesh.SamplePosition(targetPos, out NavMeshHit navHit, 2.0f, NavMesh.AllAreas))
        {
            return navHit.position + Vector3.up * 0.05f;
        }

        return new Vector3(targetPos.x, transform.position.y + 0.02f, targetPos.z);
    }

    private void SpawnGreenFireTrail(Vector3 start, Vector3 end)
    {
        if (greenFireTrailPrefab == null) return;

        Vector3 center = (start + end) / 2f;
        float dist = Vector3.Distance(start, end);

        GameObject trail = Instantiate(greenFireTrailPrefab, center, Quaternion.LookRotation(end - start));
        // Ajustar escala Z para cubrir la distancia
        trail.transform.localScale = new Vector3(1, 1, dist);
        instantiatedEffects.Add(trail);

        FireTrail ft = trail.GetComponent<FireTrail>();
        if (ft != null)
        {
            ft.DamagePerSecond = fireTrailDamagePerSecond;
            ft.Lifetime = fireTrailLifeTime;
        }
    }

    private GameObject SpawnNecioPecadorWarning(Vector3 pos, float radius)
    {
        if (necioPecadorWarningPrefab == null) return null;
        GameObject warningObj = Instantiate(necioPecadorWarningPrefab, pos, Quaternion.identity);
        warningObj.transform.localScale = new Vector3(radius * 2, 0.1f, radius * 2); // Ajustar tamaño visual
        instantiatedEffects.Add(warningObj);
        return warningObj;
    }

    private GameObject SpawnSodomaWarning(Vector3 pos, float radius)
    {
        GameObject obj = SpawnNecioPecadorWarning(pos, radius);
        if (obj != null)
        {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                r.material.color = Color.green;
            }
        }
        return obj;
    }

    private void SpawnSoulHand(Vector3 pos)
    {
        if (soulHandPrefab == null) return;
        GameObject hand = Instantiate(soulHandPrefab, pos, Quaternion.identity);
        instantiatedEffects.Add(hand);

        SoulHand sh = hand.GetComponent<SoulHand>();
        if (sh != null) sh.Initialize(necioPecadorDamage, necioPecadorRadius);

        if (audioSource != null) audioSource.PlayOneShot(necioPecadorExplosionSFX);
    }

    private void SpawnSlashVFX()
    {
        if (greenFireVFX != null && swordTransform != null)
        {
            ParticleSystem vfx = Instantiate(greenFireVFX, swordTransform.position, swordTransform.rotation);
            vfx.Play();
            if (vfx.gameObject != null) Destroy(vfx.gameObject, 1f);
        }
    }

    private void StartArmorGlow()
    {
        if (armorGlowVFX != null) armorGlowVFX.Play();
        ApplyArmorEmission(Color.green * 3f);
    }

    private void StopArmorGlow()
    {
        if (armorGlowVFX != null) armorGlowVFX.Stop();
        ApplyArmorEmission(originalEmissionColor);
    }

    private void ApplyArmorEmission(Color c)
    {
        if (armorRenderers == null) return;
        foreach (var r in armorRenderers)
        {
            r.GetPropertyBlock(armorPropertyBlock);
            armorPropertyBlock.SetColor("_EmissionColor", c);
            r.SetPropertyBlock(armorPropertyBlock);
        }
    }

    private void StartEyeGlow() 
    { 
        //Implementar si hay material de ojos
    }

    private void StopEyeGlow()
    {
        //Restaurar material de ojos
    }

    #endregion

    #region Debug 

    private void OnDrawGizmos()
    {
        Vector3 damageOrigin = (swordTransform != null) ? swordTransform.position : transform.position;

        Color stateColor = currentState switch
        {
            BossState.Idle => Color.white,
            BossState.Chasing => Color.blue,
            BossState.Attacking => Color.red,
            BossState.Charging => new Color(1f, 0.64f, 0f), // Naranja
            BossState.Vulnerable => Color.green,
            BossState.Stunned => Color.yellow,
            _ => Color.grey
        };

        // Cycle 1
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.position, 1.0f);

        Vector3 attackCenter = damageOrigin + transform.forward;

        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawSphere(attackCenter, apocalipsisRange);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(damageOrigin, attackCenter);

        // Cycle 3
        Vector3 sodomaCenter = damageOrigin + transform.forward;

        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Gizmos.DrawSphere(sodomaCenter, sodomaCutRange);

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(sodomaCenter, sodomaCutRange);

        // Special Ability
        Gizmos.color = new Color(0.5f, 0f, 0.5f, 0.5f);
        Gizmos.DrawSphere(transform.position + Vector3.forward * 4.5f, necioPecadorRadius);

#if UNITY_EDITOR
        GUIStyle style = new GUIStyle();
        style.normal.textColor = stateColor;
        style.fontSize = 14;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;

        string debugText = $"{currentState}\nHP: {(int)currentHealth}";
        if (isInLowHealthPhase) debugText += "\n[BERSERK]";

        UnityEditor.Handles.Label(transform.position + Vector3.up * 2.5f, debugText, style);
#endif
    }

    private void OnGUI()
    {
        if (!showDebugGUI) return;
        GUILayout.BeginArea(new Rect(10, 10, 200, 100));
        GUILayout.Label($"State: {currentState}");
        GUILayout.Label($"HP: {currentHealth}");
        GUILayout.EndArea();
    }

    #endregion

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    /// <summary> 
    /// Funcipn de depuracion para reportar mensajes en la consola de Unity. 
    /// </summary> 
    /// <<param name="message">Mensaje a reportar.</param> >
    /// <param name="reportPriorityLevel">Nivel de prioridad: Debug, Warning, Error.</param>
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[BloodKnightBoss] {message}");
                break;
            case 2:
                Debug.LogWarning($"[BloodKnightBoss] {message}");
                break;
            case 3:
                Debug.LogError($"[BloodKnightBoss] {message}");
                break;
            default:
                Debug.Log($"[BloodKnightBoss] {message}");
                break;
        }
    }
}