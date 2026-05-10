using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Jefe 2 - BloodKnight / The Ones Who Shattered.
/// </summary>
public class BloodKnightBoss : MonoBehaviour
{
    #region Enums

    private enum BossState
    {
        Idle,
        Patrol,
        Chase,
        Review,
        StaticFailureWindup,
        StaticFailureRelease,
        BrokenCharge,
        DivisoryFailure,
        ScrapRam,
        DrownedHands,
        Stunned,
        PhaseTransition,
        Dead
    }

    #endregion

    #region Inspector - References

    [Header("Referencias")]
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform player;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform attackOrigin;
    [SerializeField] private Transform mineDropPoint;
    [SerializeField] private CinemachineCamera vcam;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioSource ambientAudioSource;

    #endregion

    #region Inspector - Stats Base

    [Header("Stats base")]
    [SerializeField] private float maxHealth = 500f;
    [SerializeField] private float baseMoveSpeed = 7f;
    [SerializeField] private float aggroRange = 20f;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private LayerMask obstacleLayers;

    #endregion

    #region Inspector - Falla Estatica

    [Header("Falla Estatica")]
    [SerializeField] private float staticFailureDamage = 35f;
    [SerializeField] private float staticFailureWindup = 2f;
    [SerializeField] private float staticFailureCooldown = 27f;
    [SerializeField] private float staticFailureAoERadius = 4.5f;
    [SerializeField, Range(1f, 180f)] private float shieldAngle = 150f;
    [SerializeField] private int backHitsToInterrupt = 5;
    [SerializeField] private float interruptStunDuration = 1.5f;

    #endregion

    #region Inspector - Carga de los Quebrados

    [Header("Carga de los Quebrados")]
    [SerializeField] private float brokenChargeDuration = 10f;
    [SerializeField] private int brokenChargeDashCount = 10;
    [SerializeField] private float brokenChargeDashSpeed = 13.5f;
    [SerializeField] private float brokenChargeHitDamage = 7f;
    [SerializeField] private float brokenChargeCooldown = 19f;
    [SerializeField] private float zigzagSideOffset = 3.5f;
    [SerializeField] private float dashHitRadius = 1.35f;

    #endregion

    #region Inspector - Embestida de Chatarra

    [Header("Embestida de Chatarra")]
    [SerializeField] private float scrapRamSpeed = 14f;
    [SerializeField] private float scrapRamMaxTurnRate = 20f;
    [SerializeField] private float scrapRamDuration = 2.2f;
    [SerializeField] private float scrapRamBossStun = 2f;
    [SerializeField] private float scrapRamPlayerStun = 3f;
    [SerializeField] private int scrapRamMineBurst = 5;
    [SerializeField] private float scrapRamActiveDist = 15f;
    [SerializeField] private float scrapRamNoDamageWindow = 10f;

    #endregion

    #region Inspector - Falla Divisoria

    [Header("Falla Divisoria")]
    [SerializeField] private float divisoryTriggerDist = 15f;
    [SerializeField] private float divisoryWallDuration = 5f;
    [SerializeField] private float divisoryContactDmg = 2.5f;
    [SerializeField] private float divisoryInstantDmg = 12.5f;
    [SerializeField] private float divisoryCastTime = 0.8f;
    [SerializeField] private Vector3 divisoryWallScale = new Vector3(1.2f, 3.2f, 18f);

    #endregion

    #region Inspector - Manos de los Ahogados

    [Header("Manos de los Ahogados")]
    [SerializeField] private float drownedDamage = 15f;
    [SerializeField] private float drownedRange = 15f;
    [SerializeField] private int drownedHitCount = 3;
    [SerializeField] private float drownedInterval = 0.9f;
    [SerializeField] private float drownedCooldown = 14f;
    [SerializeField, Range(0f, 1f)] private float predictiveChance = 0.75f;
    [SerializeField] private int predictiveDashThreshold = 15;
    [SerializeField] private float predictiveLeadDist = 2.75f;

    #endregion

    #region Inspector - Colapso del Cuerpo

    [Header("Colapso del Cuerpo")]
    [SerializeField, Range(0.01f, 1f)] private float collapseThreshold = 0.25f;
    [SerializeField] private float collapseSpeedMult = 1.30f;
    [SerializeField] private float scrapAuraRadius = 3f;
    [SerializeField] private float scrapAuraDps = 0.5f;
    [SerializeField] private float phaseTransitionTime = 1.0f;

    #endregion

    #region Inspector - Prefabs

    [Header("Prefabs")]
    [SerializeField] private BossExplosiveMine bossMinePrefab;
    [SerializeField] private CrystalTrail crystalTrailPrefab;
    [SerializeField] private SolidLightWall solidLightWallPrefab;
    [SerializeField] private SoulHand soulHandPrefab;
    [SerializeField] private GameObject staticFailureWarningPrefab;
    [SerializeField] private GameObject frontBlockVFXPrefab;
    [SerializeField] private GameObject impactStunVFXPrefab;
    [SerializeField] private GameObject phaseCollapseVFXPrefab;
    [SerializeField] private GameObject scrapRainVFXPrefab;
    [SerializeField] private GameObject scrapAuraVFXPrefab;

    #endregion

    #region Inspector - Audio

    [Header("Audio")]
    [SerializeField] private AudioClip staticChargeSFX;
    [SerializeField] private AudioClip staticImpactSFX;
    [SerializeField] private AudioClip shieldBlockSFX;
    [SerializeField] private AudioClip dashSFX;
    [SerializeField] private AudioClip divisorySFX;
    [SerializeField] private AudioClip drownedHandsSFX;
    [SerializeField] private AudioClip scrapRamSFX;
    [SerializeField] private AudioClip collapseSFX;
    [SerializeField] private AudioClip stunSFX;
    [SerializeField] private AudioClip deathSFX;

    #endregion

    #region Inspector - Debug

    [Header("Debug")]
    [SerializeField] private bool showDebugGUI = false;

    #endregion

    #region Internal State

    private static readonly int AnimWalking = Animator.StringToHash("Walking");
    private static readonly int AnimSwordStack = Animator.StringToHash("SwordStack");
    private static readonly int AnimAttackDash = Animator.StringToHash("AttackDash");
    private static readonly int AnimAttackHand = Animator.StringToHash("AttackHand");
    private static readonly int AnimAttackEnded = Animator.StringToHash("AttackEnded");
    private static readonly int AnimDeath = Animator.StringToHash("Death");

    private BossState state = BossState.Idle;
    private float currentHealth;
    private bool isDead;

    private float nextStaticTime;
    private float nextBrokenChargeTime;
    private float nextDrownedTime;

    private bool interruptionActive;
    private bool phaseCollapsed;

    private bool staticShieldActive;
    private int rearHitsDuringWindup;

    private float lastDamageInRangeTime;

    private int playerDashCount;

    private Vector3 lastPlayerPos;
    private Vector3 playerVelocity;

    private readonly List<GameObject> spawnedObjects = new();

    private PlayerHealth playerHealth;
    private PlayerMovement playerMovement;
    private PlayerStatsManager playerStats;

    private CinemachineBasicMultiChannelPerlin camNoise;

    private int groundLayerMask;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        CacheComponents();
        groundLayerMask = LayerMask.GetMask("Ground");
    }

    private void Start()
    {
        currentHealth = maxHealth;
        lastDamageInRangeTime = Time.time;
        lastPlayerPos = player != null ? player.position : transform.position;

        nextStaticTime = Time.time + 2f;
        nextBrokenChargeTime = Time.time + 6f;
        nextDrownedTime = Time.time + 4f;

        StartCoroutine(MainBrainRoutine());
    }

    private void OnEnable()
    {
        if (enemyHealth == null) return;
        enemyHealth.OnHealthChanged += HandleHealthChanged;
        enemyHealth.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        if (enemyHealth == null) return;
        enemyHealth.OnHealthChanged -= HandleHealthChanged;
        enemyHealth.OnDeath -= HandleDeath;
    }

    private void Update()
    {
        if (isDead || player == null) return;

        TrackPlayerVelocity();
        TickScrapAura();
        CheckDynamicInterruptions();
    }

    #endregion

    #region Initialization & Data Sync

    private void CacheComponents()
    {
        if (enemyHealth == null) enemyHealth = GetComponent<EnemyHealth>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>();

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
                playerHealth = playerObj.GetComponent<PlayerHealth>();
                playerMovement = playerObj.GetComponent<PlayerMovement>();
                playerStats = playerObj.GetComponent<PlayerStatsManager>();
            }
        }

        if (vcam == null) vcam = Object.FindFirstObjectByType<CinemachineCamera>();
        if (vcam != null)
            camNoise = vcam.GetCinemachineComponent(CinemachineCore.Stage.Noise) as CinemachineBasicMultiChannelPerlin;

        if (enemyHealth != null) enemyHealth.SetMaxHealth(maxHealth);

        if (agent != null)
        {
            agent.speed = baseMoveSpeed;
            agent.angularSpeed = 720f;
            agent.stoppingDistance = 1.75f;
        }

        if (attackOrigin == null) attackOrigin = transform;
        if (mineDropPoint == null) mineDropPoint = transform;
    }

    #endregion

    #region Core Health & Events

    private void HandleHealthChanged(float current, float max)
    {
        currentHealth = current;
        maxHealth = max;

        if (player != null && Vector3.Distance(transform.position, player.position) <= scrapRamActiveDist)
            lastDamageInRangeTime = Time.time;

        if (!phaseCollapsed && currentHealth <= maxHealth * collapseThreshold)
            StartCoroutine(EnterCollapsePhaseRoutine());
    }

    private void HandleDeath(GameObject go)
    {
        if (go != gameObject || isDead) return;
        isDead = true;
        state = BossState.Dead;

        StopAllCoroutines();
        CleanupSpawnedObjects();

        if (agent != null) { agent.isStopped = true; agent.enabled = false; }

        PlaySFX(deathSFX);
        animator?.SetTrigger(AnimDeath);
    }

    #endregion

    #region Boss Brain & Behaviours

    private IEnumerator MainBrainRoutine()
    {
        yield return new WaitForSeconds(0.5f);

        while (!isDead)
        {
            while (interruptionActive)
                yield return null;

            if (!PlayerInAggroRange())
            {
                state = BossState.Patrol;
                SetWalking(false);
                yield return null;
                continue;
            }

            state = BossState.Review;

            if (Time.time >= nextStaticTime)
            {
                yield return ExecuteStaticFailureRoutine();
                if (isDead || interruptionActive) continue;

                yield return ExecuteBrokenChargeRoutine();
                continue;
            }

            yield return ChaseAndPokeRoutine();
        }
    }

    private IEnumerator ChaseAndPokeRoutine()
    {
        state = BossState.Chase;

        float window = 1.5f;
        float elapsed = 0f;

        while (elapsed < window && !isDead && !interruptionActive)
        {
            if (player == null) yield break;

            float dist = Vector3.Distance(transform.position, player.position);
            MoveTowards(player.position, baseMoveSpeed * 0.7f);
            FacePlayer(10f);

            if (dist <= drownedRange && Time.time >= nextDrownedTime)
            {
                StopAgent();
                yield return ExecuteDrownedHandsRoutine(usePredictive: false);
                nextDrownedTime = Time.time + drownedCooldown;
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        StopAgent();
    }

    private void CheckDynamicInterruptions()
    {
        if (isDead || interruptionActive || player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);

        if (dist > divisoryTriggerDist)
        {
            StartCoroutine(ExecuteDivisoryFailureRoutine());
            return;
        }

        if (dist <= scrapRamActiveDist && (Time.time - lastDamageInRangeTime) >= scrapRamNoDamageWindow)
        {
            lastDamageInRangeTime = Time.time;
            StartCoroutine(ExecuteScrapRamRoutine());
            return;
        }

        if (playerDashCount >= predictiveDashThreshold && dist <= drownedRange && Time.time >= nextDrownedTime && state == BossState.Chase)
        {
            state = BossState.DrownedHands;
            nextDrownedTime = Time.time + drownedCooldown;
            StartCoroutine(ExecuteDrownedHandsRoutine(usePredictive: true));
        }
    }

    #endregion

    #region Attack - Falla Estatica

    private IEnumerator ExecuteStaticFailureRoutine()
    {
        state = BossState.StaticFailureWindup;
        rearHitsDuringWindup = 0;
        staticShieldActive = true;
        nextStaticTime = Time.time + staticFailureCooldown;

        StopAgent();
        FacePlayerInstant();
        SetWalking(false);

        animator?.ResetTrigger(AnimAttackEnded);
        animator?.SetTrigger(AnimSwordStack);
        PlaySFX(staticChargeSFX);

        GameObject warning = SpawnWarningIndicator(
            transform.position + transform.forward * 1.5f,
            staticFailureAoERadius * 2f,
            staticFailureWarningPrefab
        );

        float elapsed = 0f;
        while (elapsed < staticFailureWindup)
        {
            elapsed += Time.deltaTime;
            yield return null;

            if (state != BossState.StaticFailureWindup)
            {
                if (warning != null) Destroy(warning);
                yield break;
            }
        }

        if (warning != null) Destroy(warning);

        staticShieldActive = false;
        state = BossState.StaticFailureRelease;

        DealAoEDamage(transform.position + transform.forward * 1.5f, staticFailureAoERadius, staticFailureDamage);
        PlaySFX(staticImpactSFX);
        ShakeCamera(2f, 0.25f);

        animator?.SetBool(AnimAttackEnded, true);
        yield return new WaitForSeconds(0.4f);
    }

    public void NotifyBossDamaged(Vector3 attackerWorldPos)
    {
        if (isDead) return;

        if (state == BossState.StaticFailureWindup)
        {
            if (IsHitFromBack(attackerWorldPos))
            {
                rearHitsDuringWindup++;
                if (rearHitsDuringWindup >= backHitsToInterrupt)
                {
                    StartCoroutine(InterruptStaticFailureRoutine());
                }
            }
            else
            {
                TriggerFrontShieldBlock(attackerWorldPos);
            }
        }
    }

    public bool IsAttackBlockedByShield(Vector3 attackerWorldPos)
    {
        if (!staticShieldActive) return false;
        float angle = Vector3.Angle(transform.forward, (attackerWorldPos - transform.position).normalized);
        return angle <= shieldAngle * 0.5f;
    }

    private IEnumerator InterruptStaticFailureRoutine()
    {
        if (state != BossState.StaticFailureWindup || interruptionActive) yield break;

        interruptionActive = true;
        staticShieldActive = false;
        state = BossState.Stunned;

        StopAgent();
        SetWalking(false);
        PlaySFX(stunSFX);
        SpawnImpactStunVFX(transform.position);
        animator?.SetTrigger(AnimAttackEnded);

        yield return new WaitForSeconds(interruptStunDuration);

        interruptionActive = false;
        state = BossState.Idle;
    }

    private void TriggerFrontShieldBlock(Vector3 attackerPos)
    {
        PlaySFX(shieldBlockSFX);
        if (frontBlockVFXPrefab == null) return;

        Vector3 spawnPos = transform.position + (attackerPos - transform.position).normalized * 1.2f + Vector3.up;
        var vfx = Instantiate(frontBlockVFXPrefab, spawnPos, Quaternion.identity);
        spawnedObjects.Add(vfx);
        Destroy(vfx, 1.5f);
    }

    private bool IsHitFromBack(Vector3 attackerPos)
    {
        Vector3 dir = (attackerPos - transform.position).normalized;
        return Vector3.Dot(transform.forward, dir) < -0.15f;
    }

    #endregion

    #region Attack - Carga de los Quebrados

    private IEnumerator ExecuteBrokenChargeRoutine()
    {
        state = BossState.BrokenCharge;
        nextBrokenChargeTime = Time.time + brokenChargeCooldown;

        float timePerDash = brokenChargeDuration / Mathf.Max(1, brokenChargeDashCount);

        for (int i = 0; i < brokenChargeDashCount; i++)
        {
            if (isDead || interruptionActive) yield break;

            FacePlayerInstant();

            SpawnBossMine(mineDropPoint.position);

            Vector3 target = ComputeZigzagTarget(i);
            animator?.SetTrigger(AnimAttackDash);
            PlaySFX(dashSFX);

            yield return DashRoutine(target, timePerDash * 0.65f, brokenChargeDashSpeed,
                                     dealContactDamage: true, spawnCrystalOnWallHit: true);

            float rest = timePerDash * 0.35f;
            yield return new WaitForSeconds(rest);
        }

        StopAgent();
        SetWalking(false);
        state = BossState.Idle;
    }

    private Vector3 ComputeZigzagTarget(int dashIndex)
    {
        if (player == null) return transform.position;

        Vector3 toPlayer = (player.position - transform.position).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, toPlayer).normalized;
        float sign = (dashIndex % 2 == 0) ? 1f : -1f;
        Vector3 target = player.position + side * zigzagSideOffset * sign;
        target.y = transform.position.y;

        return NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas)
            ? hit.position
            : target;
    }

    #endregion

    #region Attack - Embestida de Chatarra

    private IEnumerator ExecuteScrapRamRoutine()
    {
        if (interruptionActive || isDead) yield break;

        interruptionActive = true;
        state = BossState.ScrapRam;

        StopAgent();
        FacePlayerInstant();
        PlaySFX(scrapRamSFX);

        float elapsed = 0f;
        bool hitPlayer = false;
        bool hitWall = false;
        Vector3 wallHitPos = Vector3.zero;

        while (elapsed < scrapRamDuration)
        {
            if (player != null)
            {
                Vector3 desiredDir = (player.position - transform.position).normalized;
                desiredDir.y = 0f;
                if (desiredDir.sqrMagnitude > 0.001f)
                {
                    Quaternion desired = Quaternion.LookRotation(desiredDir);
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation, desired, scrapRamMaxTurnRate * Time.deltaTime);
                }
            }

            transform.position += transform.forward * scrapRamSpeed * Time.deltaTime;

            Collider[] playerHits = Physics.OverlapSphere(
                transform.position + transform.forward * 0.8f, 1.2f, playerLayer);
            foreach (var c in playerHits)
            {
                if (!c.CompareTag("Player")) continue;
                hitPlayer = true;
                break;
            }
            if (hitPlayer) break;

            if (Physics.SphereCast(transform.position + Vector3.up * 0.75f,
                                   0.6f, transform.forward, out RaycastHit wallHit, 0.9f, obstacleLayers))
            {
                hitWall = true;
                wallHitPos = wallHit.point;
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (hitPlayer)
        {
            playerHealth?.ApplyStun(scrapRamPlayerStun);
        }
        else if (hitWall)
        {
            SpawnImpactStunVFX(wallHitPos);
            PlaySFX(stunSFX);

            for (int i = 0; i < scrapRamMineBurst; i++)
            {
                Vector2 rnd = Random.insideUnitCircle * 2f;
                SpawnBossMine(transform.position + new Vector3(rnd.x, 0f, rnd.y));
            }

            yield return new WaitForSeconds(scrapRamBossStun);
        }
        else
        {
            yield return new WaitForSeconds(0.25f);
        }

        interruptionActive = false;
        state = BossState.Idle;
    }

    #endregion

    #region Attack - Falla Divisoria

    private IEnumerator ExecuteDivisoryFailureRoutine()
    {
        if (interruptionActive || isDead) yield break;

        interruptionActive = true;
        state = BossState.DivisoryFailure;

        StopAgent();
        FacePlayerInstant();
        PlaySFX(divisorySFX);

        yield return new WaitForSeconds(divisoryCastTime);

        if (solidLightWallPrefab != null)
        {
            Vector3 wallPos = ComputeDivisoryWallPosition();
            Quaternion wallRot = player != null
                ? Quaternion.LookRotation(Vector3.Cross(Vector3.up, (player.position - transform.position).normalized))
                : Quaternion.identity;

            SolidLightWall wall = Instantiate(solidLightWallPrefab, wallPos, wallRot);
            wall.transform.localScale = divisoryWallScale;
            wall.ContactDamagePerSecond = divisoryContactDmg;
            wall.InstantDamageOnSpawn = divisoryInstantDmg;
            wall.WallLifetime = divisoryWallDuration;
            spawnedObjects.Add(wall.gameObject);
        }

        yield return new WaitForSeconds(0.2f);
        interruptionActive = false;
        state = BossState.Idle;
    }

    private Vector3 ComputeDivisoryWallPosition()
    {
        Vector3 mid = (transform.position + (player != null ? player.position : transform.position)) * 0.5f;
        mid.y = transform.position.y + divisoryWallScale.y * 0.5f;

        if (Physics.Raycast(mid + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 15f, groundLayerMask))
        {
            mid.y = hit.point.y + divisoryWallScale.y * 0.5f;
        }

        return mid;
    }

    #endregion

    #region Attack - Manos de los Ahogados

    private IEnumerator ExecuteDrownedHandsRoutine(bool usePredictive)
    {
        state = BossState.DrownedHands;

        animator?.SetTrigger(AnimAttackHand);
        PlaySFX(drownedHandsSFX);

        for (int i = 0; i < drownedHitCount; i++)
        {
            if (isDead) yield break;

            Vector3 target = ComputeHandsTarget(usePredictive);
            SpawnSoulHand(target);

            yield return new WaitForSeconds(drownedInterval);
        }

        state = BossState.Idle;
    }

    private Vector3 ComputeHandsTarget(bool usePredictive)
    {
        if (player == null) return transform.position;

        Vector3 target = player.position;

        if (usePredictive && playerVelocity.sqrMagnitude > 0.01f && Random.value <= predictiveChance)
        {
            target += playerVelocity.normalized * predictiveLeadDist;
        }

        return NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas)
            ? hit.position
            : target;
    }

    public void RegisterPlayerDash()
    {
        playerDashCount++;
    }

    #endregion

    #region Phase Transition - Colapso del Cuerpo

    private IEnumerator EnterCollapsePhaseRoutine()
    {
        if (phaseCollapsed || isDead) yield break;

        phaseCollapsed = true;
        interruptionActive = true;
        state = BossState.PhaseTransition;

        StopAgent();
        SetWalking(false);
        PlaySFX(collapseSFX);

        if (phaseCollapseVFXPrefab != null)
        {
            var vfx = Instantiate(phaseCollapseVFXPrefab, transform.position, Quaternion.identity, transform);
            spawnedObjects.Add(vfx);
        }

        yield return new WaitForSeconds(phaseTransitionTime);

        if (agent != null) agent.speed = baseMoveSpeed * collapseSpeedMult;

        if (scrapRainVFXPrefab != null)
        {
            var rain = Instantiate(scrapRainVFXPrefab, transform.position, Quaternion.identity, transform);
            spawnedObjects.Add(rain);
        }

        if (scrapAuraVFXPrefab != null)
        {
            var aura = Instantiate(scrapAuraVFXPrefab, transform.position, Quaternion.identity, transform);
            spawnedObjects.Add(aura);
        }

        interruptionActive = false;
        state = BossState.Idle;
    }

    private void TickScrapAura()
    {
        if (!phaseCollapsed || player == null || playerHealth == null) return;

        if (Vector3.Distance(transform.position, player.position) > scrapAuraRadius) return;

        playerHealth.TakeDamage(scrapAuraDps * Time.deltaTime);
    }

    #endregion

    #region Movement & Tracking Utils

    private IEnumerator DashRoutine(Vector3 target, float duration, float speed, bool dealContactDamage, bool spawnCrystalOnWallHit)
    {
        float elapsed = 0f;

        while (elapsed < duration && !isDead)
        {
            Vector3 dir = (target - transform.position);
            dir.y = 0f;

            if (dir.sqrMagnitude < 0.01f) break;

            dir.Normalize();
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            if (Physics.SphereCast(transform.position + Vector3.up * 0.5f, 
                0.45f, dir, out RaycastHit wallHit, 0.8f, obstacleLayers))
            {
                if (spawnCrystalOnWallHit)
                    SpawnCrystalTrail(wallHit.point + wallHit.normal * 0.1f, wallHit.normal);
                break;
            }

            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

            if (dealContactDamage)
                DealContactDamageDuringDash();

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void DealContactDamageDuringDash()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position + transform.forward * 0.75f, dashHitRadius, playerLayer);
        foreach (var c in hits)
        {
            if (!c.CompareTag("Player")) continue;
            c.GetComponent<PlayerHealth>()?.TakeDamage(brokenChargeHitDamage);
            break;
        }
    }

    private void MoveTowards(Vector3 destination, float speed)
    {
        if (agent == null || !agent.enabled) return;
        agent.speed = speed;
        agent.isStopped = false;
        agent.SetDestination(destination);
        SetWalking(agent.velocity.sqrMagnitude > 0.01f);
    }

    private void StopAgent()
    {
        if (agent == null || !agent.enabled) return;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
    }

    private void FacePlayer(float lerpSpeed = 10f)
    {
        if (player == null) return;
        Vector3 dir = player.position - transform.position; dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.Slerp(
            transform.rotation, Quaternion.LookRotation(dir.normalized), Time.deltaTime * lerpSpeed);
    }

    private void FacePlayerInstant()
    {
        if (player == null) return;
        Vector3 dir = player.position - transform.position; dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    private bool PlayerInAggroRange()
    {
        return player != null && Vector3.Distance(transform.position, player.position) <= aggroRange;
    }

    private void TrackPlayerVelocity()
    {
        if (player == null) return;
        playerVelocity = (player.position - lastPlayerPos) / Mathf.Max(Time.deltaTime, 0.0001f);
        lastPlayerPos = player.position;
    }

    #endregion

    #region Damage Utils

    private void DealAoEDamage(Vector3 center, float radius, float dmg)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius, playerLayer);
        foreach (var c in hits)
        {
            if (!c.CompareTag("Player")) continue;
            c.GetComponent<PlayerHealth>()?.TakeDamage(dmg);
            return;
        }
    }

    #endregion

    #region Spawners & VFX

    private void SpawnBossMine(Vector3 pos)
    {
        if (bossMinePrefab == null) return;
        var mine = Instantiate(bossMinePrefab, pos, Quaternion.identity);
        spawnedObjects.Add(mine.gameObject);
    }

    private void SpawnCrystalTrail(Vector3 pos, Vector3 normal)
    {
        if (crystalTrailPrefab == null) return;
        var trail = Instantiate(crystalTrailPrefab, pos,
            Quaternion.LookRotation(Vector3.Cross(normal, Vector3.up)));
        spawnedObjects.Add(trail.gameObject);
    }

    private void SpawnSoulHand(Vector3 pos)
    {
        if (soulHandPrefab == null) return;
        var hand = Instantiate(soulHandPrefab, pos, Quaternion.identity);
        hand.Initialize(drownedDamage, 1.5f);
        spawnedObjects.Add(hand.gameObject);
    }

    private GameObject SpawnWarningIndicator(Vector3 pos, float scale, GameObject prefab)
    {
        if (prefab == null) return null;
        var obj = Instantiate(prefab, pos, Quaternion.identity);
        obj.transform.localScale = Vector3.one * scale;
        spawnedObjects.Add(obj);
        return obj;
    }

    private void SpawnImpactStunVFX(Vector3 pos)
    {
        if (impactStunVFXPrefab == null) return;
        var vfx = Instantiate(impactStunVFXPrefab, pos, Quaternion.identity);
        spawnedObjects.Add(vfx);
        Destroy(vfx, 2f);
    }

    #endregion

    #region Audio & Camera Utils

    private void SetWalking(bool isWalking)
    {
        animator?.SetBool(AnimWalking, isWalking);
    }

    private void PlaySFX(AudioClip clip, float vol = 1f)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip, vol);
    }

    private void ShakeCamera(float amplitude, float duration)
    {
        if (camNoise == null) return;
        StartCoroutine(CameraShakeRoutine(amplitude, duration));
    }

    private IEnumerator CameraShakeRoutine(float amplitude, float duration)
    {
        float original = camNoise.AmplitudeGain;
        camNoise.AmplitudeGain = amplitude;
        yield return new WaitForSeconds(duration);
        camNoise.AmplitudeGain = original;
    }

    #endregion

    #region Cleanup

    private void CleanupSpawnedObjects()
    {
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            if (spawnedObjects[i] != null) Destroy(spawnedObjects[i]);
        }
        spawnedObjects.Clear();
    }

    #endregion

    #region Logging

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, aggroRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, divisoryTriggerDist);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, drownedRange);

        Gizmos.color = new Color(1f, 0.3f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, scrapRamActiveDist);

        Gizmos.color = new Color(1f, 0f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, scrapAuraRadius);

        Vector3 leftEdge = Quaternion.Euler(0f, -shieldAngle * 0.5f, 0f) * transform.forward;
        Vector3 rightEdge = Quaternion.Euler(0f, shieldAngle * 0.5f, 0f) * transform.forward;
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position + Vector3.up, leftEdge * 3f);
        Gizmos.DrawRay(transform.position + Vector3.up, rightEdge * 3f);
    }

    private void OnGUI()
    {
        if (!showDebugGUI || !Application.isPlaying) return;
        GUILayout.BeginArea(new Rect(10, 10, 220, 120));
        GUILayout.Label($"State: {state}");
        GUILayout.Label($"HP: {currentHealth:F0} / {maxHealth:F0}");
        GUILayout.Label($"Shield active: {staticShieldActive}");
        GUILayout.Label($"Rear hits: {rearHitsDuringWindup}/{backHitsToInterrupt}");
        GUILayout.Label($"Player dashes: {playerDashCount}/{predictiveDashThreshold}");
        GUILayout.Label($"Phase collapsed: {phaseCollapsed}");
        GUILayout.EndArea();
    }

    #endregion
}