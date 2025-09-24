using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public class BloodKnightBoss : MonoBehaviour
{
    #region Statistics and Configuration

    [Header("Boss Configuration")]
    [SerializeField] private float maxHealth = 300f;
    [SerializeField] private float currentHealth;
    [SerializeField] private float moveSpeed = 5f;

    [Header("Attack 1 - Sodoma y Gomorra")]
    [SerializeField] private float sodomaDamage = 35f;
    [SerializeField] private float fireTrailDamage = 5f;
    [SerializeField] private float sodomaCooldown = 15f;
    [SerializeField] private float sodomaRange = 8f;
    [SerializeField] private float stunDuration = 4f;

    [Header("Attack 2 - Apocalipsis")]
    [SerializeField] private float apocalipsisDamage = 7f;
    [SerializeField] private int apocalipsisAttacks = 10;
    [SerializeField] private float attackInterval = 1f;

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Animator animator;
    [SerializeField] private GameObject fireTrailPrefab;
    [SerializeField] private ParticleSystem armorGlowEffect;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sodomaChargeSound;
    [SerializeField] private AudioClip sodomaAttackSound;
    [SerializeField] private AudioClip apocalipsisSound;
    [SerializeField] private AudioClip critDmgSound;
    [SerializeField] private AudioClip normalDmgSound;
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private AudioClip stunnedSound;

    [Header("UI - Sliders (opcional)")]
    [SerializeField] private Slider firstLifeSlider;
    [SerializeField] private Image firstFillImage;

    [Header("Robo de vida para el jugador")]
    [SerializeField] private float healthStealAmount = 0f;

    [Header("Debug Options")]
    [SerializeField] private bool showDetailsOptions = false;

    #endregion

    #region Internal Variables

    private enum BossState
    {
        Idle,
        Chasing,
        Attacking,
        Stunned
    }

    private BossState currentState = BossState.Idle;
    private NavMeshAgent agent;
    private float lastAttackTime;
    private bool isVulnerableToCounter = false;

    private PlayerHealth playerHealth;
    private Coroutine bossAICoroutine;
    private Coroutine stunCoroutine;
    private Coroutine currentAttackCoroutine;
    private Coroutine currentCriticalDamageCoroutine;
    private Coroutine ensurePathCoroutine;

    #endregion

    #region Properties

    private void Start()
    {
        if (firstLifeSlider != null)
        {
            firstLifeSlider.maxValue = Mathf.Max(1, maxHealth);
            firstLifeSlider.minValue = 0;
            firstLifeSlider.value = Mathf.Clamp(currentHealth, 0, maxHealth);
            if (!firstLifeSlider.gameObject.activeSelf) firstLifeSlider.gameObject.SetActive(true);
        }

        if (firstFillImage != null)
        {
            firstFillImage.color = new Color(0.5f, 0f, 1f);
            if (!firstFillImage.gameObject.activeSelf) firstFillImage.gameObject.SetActive(true);
        }

        InitializeBoss();
        UpdateSlidersSafely();
    }

    private void Update()
    {
        if (bossAICoroutine == null && currentHealth > 0f)
        {
            bossAICoroutine = StartCoroutine(BossAI());
            ReportDebug("BossAI re-lanzada desde Update (auto-recover).", 2);
        }
    }

    private void InitializeBoss()
    {
        currentHealth = maxHealth;
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
        agent.stoppingDistance = sodomaRange - 1f;

        if (player == null) player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player != null) playerHealth = player.GetComponent<PlayerHealth>();

        if (bossAICoroutine == null) bossAICoroutine = StartCoroutine(BossAI());
    }

    #endregion

    #region AI System

    private IEnumerator BossAI()
    {
        while (currentHealth > 0)
        {
            if (currentState == BossState.Stunned)
            {
                yield return null;
                continue;
            }

            if (player == null)
            {
                if (GameObject.FindGameObjectWithTag("Player") != null)
                {
                    player = GameObject.FindGameObjectWithTag("Player").transform;
                }
                else
                {
                    yield return new WaitForSeconds(0.5f);
                    continue;
                }
            }

            float distanceToPlayer = Vector3.Distance(transform.position, player.position);

            if (distanceToPlayer > agent.stoppingDistance)
            {
                ChasePlayer();
            }
            else
            {
                if (agent.velocity.magnitude > 0.1f)
                {
                    agent.ResetPath();
                    if (animator != null) animator.SetBool("IsMoving", false);
                }

                if (currentState != BossState.Attacking)
                {
                    if (Time.time > lastAttackTime + sodomaCooldown)
                    {
                        currentAttackCoroutine = StartCoroutine(ExecuteSodomaYGomorra());
                        yield return currentAttackCoroutine;
                    }
                    else
                    {
                        currentAttackCoroutine = StartCoroutine(ExecuteApocalipsis());
                        yield return currentAttackCoroutine;
                    }
                }
            }

            yield return new WaitForSeconds(0.2f);
        }
    }

    private void ChasePlayer()
    {
        currentState = BossState.Chasing;
        agent.isStopped = false;
        agent.SetDestination(player.position);
        if (animator != null) animator.SetBool("IsMoving", true);
    }

    #endregion

    #region Attacks

    private void StopCurrentAttackCoroutine()
    {
        if (currentAttackCoroutine != null)
        {
            try
            {
                StopCoroutine(currentAttackCoroutine);
            }
            catch
            { 
                /* defensivo: en caso de que ya esté detenido */ 
            }
            
            currentAttackCoroutine = null;
        }
    }

    private IEnumerator ExecuteSodomaYGomorra()
    {
        ReportDebug("El Blood Knight está ejecutando 'Sodoma y Gomorra'.", 1);

        currentState = BossState.Attacking;
        agent.isStopped = true;
        lastAttackTime = Time.time;

        Vector3 startDashPosition = transform.position;

        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        transform.rotation = Quaternion.LookRotation(new Vector3(directionToPlayer.x, 0, directionToPlayer.z));

        Vector3 backwardPos = transform.position - transform.forward * 3f;
        float startSlideDuration = 0.5f;

        yield return MoveToPosition(backwardPos, startSlideDuration);

        if (animator != null) animator.SetTrigger("ChargeSodoma");
        if (armorGlowEffect != null) armorGlowEffect.Play();
        if (audioSource != null && sodomaChargeSound != null) audioSource.PlayOneShot(sodomaChargeSound);

        isVulnerableToCounter = true;

        yield return new WaitForSeconds(1.5f);

        isVulnerableToCounter = false;

        if (armorGlowEffect != null) armorGlowEffect.Stop();

        ReportDebug("El Blood Knight ha terminado de cargar 'Sodoma y Gomorra'.", 1);

        if (currentState == BossState.Stunned) yield break;

        if (animator != null) animator.SetTrigger("ExecuteSodoma");
        if (audioSource != null && sodomaAttackSound != null) audioSource.PlayOneShot(sodomaAttackSound);

        Vector3 endDashPosition = player.position;
        endDashPosition.y = transform.position.y;
        float endDashDuration = 0.5f;

        yield return MoveToPosition(endDashPosition, endDashDuration);

        SpawnFireTrail(startDashPosition, endDashPosition);

        float distance = Vector3.Distance(transform.position, player.position);
        Vector3 dirToPlayer = (player.position - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, dirToPlayer);

        if (distance < sodomaRange && angle < 90f)
        {
            if (playerHealth != null) playerHealth.TakeDamage(sodomaDamage);
        }

        yield return new WaitForSeconds(2f);
        currentState = BossState.Idle;
        agent.isStopped = false;

        ReportDebug("El Blood Knight ha terminado 'Sodoma y Gomorra'.", 1);
    }

    private IEnumerator ExecuteApocalipsis()
    {
        ReportDebug("El Blood Knight está ejecutando 'Apocalipsis'.", 1);

        currentState = BossState.Attacking;
        agent.isStopped = true;
        lastAttackTime = Time.time;

        if (animator != null) animator.SetTrigger("StartApocalipsis");

        for (int i = 0; i < apocalipsisAttacks; i++)
        {
            Vector3 targetPos = player.position + (Quaternion.Euler(0, Random.Range(0, 360), 0) * Vector3.forward * 5f);
            targetPos.y = transform.position.y;

            yield return MoveToPosition(targetPos, 0.3f);

            Vector3 directionToPlayer = (player.position - transform.position).normalized;
            transform.rotation = Quaternion.LookRotation(new Vector3(directionToPlayer.x, 0, directionToPlayer.z));

            if (animator != null) animator.SetTrigger(i % 2 == 0 ? "Thrust" : "Slash");
            if (audioSource != null && apocalipsisSound != null) audioSource.PlayOneShot(apocalipsisSound);

            if (Vector3.Distance(transform.position, player.position) < 4f)
            {
                if (playerHealth != null) playerHealth.TakeDamage(apocalipsisDamage);
            }

            yield return new WaitForSeconds(attackInterval);
        }

        yield return new WaitForSeconds(1.5f);
        currentState = BossState.Idle;
        agent.isStopped = false;

        ReportDebug("El Blood Knight ha terminado 'Apocalipsis'.", 1);
    }

    #endregion

    #region Utility Coroutines & Counter-Attack

    private IEnumerator MoveToPosition(Vector3 target, float duration)
    {
        agent.enabled = false;

        float time = 0;
        Vector3 startPosition = transform.position;

        while (time < duration)
        {
            transform.position = Vector3.Lerp(startPosition, target, time / duration);
            time += Time.deltaTime;
            yield return null;
        }
        transform.position = target;

        ResynchronizeAgent();
    }

    private void SpawnFireTrail(Vector3 startPoint, Vector3 endPoint)
    {
        GameObject trailObject = Instantiate(fireTrailPrefab, startPoint, Quaternion.identity);

        Vector3 centerPoint = (startPoint + endPoint) / 2f;
        trailObject.transform.position = centerPoint;

        Vector3 direction = endPoint - startPoint;
        trailObject.transform.rotation = Quaternion.LookRotation(direction);

        float distance = direction.magnitude;
        trailObject.transform.localScale = new Vector3(trailObject.transform.localScale.x, trailObject.transform.localScale.y, distance);

        FireTrail fireTrailScript = trailObject.GetComponent<FireTrail>();
        if (fireTrailScript != null)
        {
            fireTrailScript.DamagePerSecond = fireTrailDamage;
        }
    }

    public void OnPlayerCounterAttack()
    {
        if (!isVulnerableToCounter) return;
        if (stunCoroutine != null)
        {
            ReportDebug("Stun ya en progreso: ignorando nuevo stun.", 1);
            return;
        }

        stunCoroutine = StartCoroutine(GetStunned());

        ReportDebug("El Blood Knight ha sido contraatacado y se intentará aturdir.", 1);
    }

    private IEnumerator GetStunned()
    {
        DumpState("EnterStun");
        DumpPathInfo("EnterStun");
        ReportDebug("¡El Blood Knight ha sido aturdido por el contraataque del jugador!", 1);

        //currentState = BossState.Stunned;
        //isVulnerableToCounter = false;

        //StopCurrentAttackCoroutine();

        //if (agent.enabled) agent.isStopped = true;

        if (animator != null) animator.SetTrigger("Stunned");
        if (armorGlowEffect != null) armorGlowEffect.Stop();
        if (audioSource != null && stunnedSound != null) audioSource.PlayOneShot(stunnedSound);

        float timer = 0f;
        while (timer < stunDuration)
        {
            if (currentHealth <= 0f) break;
            timer += Time.deltaTime;
            yield return null;
        }

        //if (bossAICoroutine == null) bossAICoroutinse = StartCoroutine(BossAI());

        //ResynchronizeAgent();
        //currentState = BossState.Idle;
        //lastAttackTime = Time.time;

        DumpState("ExitStun");
        DumpPathInfo("ExitStun");
        stunCoroutine = null;

        ReportDebug("El Blood Knight se ha recuperado del aturdimiento.", 1);
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

    #region Health System

    public void TakeDamage(float damageAmount, bool isCritical = false)
    {
        if (audioSource != null)
        {
            if (isCritical && critDmgSound != null) audioSource.PlayOneShot(critDmgSound);
            else if (normalDmgSound != null) audioSource.PlayOneShot(normalDmgSound);
        }

        currentHealth -= damageAmount;

        if (currentHealth <= 0) Die();

        UpdateSlidersSafely();

        if (Mathf.RoundToInt(currentHealth) % 10 == 0) ReportDebug($"El jugador ha recibido {damageAmount} de daño. Vida actual: {currentHealth}/{maxHealth}", 1);

        var bloodKnightVisualEffects = GetComponent<BloodKnightVisualEffects>();

        if (bloodKnightVisualEffects != null)
        {
            bloodKnightVisualEffects.ShowDamageNumber(transform.position + Vector3.up * 4f, damageAmount, isCritical);

            if (isCritical)
            {
                ReportDebug("El Blood Knight ha recibido daño crítico.", 1);

                bloodKnightVisualEffects.StartArmorGlow();
                if (currentCriticalDamageCoroutine != null) StopCoroutine(currentCriticalDamageCoroutine);
                currentCriticalDamageCoroutine = StartCoroutine(StopGlowAfterDelay(2f));
            }
        }
    }

    private IEnumerator StopGlowAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        var bloodKnightVisualEffects = GetComponent<BloodKnightVisualEffects>();
        if (bloodKnightVisualEffects != null)
        {
            bloodKnightVisualEffects.StopArmorGlow();
        }
    }

    private void UpdateSlidersSafely()
    {
        if (firstLifeSlider != null)
        {
            firstLifeSlider.maxValue = Mathf.Max(1, maxHealth);
            firstLifeSlider.value = Mathf.Clamp(currentHealth, 0, maxHealth);
            if (!firstLifeSlider.gameObject.activeSelf) firstLifeSlider.gameObject.SetActive(true);
        }
        if (firstFillImage != null)
        {
            if (!firstFillImage.gameObject.activeSelf) firstFillImage.gameObject.SetActive(true);
            firstFillImage.color = new Color(0.5f, 0f, 1f);
        }
    }

    private void Die()
    {
        if (audioSource != null && deathSound != null) audioSource.PlayOneShot(deathSound);

        StopAllCoroutines();
        currentState = BossState.Idle;
        agent.isStopped = true;
        if (animator != null) animator.SetTrigger("Death");
        Destroy(gameObject, 5f);

        ReportDebug("El Blood Knight ha sido derrotado.", 1);
    }

    private void OnDestroy()
    {
        if (playerHealth != null && healthStealAmount > 0f)
        {
            playerHealth.Heal(healthStealAmount);
            ReportDebug($"El jugador ha robado {healthStealAmount} de vida al enemigo.", 1);
        }
    }

    #endregion

    #region Debug

    private void DumpState(string context)
    {
        float dist = player != null ? Vector3.Distance(transform.position, player.position) : -1f;
        ReportDebug($"{context} | state:{currentState} | bossAICoroutine:{bossAICoroutine != null} | stunCoroutine:{stunCoroutine != null} | agent.enabled:{(agent != null ? agent.enabled : false)} | hasPath:{(agent != null ? agent.hasPath : false)} | isStopped:{(agent != null ? agent.isStopped : false)} | vel:{(agent != null ? agent.velocity.magnitude : 0f)} | dist:{dist:F2}", 1);
    }

    private void DumpPathInfo(string context)
    {
        if (agent == null)
        {
            ReportDebug($"{context} | agent null", 1);
            return;
        }

        ReportDebug($"{context} | hasPath:{agent.hasPath} | pathPending:{agent.pathPending} | pathStatus:{agent.pathStatus} | remaining:{agent.remainingDistance:F2} | nextPos:{agent.nextPosition} | baseOffset:{agent.baseOffset}", 1);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, sodomaRange); // Sodoma y Gomorra range

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 4f); // Apocalipsis range

        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, 2.5f); // Close range for Apocalipsis damage

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 4f); // Line to show height for damage numbers

        Gizmos.color = Color.magenta;
        Gizmos.DrawRay(transform.position, transform.forward * 3f); // Forward direction
    }

    private void OnGUI()
    {
        if (!showDetailsOptions) return;

        GUI.Label(new Rect(10, 10, 300, 20), $"Estado Actual: {currentState}");
        GUI.Label(new Rect(10, 30, 300, 20), $"Vida Actual: {currentHealth}/{maxHealth}");
        GUI.Label(new Rect(10, 50, 300, 20), $"Vulnerable a Contraataque: {isVulnerableToCounter}");
        if (currentState == BossState.Attacking && currentAttackCoroutine != null)
        {
            GUI.Label(new Rect(10, 70, 300, 20), $"Atacando...");
        }
        else
        {
            GUI.Label(new Rect(10, 70, 300, 20), $"No Atacando");
        }

        if (currentState == BossState.Stunned)
        {
            GUI.Label(new Rect(10, 90, 300, 20), $"Aturdido");
        }
        else
        {
            GUI.Label(new Rect(10, 90, 300, 20), $"No Aturdido");
        }

        GUI.Label(new Rect(10, 110, 300, 20), $"Último Ataque: {lastAttackTime:F2}s");

        GUI.Label(new Rect(10, 130, 300, 20), $"Distancia al Jugador: {Vector3.Distance(transform.position, player.position):F2} unidades");
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