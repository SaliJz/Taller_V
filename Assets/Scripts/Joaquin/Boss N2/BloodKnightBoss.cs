using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;

public class BloodKnightBoss : MonoBehaviour
{
    #region Statistics and Configuration

    [Header("Boss Configuration")]
    [SerializeField] private float maxHealth = 300f;
    [SerializeField] private float currentHealth;
    [SerializeField] private float moveSpeed = 5f;

    [Header("Attack 1 - Sodoma y Gomorra")]
    [SerializeField] private float sodomaDamage = 35f;
    [SerializeField] private float sodomaCutRange = 8f;
    [SerializeField] private float fireTrailDamagePerSecond = 5f;
    [SerializeField] private float sodomaCooldown = 15f;
    [SerializeField] private float sodomaChargeTime = 1.5f;
    [SerializeField] private float sodomaStunDuration = 4f;
    [SerializeField] private float sodomaBackwardDistance = 3f;

    [Header("Attack 2 - Apocalipsis")]
    [SerializeField] private float apocalipsisDamage = 7f;
    [SerializeField] private int apocalipsisAttackCount = 10;
    [SerializeField] private float apocalipsisAttackInterval = 1f;
    [SerializeField] private float apocalipsisDashDistance = 5f;
    [SerializeField] private float apocalipsisRange = 4f;

    [Header("Attack 3 - Necio Pecador")]
    [SerializeField] private float necioPecadorDamage = 15f;
    [SerializeField] private float necioPecadorCooldown = 12f;
    [SerializeField] private int necioPecadorChainCount = 3;
    [SerializeField] private float necioPecadorRadius = 1.5f;
    [SerializeField] private float necioPecadorWarningTime = 2.5f;
    [SerializeField] private float necioPecadorWarningTimeLowHP = 1.5f;
    [SerializeField] private float necioPecadorChainDuration = 7.5f;
    [SerializeField] private float necioPecadorChainDurationLowHP = 4.5f;

    [Header("Special - Embestida del Fornido")]
    [SerializeField] private float chargeAbilityDamage = 5f;
    //[SerializeField] private float chargeAbilityStunDuration = 3f;
    [SerializeField] private float chargeAbilitySpeed = 15f;
    [SerializeField] private float chargeAbilityKneelingTime = 2f;
    [SerializeField] private float chargeAbilityRecoveryTime = 2f;
    [SerializeField] private float chargeAbilityKnockbackForce = 10f;

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
    [SerializeField] private AudioClip sodomaChargeSound;
    [SerializeField] private AudioClip sodomaAttackSound;
    [SerializeField] private AudioClip apocalipsisSlashSound;
    [SerializeField] private AudioClip necioPecadorSummonSound;
    [SerializeField] private AudioClip necioPecadorExplosionSound;
    [SerializeField] private AudioClip chargeAbilitySound;
    [SerializeField] private AudioClip stunSound;
    [SerializeField] private AudioClip deathSound;

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
        Charging
    }

    private BossState currentState = BossState.Idle;
    private NavMeshAgent agent;
    private EnemyHealth enemyHealth;
    private PlayerHealth playerHealth;

    private float lastSodomaTime;
    private float lastNecioPecadorTime;
    private bool isVulnerableToCounter = false;
    private bool isInLowHealthPhase = false;

    private Coroutine bossAICoroutine;
    //private Coroutine stunCoroutine;
    private Coroutine currentAttackCoroutine;

    private Vector3 chargeDirection;
    private bool isCharging = false;

    private MeshRenderer[] armorRenderers;
    private MaterialPropertyBlock armorPropertyBlock;
    private Color originalEmissionColor;
    private List<GameObject> instantiatedEffects = new List<GameObject>();

    #endregion

    #region Camera Shake

    [SerializeField] private CinemachineCamera vcam;
    [SerializeField] private float defaultDuration = 0.5f;
    [SerializeField] private float defaultAmplitude = 0.2f;
    [SerializeField] private float defaultFrequency = 10f;
    [SerializeField] private float fadeOutTime = 0.15f;

    private CinemachineBasicMultiChannelPerlin noise;
    private Coroutine shakeCoroutine;
    private float originalAmplitude;
    private float originalFrequency;

    #endregion

    #region Properties

    private void Awake()
    {
        InitializeComponents();
        InitializeVFXCache();
    }

    private void Start()
    {
        if (bossAICoroutine == null)
        {
            bossAICoroutine = StartCoroutine(BossAI());
        }
    }

    private void InitializeComponents()
    {
        if (enemyHealth == null) enemyHealth = GetComponent<EnemyHealth>();
        if (enemyHealth != null) enemyHealth.SetMaxHealth(maxHealth);
        currentHealth = maxHealth;

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.stoppingDistance = sodomaCutRange - 2f;
        }

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
                playerHealth = player.GetComponent<PlayerHealth>();
            }
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                ReportDebug("AudioSource agregado dinámicamente.", 2);
            }
        }

        if (vcam == null)
        {
            CinemachineCamera virtualCamera = Object.FindFirstObjectByType<CinemachineCamera>();
            if (virtualCamera != null)
            {
                vcam = virtualCamera;
            }
        }

        if (vcam != null)
        {
            noise = vcam.GetCinemachineComponent(CinemachineCore.Stage.Noise) as CinemachineBasicMultiChannelPerlin;
            if (noise == null)
            {
                Debug.LogWarning("[CameraShakeController] No se encontró CinemachineBasicMultiChannelPerlin en la vcam.");
                return;
            }
        }

        if (noise != null)
        {
            originalAmplitude = noise.AmplitudeGain;
            originalFrequency = noise.FrequencyGain;
        }
    }

    private void InitializeVFXCache()
    {
        armorRenderers = GetComponentsInChildren<MeshRenderer>();
        armorPropertyBlock = new MaterialPropertyBlock();

        if (armorRenderers != null && armorRenderers.Length > 0)
        {
            Material mat = armorRenderers[0].sharedMaterial;
            if (mat != null && mat.HasProperty("_EmissionColor"))
            {
                originalEmissionColor = mat.GetColor("_EmissionColor");
            }
        }
    }

    #endregion

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

    private void OnDestroy()
    {
        if (enemyHealth != null) enemyHealth.OnDeath -= HandleEnemyDeath;
        if (enemyHealth != null) enemyHealth.OnHealthChanged -= HandleEnemyHealthChange;
    }

    private void HandleEnemyDeath(GameObject enemy)
    {
        if (enemy != gameObject) return;

        isVulnerableToCounter = false;
        isInLowHealthPhase = false;
        showDebugGUI = false;

        StopAllCoroutines();

        DestroyAllInstantiatedEffects();

        if (agent != null)
        {
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
                agent.updatePosition = false;
                agent.updateRotation = false;
            }
            else
            {
                agent.enabled = false;
            }
        }

        if (animator != null) animator.SetTrigger("Die");
        if (audioSource != null && deathSound != null) audioSource.PlayOneShot(deathSound);

        this.enabled = false;
    }

    private void DestroyAllInstantiatedEffects()
    {
        foreach (GameObject effect in instantiatedEffects)
        {
            if (effect != null)
            {
                Destroy(effect);
            }
        }

        instantiatedEffects.Clear();
    }

    private void HandleEnemyHealthChange(float newCurrentHealth, float newMaxHealth)
    {
        currentHealth = newCurrentHealth;
        maxHealth = newMaxHealth;
    }

    #region AI System

    private IEnumerator BossAI()
    {
        while (currentHealth > 0)
        {
            // Verificar fase de vida baja
            if (!isInLowHealthPhase && currentHealth <= maxHealth * 0.25f)
            {
                isInLowHealthPhase = true;
                ReportDebug("¡Dark Knight entró en fase de vida baja (25%)!", 1);
            }

            // No hacer nada si está aturdido o cargando
            if (currentState == BossState.Stunned || currentState == BossState.Charging)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            // Verificar jugador
            if (player == null)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            float distanceToPlayer = Vector3.Distance(transform.position, player.position);

            // Perseguir si está lejos
            if (distanceToPlayer > agent.stoppingDistance + 2f)
            {
                ChasePlayer();
                yield return new WaitForSeconds(0.2f);
                continue;
            }

            // Detener movimiento al alcanzar rango de ataque
            if (agent.hasPath && distanceToPlayer <= agent.stoppingDistance + 1f)
            {
                agent.ResetPath();
                if (animator != null) animator.SetBool("IsMoving", false);
            }

            // Decidir ataque (si no está atacando actualmente)
            if (currentState != BossState.Attacking)
            {
                // 10% de probabilidad de usar embestida aleatoriamente
                if (UnityEngine.Random.value < 0.1f)
                {
                    currentAttackCoroutine = StartCoroutine(ExecuteChargeAbility());
                    yield return currentAttackCoroutine;
                    continue;
                }

                // Prioridad a Sodoma y Gomorra si está disponible
                if (Time.time >= lastSodomaTime + sodomaCooldown)
                {
                    currentAttackCoroutine = StartCoroutine(ExecuteSodomaYGomorra());
                    yield return currentAttackCoroutine;
                }
                // Necio Pecador si está disponible
                else if (Time.time >= lastNecioPecadorTime + necioPecadorCooldown)
                {
                    currentAttackCoroutine = StartCoroutine(ExecuteNecioPecador());
                    yield return currentAttackCoroutine;
                }
                // Si no, Apocalipsis
                else
                {
                    currentAttackCoroutine = StartCoroutine(ExecuteApocalipsis());
                    yield return currentAttackCoroutine;
                }
            }

            yield return new WaitForSeconds(0.2f);
        }

        // Boss derrotado
        bossAICoroutine = null;
    }

    private void ChasePlayer()
    {
        if (currentState == BossState.Attacking) return;

        currentState = BossState.Chasing;
        agent.isStopped = false;
        agent.SetDestination(player.position);

        if (animator != null)
        {
            animator.SetBool("IsMoving", true);
        }
    }

    #endregion

    #region Attack 1 - Sodoma y Gomorra

    private IEnumerator ExecuteSodomaYGomorra()
    {
        ReportDebug("Ejecutando: Sodoma y Gomorra", 1);

        currentState = BossState.Attacking;
        agent.isStopped = true;
        lastSodomaTime = Time.time;

        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        directionToPlayer.y = 0;
        transform.rotation = Quaternion.LookRotation(directionToPlayer);

        Vector3 startDashPosition = transform.position;

        // FASE 1: Deslizarse hacia atrás y agacharse
        Vector3 backwardPosition = transform.position - transform.forward * sodomaBackwardDistance;

        if (animator != null) animator.SetTrigger("Crouch");

        yield return MoveToPosition(backwardPosition, 0.5f);

        // FASE 2: Carga (brillo verde, vulnerable a contraataque)
        StartArmorGlow();
        if (audioSource != null && sodomaChargeSound != null) audioSource.PlayOneShot(sodomaChargeSound);
        isVulnerableToCounter = true;

        yield return new WaitForSeconds(sodomaChargeTime);

        isVulnerableToCounter = false;
        
        StopArmorGlow();

        ReportDebug("Ha terminado de cargar 'Sodoma y Gomorra'.", 1);

        if (currentState == BossState.Stunned) yield break;

        if (animator != null) animator.SetTrigger("DashSlash");
        if (audioSource != null && sodomaAttackSound != null) audioSource.PlayOneShot(sodomaAttackSound);

        Vector3 endDashPosition = player.position;
        endDashPosition.y = transform.position.y;

        yield return MoveToPosition(endDashPosition, 0.5f);

        PerformSodomaSlash();
        SpawnGreenFireTrail(startDashPosition, endDashPosition);

        yield return new WaitForSeconds(1f);

        currentState = BossState.Idle;
        agent.isStopped = false;

        ReportDebug("Sodoma y Gomorra completado", 1);
    }

    private void PerformSodomaSlash()
    {
        // Detectar en semicírculo frontal
        Collider[] hits = Physics.OverlapSphere(transform.position, sodomaCutRange);

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                Vector3 dirToPlayer = (hit.transform.position - transform.position).normalized;
                float angle = Vector3.Angle(transform.forward, dirToPlayer);

                // 180° de área (90° a cada lado)
                if (angle <= 90f)
                {
                    if (playerHealth != null)
                    {
                        playerHealth.TakeDamage(sodomaDamage);
                        ReportDebug($"Sodoma golpeó al jugador: {sodomaDamage} de daño", 1);
                    }
                }
            }
        }
    }

    private void SpawnGreenFireTrail(Vector3 start, Vector3 end)
    {
        if (greenFireTrailPrefab == null) return;

        GameObject trail = Instantiate(greenFireTrailPrefab);
        instantiatedEffects.Add(trail);

        Vector3 center = (start + end) / 2f;
        trail.transform.position = center;

        Vector3 direction = end - start;
        trail.transform.rotation = Quaternion.LookRotation(direction);

        float distance = direction.magnitude;
        trail.transform.localScale = new Vector3(1f, 1f, distance);

        // Configurar daño del rastro
        FireTrail fireScript = trail.GetComponent<FireTrail>();
        if (fireScript != null)
        {
            fireScript.DamagePerSecond = fireTrailDamagePerSecond;
        }

        Destroy(trail, 10f);
    }

    #endregion

    #region Attack 2 - Apocalipsis

    private IEnumerator ExecuteApocalipsis()
    {
        ReportDebug("Ejecutando: Apocalipsis", 1);

        currentState = BossState.Attacking;
        agent.isStopped = true;

        if (animator != null) animator.SetTrigger("StartApocalipsis");

        for (int i = 0; i < apocalipsisAttackCount; i++)
        {
            // Deslizamiento diagonal aleatorio alrededor del jugador
            float angle = UnityEngine.Random.Range(0f, 360f);
            Vector3 offset = Quaternion.Euler(0, angle, 0) * Vector3.forward * apocalipsisDashDistance;
            Vector3 targetPos = player.position + offset;
            targetPos.y = transform.position.y;

            yield return MoveToPosition(targetPos, 0.3f);

            // Rotar hacia el jugador
            Vector3 dirToPlayer = (player.position - transform.position).normalized;
            dirToPlayer.y = 0;
            transform.rotation = Quaternion.LookRotation(dirToPlayer);

            // Alternar entre estocada y corte
            bool isThrust = (i % 2 == 0);

            if (animator != null)
            {
                animator.SetTrigger(isThrust ? "Thrust" : "Slash");
            }

            if (audioSource != null && apocalipsisSlashSound != null)
            {
                audioSource.PlayOneShot(apocalipsisSlashSound);
            }

            // Crear VFX de corte verde
            SpawnSlashVFX();

            DealApocalipsisDamage();

            yield return new WaitForSeconds(apocalipsisAttackInterval);
        }

        yield return new WaitForSeconds(0.5f);

        currentState = BossState.Idle;
        agent.isStopped = false;

        ReportDebug("Apocalipsis completado", 1);
    }

    public void DealApocalipsisDamage()
    {
        Vector3 attackCenter = transform.position + transform.forward * (apocalipsisRange / 2f);
        Collider[] hits = Physics.OverlapSphere(attackCenter, apocalipsisRange / 1.5f);

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(apocalipsisDamage);
                    ReportDebug($"Apocalipsis golpeó al jugador con OverlapSphere: {apocalipsisDamage} de daño", 1);

                    break;
                }
            }
        }
    }

    private void SpawnSlashVFX()
    {
        if (greenFireVFX != null && swordTransform != null)
        {
            ParticleSystem vfx = Instantiate(greenFireVFX, swordTransform.position, swordTransform.rotation);
            instantiatedEffects.Add(vfx.gameObject);
            vfx.Play();
            Destroy(vfx.gameObject, 2f);
        }
    }

    #endregion

    #region Attack 3 - Necio Pecador

    private IEnumerator ExecuteNecioPecador()
    {
        ReportDebug("Ejecutando: Necio Pecador", 1);

        currentState = BossState.Attacking;
        agent.isStopped = true;
        lastNecioPecadorTime = Time.time;

        float warningTime = isInLowHealthPhase ? necioPecadorWarningTimeLowHP : necioPecadorWarningTime;
        float chainInterval = isInLowHealthPhase ?
            (necioPecadorChainDurationLowHP / necioPecadorChainCount) :
            (necioPecadorChainDuration / necioPecadorChainCount);

        for (int i = 0; i < necioPecadorChainCount; i++)
        {
            // Gesto de invocación
            if (animator != null) animator.SetTrigger("SummonHand");
            if (audioSource != null && necioPecadorSummonSound != null) audioSource.PlayOneShot(necioPecadorSummonSound);

            // Posición aleatoria cerca del jugador
            Vector3 spawnPos = player.position + UnityEngine.Random.insideUnitSphere * 3f;
            spawnPos.y = player.position.y;

            // Spawn warning (círculo brillante en el suelo)
            GameObject warning = SpawnNecioPecadorWarning(spawnPos);
            if (warning != null) instantiatedEffects.Add(warning);

            yield return new WaitForSeconds(warningTime);

            // Destruir warning y spawnear mano
            if (warning != null)
            {
                Destroy(warning);
            }

            SpawnSoulHand(spawnPos);

            yield return new WaitForSeconds(chainInterval - warningTime);
        }

        yield return new WaitForSeconds(0.5f);

        currentState = BossState.Idle;
        agent.isStopped = false;

        ReportDebug("Necio Pecador completado", 1);
    }

    private GameObject SpawnNecioPecadorWarning(Vector3 position)
    {
        if (necioPecadorWarningPrefab != null)
        {
            GameObject warning = Instantiate(necioPecadorWarningPrefab);

            warning.transform.position = position;
            warning.transform.localScale = new Vector3(necioPecadorRadius * 2f, 0.01f, necioPecadorRadius * 2f);

            return warning;
        }
        else
        {
            ReportDebug("El prefab 'necioPecadorWarningPrefab' no está asignado.", 3);
            return null;
        }
    }

    private void SpawnSoulHand(Vector3 position)
    {
        if (soulHandPrefab != null)
        {
            GameObject hand = Instantiate(soulHandPrefab, position, Quaternion.identity);
            instantiatedEffects.Add(hand);

            SoulHand handScript = hand.GetComponent<SoulHand>();
            if (handScript != null)
            {
                handScript.Initialize(necioPecadorDamage, necioPecadorRadius);
            }
            else
            {
                ReportDebug("El prefab 'soulHandPrefab' no tiene el componente SoulHand adjunto.", 3);
                Destroy(hand);
            }

            if (audioSource != null && necioPecadorExplosionSound != null)
            {
                audioSource.PlayOneShot(necioPecadorExplosionSound);
            }
        }
    }

    #endregion

    #region Special Ability - Embestida del Fornido

    private IEnumerator ExecuteChargeAbility()
    {
        ReportDebug("Ejecutando: Embestida del Fornido", 1);

        currentState = BossState.Charging;
        agent.isStopped = true;
        isCharging = true;

        // FASE 1: Arrodillarse
        if (animator != null) animator.SetTrigger("KneelCharge");

        // Guardar espada
        yield return new WaitForSeconds(chargeAbilityKneelingTime - 1f);

        // FASE 2: Alzar mirada
        StartEyeGlow();

        yield return new WaitForSeconds(1f);

        // FASE 3: Embiste
        chargeDirection = (player.position - transform.position).normalized;
        chargeDirection.y = 0;
        transform.rotation = Quaternion.LookRotation(chargeDirection);

        if (animator != null) animator.SetTrigger("ChargeRun");
        if (audioSource != null && chargeAbilitySound != null) audioSource.PlayOneShot(chargeAbilitySound);

        if (vcam != null && noise != null) ShakeCamera(defaultDuration, defaultAmplitude, defaultFrequency);

        GameObject chargeTrail = null;
        if (darkWindTrailPrefab != null)
        {
            chargeTrail = Instantiate(darkWindTrailPrefab, transform.position, Quaternion.identity);
            instantiatedEffects.Add(chargeTrail);
            chargeTrail.transform.SetParent(transform);
            if (chargeTrail != null) Destroy(chargeTrail, 3f);
        }

        StopEyeGlow();

        // Movimiento de carga
        float chargeTime = 0f;
        float maxChargeTime = 3f;
        bool hitSomething = false;
        Vector3 startPos = transform.position;

        //agent.velocity = chargeDirection * chargeAbilitySpeed;

        while (chargeTime < maxChargeTime && !hitSomething)
        {
            agent.Move(chargeDirection * chargeAbilitySpeed * Time.deltaTime);

            // Detectar colisión con jugador
            Collider[] hits = Physics.OverlapSphere(transform.position + transform.forward * 3, 3f);
            foreach (Collider hit in hits)
            {
                if (hit.CompareTag("Player"))
                {
                    hitSomething = true;
                    PerformChargeHit(hit.gameObject);
                    ReportDebug("¡Embestida golpeó al jugador!", 1);
                    break;
                }
            }

            // Detectar colisión con paredes
            if (Physics.Raycast(transform.position, chargeDirection, 1f, LayerMask.GetMask("Wall")))
            {
                ReportDebug("Embestida chocó contra pared", 1);
                hitSomething = true;
                break;
            }

            chargeTime += Time.deltaTime;
            yield return null;
        }

        if (chargeTrail != null) Destroy(chargeTrail);

        agent.ResetPath();

        // FASE 4: Recuperación
        if (hitSomething && playerHealth != null && playerHealth.GetComponent<CharacterController>() != null)
        {
            // Si golpeó al jugador
            ReportDebug("Recuperación rápida tras golpear", 1);
            yield return new WaitForSeconds(0.5f);
        }
        else
        {
            // No golpeó nada, aturdirse
            if (animator != null) animator.SetTrigger("Stunned");

            SpawnStunVFX();

            if (audioSource != null && stunSound != null)
            {
                audioSource.PlayOneShot(stunSound);
            }

            yield return new WaitForSeconds(chargeAbilityRecoveryTime);
        }

        isCharging = false;
        currentState = BossState.Idle;
        agent.isStopped = false;

        ReportDebug("Embestida del Fornido completada", 1);
    }

    private void PerformChargeHit(GameObject target)
    {
        // Aplicar daño
        PlayerHealth targetHealth = target.GetComponent<PlayerHealth>();
        if (targetHealth != null)
        {
            targetHealth.TakeDamage(chargeAbilityDamage);
            ReportDebug($"Daño de embestida aplicado: {chargeAbilityDamage}", 1);
        }

        // Aplicar knockback gradual con detección de paredes
        CharacterController cc = target.GetComponent<CharacterController>();
        if (cc != null)
        {
            StartCoroutine(ApplyKnockbackToPlayer(cc, target.transform));
        }

        // Aturdir jugador (necesitarías implementar esto en PlayerHealth)
        // targetHealth.ApplyStun(chargeAbilityStunDuration);

        // Estela de viento
        SpawnWindTrail(target.transform.position);
    }

    private IEnumerator ApplyKnockbackToPlayer(CharacterController cc, Transform targetTransform)
    {
        if (cc == null || targetTransform == null)
        {
            ReportDebug("CharacterController o Transform nulo en knockback", 2);
            yield break;
        }

        Vector3 pushDir = (targetTransform.position - transform.position).normalized;
        pushDir.y = 0; // Solo empujar horizontalmente

        float totalForce = chargeAbilityKnockbackForce;
        float appliedForce = 0f;
        float pushSpeed = 20f; // Velocidad del empuje

        ReportDebug($"Iniciando knockback - Dirección: {pushDir}, Fuerza total: {totalForce}", 1);

        while (appliedForce < totalForce)
        {
            // Verificar que los componentes sigan existiendo
            if (cc == null || targetTransform == null)
            {
                ReportDebug("Knockback interrumpido - componente destruido", 2);
                break;
            }

            float forceThisFrame = pushSpeed * Time.deltaTime;
            Vector3 movement = pushDir * forceThisFrame;

            // VERIFICACIÓN 1: Raycast preventivo para detectar paredes adelante
            float checkDistance = forceThisFrame + cc.radius + 0.2f;
            if (Physics.Raycast(targetTransform.position, pushDir, checkDistance, LayerMask.GetMask("Wall", "Default")))
            {
                ReportDebug("Knockback detenido - pared detectada por Raycast", 1);
                break;
            }

            // VERIFICACIÓN 2: Aplicar movimiento y verificar colisión mediante CollisionFlags
            CollisionFlags flags = cc.Move(movement);

            // Si colisionó con algo lateral (pared), detener el empuje
            if ((flags & CollisionFlags.Sides) != 0)
            {
                ReportDebug("Knockback detenido - colisión lateral detectada", 1);
                break;
            }

            appliedForce += forceThisFrame;
            yield return null;
        }

        ReportDebug($"Knockback completado - Fuerza aplicada: {appliedForce}/{totalForce}", 1);
    }

    private void SpawnWindTrail(Vector3 position)
    {
        if (darkWindTrailPrefab != null)
        {
            GameObject trail = Instantiate(darkWindTrailPrefab, position, Quaternion.identity);
            Destroy(trail, 2f);
        }
    }

    private void SpawnStunVFX()
    {
        if (stunEffectPrefab != null)
        {
            GameObject stunEffect = Instantiate(stunEffectPrefab, transform.position + Vector3.up * 4f, Quaternion.identity, transform);
            instantiatedEffects.Add(stunEffect);

            stunEffect.transform.localScale = Vector3.one * 0.5f;

            StartCoroutine(DestroyStunEffect(stunEffect, chargeAbilityRecoveryTime));
        }
    }

    private IEnumerator DestroyStunEffect(GameObject effect, float duration)
    {
        yield return new WaitForSeconds(duration);
        if (effect != null)
        {
            Destroy(effect);
        }
    }

    public void ShakeCamera(float duration, float amplitude, float frequency, bool useUnscaledTime = false)
    {
        if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
        shakeCoroutine = StartCoroutine(ShakeRoutine(duration, amplitude, frequency, useUnscaledTime));
    }

    private IEnumerator ShakeRoutine(float duration, float amplitude, float frequency, bool useUnscaledTime)
    {
        // Guardar originales
        float ampBefore = originalAmplitude;
        float freqBefore = originalFrequency;

        // Aplicar requested
        noise.AmplitudeGain = amplitude;
        noise.FrequencyGain = frequency;

        float elapsed = 0f;
        float dt = 0f;

        // Si duration <= fadeOutTime, hacemos un único fade
        float localFade = Mathf.Min(fadeOutTime, duration * 0.5f);

        // Wait loop respetando unscaled o scaled time
        while (elapsed < duration)
        {
            dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            elapsed += dt;

            // Si estamos en la fase final, atenuar la amplitude suavemente
            if (elapsed > duration - localFade)
            {
                float t = (elapsed - (duration - localFade)) / localFade; // 0..1
                float currentAmp = Mathf.Lerp(amplitude, ampBefore, t); // vuelve hacia el valor previo (normalmente 0)
                noise.AmplitudeGain = currentAmp;

                // opcional: también atenuar frecuencia si querés (aquí lo dejamos sin cambios o podrías usar same lerp)
                // noise.m_FrequencyGain = Mathf.Lerp(frequency, freqBefore, t);
            }

            yield return null;
        }

        noise.AmplitudeGain = originalAmplitude;
        noise.FrequencyGain = originalFrequency;

        shakeCoroutine = null;
    }

    #endregion

    #region VFX Methods

    private void StartArmorGlow()
    {
        if (armorGlowVFX != null)
        {
            armorGlowVFX.Play();
        }

        // Emisión verde en la armadura
        ApplyArmorEmission(Color.green * 2f);
    }

    private void StopArmorGlow()
    {
        if (armorGlowVFX != null)
        {
            armorGlowVFX.Stop();
        }

        // Restaurar emisión original
        ApplyArmorEmission(originalEmissionColor);
    }

    private void ApplyArmorEmission(Color emissionColor)
    {
        if (armorRenderers == null || armorPropertyBlock == null) return;

        foreach (MeshRenderer renderer in armorRenderers)
        {
            if (renderer == null) continue;

            renderer.GetPropertyBlock(armorPropertyBlock);
            armorPropertyBlock.SetColor("_EmissionColor", emissionColor);
            renderer.SetPropertyBlock(armorPropertyBlock);

            Material[] materials = renderer.sharedMaterials;
            foreach (Material mat in materials)
            {
                if (mat != null && mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                }
            }
        }
    }

    private void StartEyeGlow()
    {
        ReportDebug("Ojos brillando en rojo", 1);
    }

    private void StopEyeGlow()
    {
        ReportDebug("Ojos dejaron de brillar", 1);
    }

    #endregion

    #region Counter-Attack System

    public void OnPlayerCounterAttack()
    {
        //if (!isVulnerableToCounter)
        //{
        //    ReportDebug("No vulnerable a contraataque en este momento", 1);
        //    return;
        //}

        //if (stunCoroutine != null)
        //{
        //    ReportDebug("Ya está aturdido", 1);
        //    return;
        //}

        //stunCoroutine = StartCoroutine(GetStunned());
    }

    private IEnumerator GetStunned()
    {
        ReportDebug("¡Dark Knight aturdido por contraataque!", 1);
        
        SpawnStunVFX();

        currentState = BossState.Stunned;
        isVulnerableToCounter = false;

        if (currentAttackCoroutine != null)
        {
            StopCoroutine(currentAttackCoroutine);
            currentAttackCoroutine = null;
        }

        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        StopArmorGlow();

        if (animator != null)
        {
            animator.SetTrigger("Stunned");
            animator.ResetTrigger("DashSlash");
            animator.ResetTrigger("StartApocalipsis");
            animator.ResetTrigger("SummonHand");
            animator.ResetTrigger("KneelCharge");
            animator.ResetTrigger("ChargeRun");
            animator.SetBool("IsMoving", false);
        }

        if (audioSource != null && stunSound != null)
        {
            audioSource.PlayOneShot(stunSound);
        }

        // Esperar duración del stun
        yield return new WaitForSeconds(sodomaStunDuration);

        ReportDebug("Iniciando recuperación del stun...", 1);

        currentState = BossState.Idle;

        if (animator != null)
        {
            animator.ResetTrigger("Stunned");
            animator.SetBool("IsMoving", false);
            animator.Play("Idle", 0, 0f);
        }

        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.isStopped = false;

            ReportDebug($"Agent reactivado - isStopped: {agent.isStopped}, enabled: {agent.enabled}", 1);
        }

        lastSodomaTime = Time.time;
        //stunCoroutine = null;
        ResynchronizeAgent();

        ReportDebug("Dark Knight recuperado del aturdimiento", 1);
    }

    #endregion

    #region Utility Coroutines & Counter-Attack

    private IEnumerator MoveToPosition(Vector3 target, float duration)
    {
        // agent.enabled = false; 

        float time = 0;
        Vector3 startPosition = transform.position;

        while (time < duration)
        {
            Vector3 currentPos = Vector3.Lerp(startPosition, target, time / duration);
            Vector3 movement = currentPos - transform.position;

            agent.Move(movement);

            time += Time.deltaTime;
            yield return null;
        }

        agent.Move(target - transform.position);

        ResynchronizeAgent();

        yield return null;
    }

    private void ResynchronizeAgent()
    {
        Vector3 samplePosition = transform.position - new Vector3(0, agent.baseOffset, 0);

        if (NavMesh.SamplePosition(samplePosition, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
        {
            if (!agent.enabled) agent.enabled = true;

            agent.Warp(hit.position);
            agent.isStopped = false;
            agent.ResetPath();

            ReportDebug($"Agente resincronizado en NavMesh en la posición {hit.position}", 1);
        }
        else
        {
            ReportDebug($"FALLO CRÍTICO: No se encontró NavMesh cerca de {transform.position} para resincronizar.", 3);
        }
    }

    #endregion

    #region Debug

    private void OnDrawGizmos()
    {
        // Sodoma y Gomorra range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, sodomaCutRange);

        // Apocalipsis range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, apocalipsisRange);

        // Apocalipsis attack area (semicírculo frontal)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + transform.forward * (apocalipsisRange / 2f), apocalipsisRange / 1.5f);

        // Necio Pecador radius
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, necioPecadorRadius);

        // área de detección de embestida
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + transform.forward * 1.5f, 3f);

        // Forward direction
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, transform.forward * 5f);

        // Charge direction
        if (isCharging)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(transform.position, chargeDirection * 10f);
        }
    }

    private void OnGUI()
    {
        if (!showDebugGUI) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 400));
        GUILayout.Box("Dark Knight Debug");

        GUILayout.Label($"Estado: {currentState}");
        GUILayout.Label($"Vida: {currentHealth:F0}/{maxHealth:F0}");
        GUILayout.Label($"Fase Baja HP: {isInLowHealthPhase}");
        GUILayout.Label($"Vulnerable: {isVulnerableToCounter}");
        GUILayout.Label($"Cargando: {isCharging}");

        if (player != null)
        {
            float dist = Vector3.Distance(transform.position, player.position);
            GUILayout.Label($"Distancia: {dist:F2}");
        }

        GUILayout.Label($"Cooldowns:");
        GUILayout.Label($"- Sodoma: {Mathf.Max(0, sodomaCooldown - (Time.time - lastSodomaTime)):F1}s");
        GUILayout.Label($"- Necio: {Mathf.Max(0, necioPecadorCooldown - (Time.time - lastNecioPecadorTime)):F1}s");

        GUILayout.EndArea();
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