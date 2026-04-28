using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Clase base abstracta para todas las variantes del enemigo Static.
/// </summary>
public abstract class StaticEnemyBase : MonoBehaviour
{
    #region Enums & Structs

    public enum MorlockLevel { Nivel1, Nivel2, Nivel3 }
    protected enum MorlockState { Patrol, Pursue1, Pursue2, Pursue3, Repositioning }

    #endregion

    #region Inspector - References

    [Header("Referencias")]
    [SerializeField] protected StaticAnimCtrl visualCtrl;
    [SerializeField] protected MorlockStats stats;
    [SerializeField] protected GameObject projectilePrefab;
    [SerializeField] protected Transform firePoint;

    #endregion

    #region Inspector - Difficulty Settings

    [Header("Dificultad de anticipación por disparo")]
    [SerializeField] protected MorlockLevel currentLevel = MorlockLevel.Nivel1;
    [SerializeField] protected float interceptProbabilityNivel1 = 0.25f;
    [SerializeField] protected float interceptProbabilityNivel2 = 0.5f;
    [SerializeField] protected float interceptProbabilityNivel3 = 0.75f;
    #endregion

    #region Inspector - Base Stats Settings

    [Header("Estadisticas (fallback si no hay MorlockStats)")]
    [Header("Vida")]
    [SerializeField] protected float health = 15f;
    [Header("Movimiento")]
    [SerializeField] protected float moveSpeed = 4f;

    #endregion

    #region Inspector - Combat Settings

    [Header("Combate")]
    [SerializeField] protected float fireRate = 1.5f;
    [SerializeField] protected bool useRandomFireRate = true;
    [SerializeField] protected float minFireRate = 1.5f;
    [SerializeField] protected float maxFireRate = 3f;
    [SerializeField] protected float minDamageIncrease = 1f;
    [SerializeField] protected float projectileSpeed = 12f;
    [SerializeField] protected float maxDamageIncrease = 2;
    [SerializeField] protected float maxDistanceForDamageIncrease = 6f;
    [SerializeField] protected float maxDistanceForDamageStart = 20f;
    [SerializeField] protected float attackRange = 50f;

    #endregion

    #region Inspector - Patrol Settings

    [Header("Patrullaje")]
    [SerializeField] protected bool canReturnToPatrol = true;
    [SerializeField] protected float detectionRadius = 50f;
    [SerializeField] protected float detectionGrowthInterval = 2.5f;
    [SerializeField] protected float detectionGrowthAmount = 10f;
    [SerializeField] protected float teleportRange = 5f;
    [SerializeField] protected Transform[] patrolWaypoints;
    [SerializeField] protected bool loopWaypoints = true;
    [SerializeField] protected int freePatrolIterations = 10;
    [SerializeField] protected bool patrolAroundOrigin = true;
    [SerializeField] protected float patrolRadius = 8f;
    [SerializeField] protected float patrolIdleTime = 1.2f;
    [SerializeField] protected bool canReposition = false;
    [SerializeField] protected float repositionTeleportCooldown = 0.75f;

    #endregion

    #region Inspector - Pursuit Settings

    [Header("Perseguir 1")]
    [SerializeField] protected float p1_teleportCooldown = 2.5f;
    [SerializeField] protected float p1_pursuitAdvanceDistance = 4f;
    [SerializeField] protected float p1_pursuitLateralVariationMin = 3f;
    [SerializeField] protected float p1_pursuitLateralVariationMax = 5f;

    [Header("Perseguir 2")]
    [SerializeField] protected float p2_activationRadius = 5f;
    [SerializeField] protected float p2_teleportCooldown = 2.5f;
    [SerializeField] protected float p2_teleportRange = 5f;

    #endregion

    #region Inspector - Spawn Settings

    [Header("Configuracion de spawn")]
    [SerializeField] protected float spawnDelay = 1.0f;

    #endregion

    #region Inspector - Audio Settings

    [Header("Sound")]
    [SerializeField] protected AudioSource audioSource;
    [Header("SFX Generales")]
    [SerializeField] protected AudioClip idleSFX;
    [SerializeField] protected AudioClip teleportSFX;
    [SerializeField] protected AudioClip deathSFX;
    [Header("SFX Combate")]
    [SerializeField] protected AudioClip shootSFX;
    #endregion

    #region Inspector - VFX Settings

    [Header("VFX Teletransporte")]
    [SerializeField] protected float animTeleportDelay = 1f;
    [SerializeField] protected GameObject teleportVFX;
    [SerializeField] protected float teleportVFXHeightOffset = 1.5f;
    [SerializeField] protected float teleportVFXDurationToDestroy = 1.5f;
    [SerializeField] protected float teleportVFXDelay = 1.5f;

    #endregion

    #region Internal State

    protected EnemyHealth enemyHealth;
    protected NavMeshAgent agent;
    protected Transform playerTransform;
    protected CharacterController playerCharacterController;
    protected MorlockWordLibrary wordLibrary;
    protected MorlockState currentState;

    protected bool isDead = false;
    protected bool isReady = false;
    protected Coroutine currentBehaviorCoroutine = null;
    protected Coroutine shootCoroutine = null;

    protected int currentWaypointIndex = 0;
    protected Vector3 originPosition;
    protected List<GameObject> activeTeleportVFXs = new List<GameObject>();

    protected float idleTimer;
    protected float idleInterval;
    protected float currentDetectionTimer;
    protected float baseDetectionRadius;
    protected Vector3 lastLookDirection = Vector3.forward;

    #endregion

    #region Unity Lifecycle

    protected virtual void Awake()
    {
        if (enemyHealth == null) enemyHealth = GetComponent<EnemyHealth>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (visualCtrl == null) visualCtrl = GetComponentInChildren<StaticAnimCtrl>();
        if (wordLibrary == null) wordLibrary = GetComponent<MorlockWordLibrary>();

        if (enemyHealth == null) ReportDebug("Componente EnemyHealth no encontrado en el enemigo.", 3);
        if (agent == null) ReportDebug("Componente NavMeshAgent no encontrado en el enemigo.", 3);
        if (visualCtrl == null) ReportDebug("Componente StaticAnimCtrl no encontrado en el enemigo.", 2);
    }

    protected virtual void Start()
    {
        originPosition = transform.position;
        baseDetectionRadius = detectionRadius;

        var playerGameObject = GameObject.FindGameObjectWithTag("Player");
        if (playerGameObject != null)
        {
            playerTransform = playerGameObject.transform;
            playerCharacterController = playerGameObject.GetComponent<CharacterController>();
        }

        InitializedEnemy();
        StartCoroutine(SpawnRoutine());
        ResetIdleTimer();
    }

    protected virtual void OnEnable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath += HandleEnemyDeath;
            enemyHealth.OnDamaged += HandleDamageTaken;
        }
    }

    protected virtual void OnDisable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleEnemyDeath;
            enemyHealth.OnDamaged -= HandleDamageTaken;
        }
        StopAllCoroutines();
    }

    protected virtual void OnDestroy()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleEnemyDeath;
            enemyHealth.OnDamaged -= HandleDamageTaken;
        }
        StopAllCoroutines();
    }

    protected virtual void Update()
    {
        if (isDead || playerTransform == null || !isReady) return;

        UpdateAnimationAndRotation();

        if (enemyHealth != null && enemyHealth.IsStunned)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            return;
        }

        HandleIdleSound();

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        float distanceFromOrigin = Vector3.Distance(transform.position, originPosition);

        if (distanceToPlayer > detectionRadius && (currentState != MorlockState.Patrol && currentState != MorlockState.Repositioning))
        {
            if (canReturnToPatrol)
            {
                if (patrolAroundOrigin && distanceFromOrigin > patrolRadius * 1.1f)
                {
                    if (canReposition) ChangeState(MorlockState.Repositioning);
                    else ChangeState(MorlockState.Patrol);
                }
                else ChangeState(MorlockState.Patrol);
            }
            return;
        }

        switch (currentState)
        {
            case MorlockState.Patrol:
                HandleDetectionGrowth();
                if (distanceToPlayer <= detectionRadius)
                {
                    ChangeState(MorlockState.Pursue1);
                }
                break;
            case MorlockState.Pursue1:
                if (distanceToPlayer <= p2_activationRadius)
                {
                    ChangeState(MorlockState.Pursue2);
                }
                break;
            case MorlockState.Pursue2:
                break;
        }
    }

    #endregion

    #region Initialization & Data Sync

    protected virtual void InitializedEnemy()
    {
        if (stats != null)
        {
            health = stats.health;
            moveSpeed = stats.moveSpeed;
            attackRange = stats.optimalAttackDistance;
            teleportRange = stats.teleportRange;
            fireRate = stats.fireRate;
            minDamageIncrease = stats.projectileDamage;
            projectileSpeed = stats.projectileSpeed;
            enemyHealth.SetMaxHealth(stats.health);
        }
        else
        {
            ReportDebug("MorlockStats no asignado. Usando valores por defecto.", 2);
        }

        enemyHealth.SetMaxHealth(health);

        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.stoppingDistance = attackRange;
            agent.updatePosition = true;
            agent.updateRotation = false;
            agent.isStopped = false;
        }
    }

    protected virtual IEnumerator SpawnRoutine()
    {
        isReady = false;
        yield return new WaitForSeconds(spawnDelay);
        isReady = true;
        ChangeState(MorlockState.Patrol);
    }

    #endregion

    #region Health & Damage System

    public virtual void ApplyRoomMultiplier(float damageMult, float speedMult)
    {
        minDamageIncrease *= damageMult;
        moveSpeed *= speedMult;
        if (agent != null) agent.speed = moveSpeed;
        ReportDebug($"Multiplicadores de sala aplicados a Morlock. Daño x{damageMult}, Velocidad x{speedMult}. Nueva Velocidad: {moveSpeed:F2}", 1);
    }

    protected virtual void HandleDamageTaken() { }

    protected virtual void HandleEnemyDeath(GameObject enemy)
    {
        if (isDead || enemy != gameObject) return;

        isDead = true;

        if (visualCtrl != null) visualCtrl.PlayDeath();
        if (audioSource != null && deathSFX != null) audioSource.PlayOneShot(deathSFX);

        ChangeState(MorlockState.Repositioning);
        StopAllCoroutines();

        for (int i = activeTeleportVFXs.Count - 1; i >= 0; i--)
        {
            if (activeTeleportVFXs[i] != null) Destroy(activeTeleportVFXs[i]);
        }
        activeTeleportVFXs.Clear();

        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.updatePosition = false;
            agent.updateRotation = false;
        }

        this.enabled = false;
    }

    #endregion

    #region AI State Machine

    protected virtual void ChangeState(MorlockState newState)
    {
        if (currentState == newState && currentBehaviorCoroutine != null && currentState != MorlockState.Repositioning) return;

        if (currentBehaviorCoroutine != null)
        {
            StopCoroutine(currentBehaviorCoroutine);
            currentBehaviorCoroutine = null;
        }

        if (shootCoroutine != null)
        {
            StopCoroutine(shootCoroutine);
            shootCoroutine = null;
        }

        currentState = newState;

        switch (currentState)
        {
            case MorlockState.Patrol:
                detectionRadius = baseDetectionRadius;
                currentDetectionTimer = 0f;
                currentBehaviorCoroutine = StartCoroutine(PatrolRoutine());
                break;
            case MorlockState.Pursue1:
                currentBehaviorCoroutine = StartCoroutine(Pursuit1Routine());
                break;
            case MorlockState.Pursue2:
                currentBehaviorCoroutine = StartCoroutine(Pursuit2Routine());
                break;
            case MorlockState.Repositioning:
                currentBehaviorCoroutine = StartCoroutine(RepositioningRoutine());
                break;
        }
    }

    protected virtual IEnumerator PatrolRoutine()
    {
        int freePatrolCount = 0;
        bool shouldContinue = true;

        while (currentState == MorlockState.Patrol && shouldContinue)
        {
            Vector3 targetPosition;
            if (patrolWaypoints != null && patrolWaypoints.Length > 0)
            {
                targetPosition = patrolWaypoints[currentWaypointIndex].position;
                if (loopWaypoints) currentWaypointIndex = (currentWaypointIndex + 1) % patrolWaypoints.Length;
                else
                {
                    currentWaypointIndex++;
                    if (currentWaypointIndex >= patrolWaypoints.Length) shouldContinue = false;
                }
            }
            else
            {
                if (!loopWaypoints)
                {
                    freePatrolCount++;
                    if (freePatrolCount > freePatrolIterations) shouldContinue = false;
                }

                Vector3 center = patrolAroundOrigin ? originPosition : transform.position;
                TryGetRandomPoint(center, patrolRadius, out targetPosition);
            }

            yield return StartCoroutine(TeleportToPositionRoutine(targetPosition));
            yield return new WaitForSeconds(patrolIdleTime);
        }
    }

    protected virtual IEnumerator RepositioningRoutine()
    {
        if (originPosition == Vector3.zero) originPosition = transform.position;

        while (currentState == MorlockState.Repositioning)
        {
            float distFromOrigin = Vector3.Distance(transform.position, originPosition);

            if (distFromOrigin <= patrolRadius)
            {
                ChangeState(MorlockState.Patrol);
                yield break;
            }

            Vector3 stepTarget = Vector3.MoveTowards(transform.position, originPosition, teleportRange);

            NavMeshHit stepHit;
            Vector3 finalPos = stepTarget;
            if (NavMesh.SamplePosition(stepTarget, out stepHit, 2.5f, NavMesh.AllAreas))
            {
                finalPos = stepHit.position;
            }

            OnBeforeTeleport(transform.position);

            if (visualCtrl != null) visualCtrl.PlayTPout();
            if (audioSource != null && teleportSFX != null) audioSource.PlayOneShot(teleportSFX);
            SpawnTeleportVFX(transform.position);

            yield return new WaitForSeconds(0.4f);

            transform.position = finalPos;
            if (agent != null && agent.enabled) agent.Warp(finalPos);

            if (visualCtrl != null)
            {
                visualCtrl.restoreOriginalMaterials();
                visualCtrl.PlayTPin();
            }
            SpawnTeleportVFX(finalPos);

            yield return new WaitForSeconds(repositionTeleportCooldown);
        }
    }

    protected virtual IEnumerator Pursuit1Routine()
    {
        while (currentState == MorlockState.Pursue1)
        {
            Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
            Vector3 advancePosition = transform.position + directionToPlayer * p1_pursuitAdvanceDistance;

            Vector3 lateralDirection = Vector3.Cross(directionToPlayer, Vector3.up);
            float lateralOffset = Random.Range(p1_pursuitLateralVariationMin, p1_pursuitLateralVariationMax) * (Random.value > 0.5f ? 1f : -1f);

            Vector3 targetPosition = advancePosition + lateralDirection * lateralOffset;

            yield return StartCoroutine(TeleportToPositionRoutine(targetPosition));
            StartShootCoroutine();
            yield return new WaitForSeconds(p1_teleportCooldown);
        }
    }

    protected virtual IEnumerator Pursuit2Routine()
    {
        Vector3 lastTargetPos = Vector3.zero;

        while (currentState == MorlockState.Pursue2)
        {
            List<Vector3> validCandidates = new List<Vector3>();
            float[] angles = { 0f, 90f, 180f, 270f };

            for (int i = 0; i < angles.Length; i++)
            {
                Vector3 offset = Quaternion.Euler(0, angles[i], 0) * Vector3.forward * p2_teleportRange;
                Vector3 potentialPos = playerTransform.position + offset;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(potentialPos, out hit, 2.0f, NavMesh.AllAreas))
                {
                    validCandidates.Add(hit.position);
                }
            }

            if (validCandidates.Count > 0)
            {
                Vector3 selectedPos;
                if (validCandidates.Count > 1 && lastTargetPos != Vector3.zero)
                {
                    var freshCandidates = validCandidates.FindAll(p => Vector3.Distance(p, lastTargetPos) > 1.0f);
                    if (freshCandidates.Count > 0) selectedPos = freshCandidates[Random.Range(0, freshCandidates.Count)];
                    else selectedPos = validCandidates[Random.Range(0, validCandidates.Count)];
                }
                else
                {
                    selectedPos = validCandidates[Random.Range(0, validCandidates.Count)];
                }

                lastTargetPos = selectedPos;
                yield return StartCoroutine(TeleportToPositionRoutine(selectedPos));
                StartShootCoroutine();
            }
            else
            {
                ReportDebug("Pursue2: Ningún punto cumple con la distancia/navmesh requerida. Esperandondo.", 2);
            }

            yield return new WaitForSeconds(p2_teleportCooldown);
        }
    }

    #endregion

    #region Movement & Navigation

    protected virtual void HandleDetectionGrowth()
    {
        currentDetectionTimer += Time.deltaTime;
        if (currentDetectionTimer >= detectionGrowthInterval)
        {
            detectionRadius += detectionGrowthAmount;
            currentDetectionTimer = 0f;
            ReportDebug($"Rango de detección aumentado. Nuevo radio: {detectionRadius}", 1);
        }
    }

    protected virtual void UpdateAnimationAndRotation()
    {
        Vector3 targetDirection = Vector3.zero;

        if (currentState == MorlockState.Pursue1 || currentState == MorlockState.Pursue2)
        {
            if (playerTransform != null) targetDirection = (playerTransform.position - transform.position).normalized;
        }
        else if (agent != null && agent.velocity.sqrMagnitude > 0.1f)
        {
            targetDirection = agent.velocity.normalized;
        }

        if (targetDirection == Vector3.zero) targetDirection = lastLookDirection;
        else targetDirection.y = 0;

        if (targetDirection.sqrMagnitude > 0.01f)
        {
            float angle = Mathf.Atan2(targetDirection.x, targetDirection.z) * Mathf.Rad2Deg;
            float snappedAngle = Mathf.Round(angle / 45f) * 45f;

            Quaternion targetRotation = Quaternion.Euler(0, snappedAngle, 0);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 720f * Time.deltaTime);

            Vector3 dirVector = Quaternion.Euler(0, snappedAngle, 0) * Vector3.forward;
            float xInt = Mathf.Round(dirVector.x);
            float yInt = Mathf.Round(dirVector.z);
            lastLookDirection = new Vector3(xInt, 0, yInt);
        }
    }

    protected virtual void ForceFacePlayer()
    {
        if (playerTransform == null) return;

        Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
        directionToPlayer.y = 0;

        if (directionToPlayer.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(directionToPlayer.x, directionToPlayer.z) * Mathf.Rad2Deg;
            float snappedAngle = Mathf.Round(angle / 45f) * 45f;
            transform.rotation = Quaternion.Euler(0, snappedAngle, 0);

            Vector3 dirVector = Quaternion.Euler(0, snappedAngle, 0) * Vector3.forward;
            float xInt = Mathf.Round(dirVector.x);
            float yInt = Mathf.Round(dirVector.z);
            lastLookDirection = new Vector3(xInt, 0, yInt);
        }
    }

    protected virtual IEnumerator TeleportToPositionRoutine(Vector3 targetPosition)
    {
        if (enemyHealth != null && enemyHealth.IsStunned) yield break;

        NavMeshHit hit;
        Vector3 finalDestination = targetPosition;
        if (NavMesh.SamplePosition(targetPosition, out hit, 10f, NavMesh.AllAreas))
        {
            finalDestination = hit.position;
        }
        else yield break;

        OnBeforeTeleport(transform.position);

        if (visualCtrl != null) visualCtrl.PlayTPout();
        SpawnTeleportVFX(transform.position);

        yield return new WaitForSeconds(0.35f);

        if (isDead || (enemyHealth != null && enemyHealth.IsStunned)) yield break;

        if (audioSource != null && teleportSFX != null) audioSource.PlayOneShot(teleportSFX);

        transform.position = finalDestination;
        if (agent != null && agent.enabled) agent.Warp(finalDestination);

        if (visualCtrl != null)
        {
            visualCtrl.restoreOriginalMaterials();
            visualCtrl.PlayTPin();
        }

        SpawnTeleportVFX(finalDestination);

        if (playerTransform != null && currentState != MorlockState.Patrol)
        {
            ForceFacePlayer();
        }
    }

    protected virtual void OnBeforeTeleport(Vector3 fromPosition) { }

    protected virtual bool TryGetRandomPoint(Vector3 center, float radius, out Vector3 result)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 rand = center + Random.insideUnitSphere * radius;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(rand, out hit, 2.0f, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }
        result = center;
        return false;
    }

    #endregion

    #region Combat System

    protected virtual void StartShootCoroutine()
    {
        if (shootCoroutine != null) StopCoroutine(shootCoroutine);
        shootCoroutine = StartCoroutine(ShootAfterDelayRoutine());
    }

    protected virtual IEnumerator ShootAfterDelayRoutine()
    {
        if (useRandomFireRate) fireRate = Random.Range(minFireRate, maxFireRate);

        yield return new WaitForSeconds(fireRate);
        yield return new WaitForSeconds(animTeleportDelay);

        if (!isDead && currentState != MorlockState.Patrol && currentState != MorlockState.Repositioning)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            if (distanceToPlayer <= attackRange)
            {
                ForceFacePlayer();
                if (visualCtrl != null) visualCtrl.PlayShoot();
                ExecuteProjectileSpawn();
            }
        }
        shootCoroutine = null;
    }

    protected virtual void ExecuteProjectileSpawn()
    {
        if (enemyHealth != null && enemyHealth.IsStunned || isDead) return;

        if (audioSource != null && shootSFX != null) audioSource.PlayOneShot(shootSFX);

        Vector3 aimPoint = playerTransform.position;
        float interceptProb = GetInterceptProbability(currentLevel);

        if (playerCharacterController != null && Random.value < interceptProb)
        {
            aimPoint = CalculateInterceptPoint(playerTransform.position, playerCharacterController.velocity);
        }

        Vector3 directionToAim = (aimPoint - firePoint.position).normalized;
        firePoint.rotation = Quaternion.LookRotation(directionToAim);

        InstantiateAndInitializeProjectile();
    }

    protected virtual void InstantiateAndInitializeProjectile()
    {
        GameObject projectileObj = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        StaticProjectileBase projectile = projectileObj.GetComponent<StaticProjectileBase>();

        if (projectile != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            float calculatedDamage = CalculateDamageByDistance(distanceToPlayer);
            string selectedWord = wordLibrary != null ? wordLibrary.GetRandomWord() : "STATIC";

            projectile.Initialize(projectileSpeed, calculatedDamage, selectedWord);
        }
    }

    public virtual void Shoot()
    {
        ExecuteProjectileSpawn();
    }

    protected virtual float CalculateDamageByDistance(float distance)
    {
        if (distance <= maxDistanceForDamageIncrease) return maxDamageIncrease;
        if (distance >= maxDistanceForDamageStart) return minDamageIncrease;

        float t = (distance - maxDistanceForDamageIncrease) / (maxDistanceForDamageStart - maxDistanceForDamageIncrease);
        return Mathf.Lerp(maxDamageIncrease, minDamageIncrease, t);
    }

    protected virtual float GetInterceptProbability(MorlockLevel level)
    {
        switch (level)
        {
            case MorlockLevel.Nivel1: return interceptProbabilityNivel1;
            case MorlockLevel.Nivel2: return interceptProbabilityNivel2;
            case MorlockLevel.Nivel3: return interceptProbabilityNivel3;
            default: return 0.0f;
        }
    }

    protected virtual Vector3 CalculateInterceptPoint(Vector3 targetPosition, Vector3 targetVelocity)
    {
        Vector3 directionToTarget = targetPosition - firePoint.position;
        float distance = directionToTarget.magnitude;

        if (projectileSpeed <= 0) return targetPosition;

        float timeToIntercept = distance / projectileSpeed;
        Vector3 futurePosition = targetPosition + targetVelocity * timeToIntercept;
        futurePosition += Random.insideUnitSphere * 0.5f;

        return futurePosition;
    }

    #endregion

    #region Visual & Audio Effects

    protected virtual void HandleIdleSound()
    {
        idleTimer += Time.deltaTime;
        if (idleTimer >= idleInterval)
        {
            if (audioSource != null && idleSFX != null)
            {
                audioSource.pitch = Random.Range(0.9f, 1.1f);
                audioSource.PlayOneShot(idleSFX);
                audioSource.pitch = 1f;
            }
            ResetIdleTimer();
        }
    }

    protected virtual void ResetIdleTimer()
    {
        idleTimer = 0f;
        idleInterval = Random.Range(5f, 9f);
    }

    protected virtual void SpawnTeleportVFX(Vector3 basePosition)
    {
        Vector3 spawnPos = basePosition;
        spawnPos.y += teleportVFXHeightOffset;

        for (int i = activeTeleportVFXs.Count - 1; i >= 0; i--)
        {
            GameObject existingVFX = activeTeleportVFXs[i];

            if (existingVFX != null)
            {
                if (Vector3.Distance(existingVFX.transform.position, spawnPos) < 1.0f)
                {
                    Destroy(existingVFX);
                    activeTeleportVFXs.RemoveAt(i);
                }
            }
            else
            {
                activeTeleportVFXs.RemoveAt(i);
            }
        }

        if (teleportVFX != null)
        {
            GameObject vfxInstance = Instantiate(teleportVFX, spawnPos, Quaternion.identity);
            activeTeleportVFXs.Add(vfxInstance);
            StartCoroutine(RemoveVFXFromListAfterDelay(vfxInstance, teleportVFXDurationToDestroy));
        }
    }

    protected virtual IEnumerator RemoveVFXFromListAfterDelay(GameObject vfx, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (vfx != null)
        {
            if (activeTeleportVFXs.Contains(vfx)) activeTeleportVFXs.Remove(vfx);
            Destroy(vfx);
        }
    }

    #endregion

    #region Debugging

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    protected static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1: Debug.Log($"[StaticEnemyBase] {message}"); break;
            case 2: Debug.LogWarning($"[StaticEnemyBase] {message}"); break;
            case 3: Debug.LogError($"[StaticEnemyBase] {message}"); break;
            default: Debug.Log($"[StaticEnemyBase] {message}"); break;
        }
    }

    #endregion
}