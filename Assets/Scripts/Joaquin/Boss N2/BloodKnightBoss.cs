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
    [SerializeField] private float lowHPSpeedMultiplier = 1.3f;

    [Header("Ciclo 1: Apocalipsis")]
    [SerializeField] private float apocalipsisDuration = 10f;
    [SerializeField] private float apocalipsisDamage = 7f;
    [SerializeField] private int apocalipsisTargetDashes = 10;
    [SerializeField] private float apocalipsisRange = 4f;

    [Header("Ciclo 2: Pausa Posicionamiento")]
    [SerializeField] private float positioningDuration = 2f;

    [Header("Ciclo 3: Sodoma y Gomorra")]
    [SerializeField] private float sodomaDamage = 35f;
    [SerializeField] private float sodomaCutRange = 8f;
    [SerializeField] private float fireTrailDamagePerSecond = 5f;
    [SerializeField] private float sodomaChargeTime = 1.5f;
    [SerializeField] private float sodomaBackwardDistance = 3f;

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
        Charging,
        Vulnerable
    }

    private BossState currentState = BossState.Idle;
    private NavMeshAgent agent;
    private EnemyHealth enemyHealth;
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

    #endregion

    #region Camera Shake
    [SerializeField] private CinemachineCamera vcam;
    private CinemachineBasicMultiChannelPerlin noise;
    #endregion

    private void Awake()
    {
        InitializeComponents();
        InitializeVFXCache();
    }

    private void Start()
    {
        if (bossAICoroutine == null)
        {
            bossAICoroutine = StartCoroutine(BossFlowSequence());
        }
    }

    private void InitializeComponents()
    {
        enemyHealth = GetComponent<EnemyHealth>();
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
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
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
    }

    private void HandleEnemyDeath(GameObject g)
    {
        StopAllCoroutines();
        DestroyAllInstantiatedEffects();
        if (agent != null) agent.enabled = false;
        if (animator != null) animator.SetTrigger("Die");
        this.enabled = false;
    }

    private void DestroyAllInstantiatedEffects()
    {
        foreach (var effect in instantiatedEffects) if (effect != null) Destroy(effect);
        instantiatedEffects.Clear();
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
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    agent.ResetPath();
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

    #region Apocalipsis Logic

    private IEnumerator ExecuteApocalipsisSequence()
    {
        ReportDebug("ETAPA 1: APOCALIPSIS", 1);
        currentState = BossState.Attacking;

        float startTime = Time.time;
        float endTime = startTime + apocalipsisDuration;
        int dashesPerformed = 0;

        // Dividir el tiempo total entre el numero de dashes para mantener ritmo
        float timePerDash = apocalipsisDuration / apocalipsisTargetDashes;

        if (animator != null) animator.SetTrigger("StartApocalipsis");

        while (Time.time < endTime && dashesPerformed < apocalipsisTargetDashes)
        {
            agent.isStopped = false;
            agent.SetDestination(player.position);
            yield return new WaitForSeconds(0.4f);

            agent.isStopped = true;
            Vector3 targetPos = GetZigZagDashPosition();
            targetPos.y = transform.position.y;

            // Realizar dash rápido hacia la posición objetivo
            yield return MoveToPositionFast(targetPos, 0.3f);

            if (player != null)
            {
                Vector3 lookPos = player.position;
                lookPos.y = transform.position.y;
                transform.LookAt(lookPos);
            }

            float distToPlayer = Vector3.Distance(transform.position, player.position);

            if (distToPlayer <= apocalipsisRange)
            {
                if (animator != null) animator.SetTrigger("Slash");
                if (audioSource != null) audioSource.PlayOneShot(apocalipsisSlashSound, 0.5f);

                SpawnSlashVFX();
                DealApocalipsisDamage();

                Vector3 retreatPos = transform.position - transform.forward * 2.5f;
                yield return MoveToPositionFast(retreatPos, 0.25f);
            }
            else
            {
                yield return new WaitForSeconds(0.2f);
            }

            dashesPerformed++;

            float expectedTime = startTime + (dashesPerformed * timePerDash);
            float waitTime = expectedTime - Time.time;
            if (waitTime > 0) yield return new WaitForSeconds(waitTime);
        }

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

    private void DealApocalipsisDamage()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position + transform.forward, apocalipsisRange);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player") && playerHealth != null)
            {
                ExecuteAttack(hit.gameObject, apocalipsisDamage);
                break;
            }
        }
    }

    #endregion

    #region Positioning Phase

    private IEnumerator ExecutePositioningPhase()
    {
        ReportDebug("ETAPA 2: POSICIONAMIENTO", 1);

        float timer = 0f;
        agent.isStopped = false;

        if (animator != null) animator.SetBool("IsMoving", true);

        // Mantener distancia media durante 2 segundos
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

        agent.isStopped = true;
        if (animator != null) animator.SetBool("IsMoving", false);
    }

    #endregion

    #region Sodoma y Gomorra Logic

    private IEnumerator ExecuteSodomaYGomorra()
    {
        ReportDebug("ETAPA 3: SODOMA Y GOMORRA", 1);
        currentState = BossState.Attacking;

        Vector3 backwardPos = transform.position - transform.forward * sodomaBackwardDistance;
        if (animator != null) animator.SetTrigger("Crouch"); // Animación de preparación
        yield return MoveToPositionFast(backwardPos, 0.4f);

        StartArmorGlow();
        if (audioSource != null) audioSource.PlayOneShot(sodomaChargeSound);

        yield return new WaitForSeconds(sodomaChargeTime);
        StopArmorGlow();

        if (animator != null) animator.SetTrigger("DashSlash");
        if (audioSource != null) audioSource.PlayOneShot(sodomaAttackSound);

        Vector3 dirToPlayer = (player.position - transform.position).normalized;
        float stopDistance = 1.2f;
        Vector3 strikePos = player.position - (dirToPlayer * stopDistance); // Detenerse un poco antes del jugador
        strikePos.y = transform.position.y;

        yield return MoveToPositionFast(strikePos, 0.3f);

        PerformSodomaSlash();

        SpawnGreenFireTrail(backwardPos, strikePos);

        yield return new WaitForSeconds(1f);
        currentState = BossState.Idle;
    }

    private void PerformSodomaSlash()
    {
        // Daño en cono frontal o área cercana
        Collider[] hits = Physics.OverlapSphere(transform.position, sodomaCutRange);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Player") && playerHealth != null)
            {
                ExecuteAttack(hit.gameObject, sodomaDamage);
                break; // Solo dañar una vez al jugador
            }
        }
    }

    #endregion

    #region Cooldown Phase (Necio Pecador + Vulnerable)

    private IEnumerator ExecuteCooldownPhase()
    {
        ReportDebug("ETAPA 4: FASE COOLDOWN / RELLENO", 1);

        float phaseTimer = 0f;

        ReportDebug("Iniciando Necio Pecador", 1);

        agent.isStopped = false;
        agent.speed = isInLowHealthPhase ? (moveSpeed * lowHPSpeedMultiplier * 0.8f) : (moveSpeed * 0.7f);

        float currentWarningTime = isInLowHealthPhase ? (necioPecadorWarningTime * 0.6f) : necioPecadorWarningTime;

        while (phaseTimer < necioAttackWindow)
        {
            // Movimiento: Si está lejos, se acerca. Si está cerca, orbita.
            if (player != null)
            {
                float dist = Vector3.Distance(transform.position, player.position);
                if (dist > 12f) agent.SetDestination(player.position);
                else
                {
                    // Orbitar lateralmente
                    Vector3 orbit = transform.position + transform.right * 3f;
                    agent.SetDestination(orbit);
                }
            }

            if (animator != null) animator.SetTrigger("SummonHand");
            if (audioSource != null) audioSource.PlayOneShot(necioPecadorSummonSound);

            Vector3 rawTargetPos = player.position;
            if (playerController != null)
            {
                rawTargetPos += playerController.velocity * 1.2f;
            }

            Vector3 finalTargetPos = GetGroundPosition(rawTargetPos);

            GameObject warning = SpawnNecioPecadorWarning(finalTargetPos, necioPecadorRadius);
            yield return new WaitForSeconds(currentWarningTime);
            if (warning != null) Destroy(warning);

            SpawnSoulHand(finalTargetPos);

            float interval = isInLowHealthPhase ? 1.5f : 2.5f;

            float remainingWait = interval - currentWarningTime;
            if (remainingWait > 0) yield return new WaitForSeconds(remainingWait);

            phaseTimer += interval;
        }

        agent.speed = isInLowHealthPhase ? (moveSpeed * lowHPSpeedMultiplier) : moveSpeed;
        agent.isStopped = true;

        ReportDebug("NECIO PECADOR FINALIZADO. BOSS VULNERABLE", 1);
        currentState = BossState.Vulnerable;

        if (animator != null) animator.SetBool("IsTired", true);

        // Aquí podría ir un feedback para indicar vulnerabilidad

        yield return new WaitForSeconds(necioVulnerableWindow);

        if (animator != null) animator.SetBool("IsTired", false);
        currentState = BossState.Idle;
    }

    #endregion

    #region Special Ability: Embestida del Fornido

    private IEnumerator ExecuteChargeAbility()
    {
        ReportDebug("EJECUTANDO EMBESTIDA DEL FORNIDO", 1);
        currentState = BossState.Charging;
        agent.isStopped = true;

        if (animator != null) animator.SetTrigger("KneelCharge");
        yield return new WaitForSeconds(1.5f);

        StartEyeGlow();

        if (player != null)
        {
            Vector3 chargeDir = (player.position - transform.position).normalized;
            chargeDir.y = 0; // Mantener en plano horizontal
            transform.rotation = Quaternion.LookRotation(chargeDir);
        }

        if (animator != null) animator.SetTrigger("ChargeRun");
        if (audioSource != null) audioSource.PlayOneShot(chargeAbilitySound);

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
            // Aturdirse brevemente por fallar
            if (animator != null) animator.SetTrigger("Stunned");
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
        // Opción A: Raycast (Más preciso visualmente para terrenos irregulares)
        // Lanza un rayo desde 2 metros arriba hacia abajo buscando la capa "Ground" o "Default"
        if (Physics.Raycast(targetPos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f, LayerMask.GetMask("Default", "Ground", "Terrain")))
        {
            return hit.point + Vector3.up * 0.025f; // Pequeño offset para evitar que la textura parpadee
        }

        // Opción B: NavMesh (Si el raycast falla, busca el punto navegable más cercano)
        if (NavMesh.SamplePosition(targetPos, out NavMeshHit navHit, 2.0f, NavMesh.AllAreas))
        {
            return navHit.position + Vector3.up * 0.02f;
        }

        // Fallback: Si todo falla, mantener la altura del Boss pero asegurar que no flote tanto
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
        if (ft != null) ft.DamagePerSecond = fireTrailDamagePerSecond;
    }

    private GameObject SpawnNecioPecadorWarning(Vector3 pos, float radius)
    {
        if (necioPecadorWarningPrefab == null) return null;
        GameObject warningObj = Instantiate(necioPecadorWarningPrefab, pos, Quaternion.identity);
        warningObj.transform.localScale = new Vector3(radius * 2, 0.1f, radius * 2); // Ajustar tamaño visual
        instantiatedEffects.Add(warningObj);
        return warningObj;
    }

    private void SpawnSoulHand(Vector3 pos)
    {
        if (soulHandPrefab == null) return;
        GameObject hand = Instantiate(soulHandPrefab, pos, Quaternion.identity);
        instantiatedEffects.Add(hand);

        SoulHand sh = hand.GetComponent<SoulHand>();
        if (sh != null) sh.Initialize(necioPecadorDamage, necioPecadorRadius);

        if (audioSource != null) audioSource.PlayOneShot(necioPecadorExplosionSound);
    }

    private void SpawnSlashVFX()
    {
        if (greenFireVFX != null && swordTransform != null)
        {
            ParticleSystem vfx = Instantiate(greenFireVFX, swordTransform.position, swordTransform.rotation);
            instantiatedEffects.Add(vfx.gameObject);
            vfx.Play();
            Destroy(vfx.gameObject, 1f);
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

    private void OnGUI()
    {
        if (!showDebugGUI) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 220));
        GUILayout.Box("Dark Knight AI State");
        GUILayout.Label($"Health: {currentHealth}/{maxHealth} ({(currentHealth / maxHealth) * 100:F1}%)");
        GUILayout.Label($"State: {currentState}");
        GUILayout.Label($"Low HP Phase: {isInLowHealthPhase}");
        GUILayout.Label($"Speed Multiplier: {(speedBuffApplied ? lowHPSpeedMultiplier : 1.0f)}x");

        if (forceApocalipsisNext)
            GUILayout.Label("WARNING: Next Cycle Forced -> Apocalipsis");

        GUILayout.EndArea();
    }

    private void OnDrawGizmos()
    {
        Color stateColor = currentState switch
        {
            BossState.Attacking => Color.red,
            BossState.Vulnerable => Color.green,
            BossState.Charging => new Color(1f, 0.5f, 0f),
            BossState.Stunned => Color.yellow,
            BossState.Chasing => Color.blue,
            _ => Color.white
        };

        Gizmos.color = stateColor;

        Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, apocalipsisRange);

        Gizmos.color = new Color(1f, 0f, 0f, 0.1f);
        Gizmos.DrawSphere(transform.position, sodomaCutRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, sodomaCutRange);

        Vector3 backwardPos = transform.position - transform.forward * sodomaBackwardDistance;
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(transform.position, backwardPos);
        Gizmos.DrawWireSphere(backwardPos, 0.3f);

        if (player != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, player.position);

            Vector3 dirToPlayer = (player.position - transform.position).normalized;
            Vector3 stopPos = player.position - (dirToPlayer * 1.2f);

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(stopPos, 0.5f);
            Gizmos.DrawLine(player.position, stopPos);
        }

        if (currentState == BossState.Charging || currentState == BossState.Idle)
        {
            Gizmos.color = new Color(0.5f, 0f, 1f, 0.5f);
            Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position + Vector3.up * 1.5f, transform.rotation, transform.lossyScale);
            Gizmos.matrix = rotationMatrix;
            Gizmos.DrawWireCube(Vector3.forward * 1.5f, new Vector3(3f, 3f, 3f));
            Gizmos.matrix = Matrix4x4.identity;
        }

#if UNITY_EDITOR
        GUIStyle style = new GUIStyle();
        style.normal.textColor = stateColor;
        style.fontSize = 15;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;

        UnityEditor.Handles.Label(transform.position + Vector3.up * 3.5f, $"{currentState}\nHP: {currentHealth}", style);
#endif
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