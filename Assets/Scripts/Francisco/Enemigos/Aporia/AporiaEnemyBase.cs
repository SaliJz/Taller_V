using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(EnemyHealth))]
public abstract class AporiaEnemyBase : MonoBehaviour
{
    #region Headers Comunes
    [Header("Referencias")]
    [SerializeField] protected AporiaAnimCtrl animCtrl;
    [SerializeField] protected Transform hitPoint;
    [SerializeField] protected GameObject groundIndicator;

    [Header("Patrullaje (Wander)")]
    [SerializeField] protected float wanderRadius = 8f;
    [SerializeField] protected float wanderWaitTime = 3f;
    protected float wanderTimer;

    [Header("Estadísticas Base")]
    [SerializeField] protected float health = 35f;
    [SerializeField] protected float moveSpeed = 6.5f;

    [Header("Dash Errático")]
    [SerializeField] protected float dashDuration = 0.4f;
    [SerializeField] protected float dashMaxDistance = 10f;
    [SerializeField] protected float preparationTime = 0.25f;
    [SerializeField] protected float attackTransitionDelay = 0.1f;

    [Header("Ataque Base")]
    [SerializeField] protected float attackDamage = 30f;
    [SerializeField] protected float attackRadius = 2.5f;
    [SerializeField] protected float hitDelay = 0.35f;
    [SerializeField] protected float recoveryTime = 1.2f;
    [SerializeField] protected float knockbackForce = 6f;
    [SerializeField] protected LayerMask playerLayer;

    [Header("Percepción")]
    [SerializeField] protected float detectionRadius = 15f;
    [SerializeField] protected float dashActivationDistance = 10f;

    [Header("Cooldowns")]
    [SerializeField] protected float cooldownShort = 1.0f;
    [SerializeField] protected float cooldownMedium = 1.5f;
    [SerializeField] protected float cooldownLong = 2.0f;

    [Header("Sonido")]
    [SerializeField] protected AudioSource audioSource;
    [SerializeField] protected AudioClip dashSFX;
    [SerializeField] protected AudioClip attackSFX;
    [SerializeField] protected AudioClip damageSFX;
    [SerializeField] protected AudioClip deathSFX;

    [Header("Capas")]
    [SerializeField] protected LayerMask groundLayer = ~0;
    #endregion

    #region Variables Protegidas
    protected EnemyHealth enemyHealth;
    protected NavMeshAgent agent;
    protected Transform playerTransform;
    protected bool isAttacking = false;
    protected float attackTimer;
    protected float currentCooldown;
    #endregion

    #region Unity Lifecycle
    protected virtual void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        agent = GetComponent<NavMeshAgent>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

        InitializeEnemy();
        SetupPools();

        if (groundIndicator) groundIndicator.SetActive(false);
    }

    protected virtual void Start()
    {
        var pObj = GameObject.FindGameObjectWithTag("Player");
        if (pObj) playerTransform = pObj.transform;

        currentCooldown = cooldownMedium;
        wanderTimer = wanderWaitTime;

        SpriteAnimator animator = GetComponentInChildren<SpriteAnimator>();
        if (animator != null) animator.onAnimEvent += HandleAnimEvents;
    }

    protected virtual void OnEnable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath += HandleDeath;
            enemyHealth.OnDamaged += PlayDamageSFX;
        }
    }

    protected virtual void OnDisable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleDeath;
            enemyHealth.OnDamaged -= PlayDamageSFX;
        }
    }

    protected virtual void Update()
    {
        if (isAttacking || (enemyHealth != null && (enemyHealth.IsStunned || enemyHealth.IsDead))) return;

        HandleLocomotion();

        float dist = playerTransform != null
            ? Vector3.Distance(transform.position, playerTransform.position)
            : float.MaxValue;

        if (dist <= detectionRadius)
        {
            agent.SetDestination(playerTransform.position);
            attackTimer += Time.deltaTime;

            if (attackTimer >= currentCooldown)
            {
                if (dist <= attackRadius)
                    StartCoroutine(ExecuteFullAttackSequence(false));
                else if (dist <= dashActivationDistance)
                    StartCoroutine(ExecuteFullAttackSequence(true));
            }
        }
        else
        {
            HandlePatrol();
        }
    }
    #endregion

    #region Rutinas de Combate
    protected virtual IEnumerator ExecuteFullAttackSequence(bool useDash)
    {
        isAttacking = true;
        attackTimer = 0f;

        if (agent.enabled)
        {
            agent.isStopped = true;
            agent.updatePosition = false;
        }

        Vector3 attackDirection = (playerTransform.position - transform.position).normalized;
        attackDirection.y = 0;
        if (attackDirection == Vector3.zero) attackDirection = transform.forward;
        transform.forward = attackDirection;

        if (useDash)
        {
            yield return new WaitForSeconds(preparationTime);

            if (animCtrl) animCtrl.PlayDash();
            if (audioSource && dashSFX) audioSource.PlayOneShot(dashSFX);

            Vector3 startPos = transform.position;
            Vector3 dashEnd = startPos + attackDirection * dashMaxDistance;

            if (NavMesh.Raycast(startPos, dashEnd, out NavMeshHit hit, NavMesh.AllAreas))
                dashEnd = hit.position;

            yield return StartCoroutine(PerformDash(startPos, dashEnd, attackDirection));

            if (attackTransitionDelay > 0f)
                yield return new WaitForSeconds(attackTransitionDelay);
        }
        else
        {
            yield return new WaitForSeconds(preparationTime);
        }

        yield return StartCoroutine(PerformAttack(attackDirection));

        if (agent.enabled)
        {
            agent.Warp(transform.position);
            agent.updatePosition = true;
            agent.isStopped = false;
        }

        isAttacking = false;
        currentCooldown = GetRandomCooldown();
    }

    protected virtual IEnumerator PerformDash(Vector3 startPos, Vector3 dashEnd, Vector3 attackDirection)
    {
        float elapsed = 0;
        while (elapsed < dashDuration)
        {
            elapsed += Time.deltaTime;
            Vector3 nextPos = Vector3.Lerp(startPos, dashEnd, elapsed / dashDuration);

            if (Physics.Raycast(nextPos + Vector3.up, Vector3.down, 2f, groundLayer))
                transform.position = nextPos;

            if (Vector3.Distance(transform.position, playerTransform.position) < 1.2f)
                break;

            yield return null;
        }
    }

    protected virtual IEnumerator PerformAttack(Vector3 frozenDirection)
    {
        if (groundIndicator)
        {
            groundIndicator.transform.position = hitPoint.position;
            groundIndicator.SetActive(true);
        }

        if (animCtrl)
            animCtrl.SendMessage("PlayAttack", SendMessageOptions.DontRequireReceiver);

        animHitFired = false;
        yield return new WaitForSeconds(hitDelay);

        if (groundIndicator) groundIndicator.SetActive(false);

        if (!animHitFired) OnAttackHit();

        yield return new WaitForSeconds(Mathf.Max(0, recoveryTime - hitDelay));
    }

    private bool animHitFired = false;

    public virtual void OnAttackHit()
    {
        if (audioSource && attackSFX) audioSource.PlayOneShot(attackSFX);

        Collider[] targets = Physics.OverlapSphere(hitPoint.position, attackRadius, playerLayer);
        foreach (var t in targets)
        {
            if (t.TryGetComponent<PlayerHealth>(out var pHealth))
            {
                pHealth.TakeDamage(attackDamage);
                ApplyKnockback(t.transform);
            }
        }
    }
    #endregion

    #region Muerte
    protected virtual void HandleDeath(GameObject e)
    {
        if (e != gameObject) return;
        ResetDamageFlash();
        if (audioSource && deathSFX) audioSource.PlayOneShot(deathSFX);
        animCtrl?.PlayDeath();
        agent.enabled = false;
        this.enabled = false;
    }
    #endregion

    #region Inicialización y Pools
    protected virtual void InitializeEnemy()
    {
        if (enemyHealth != null) enemyHealth.SetMaxHealth(health);
        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.stoppingDistance = attackRadius;
        }
    }

    protected virtual void SetupPools() { }
    #endregion

    #region Utilidades Comunes
    protected float GetRandomCooldown()
        => new float[] { cooldownShort, cooldownMedium, cooldownLong }[Random.Range(0, 3)];

    protected void HandleLocomotion()
    {
        if (animCtrl == null || agent == null || !agent.enabled) return;

        Vector3 moveDir = isAttacking ? transform.forward : agent.velocity;
        if (moveDir.sqrMagnitude < 0.01f) { animCtrl.h = 0; animCtrl.v = 0; return; }

        float angle = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg - 45f;
        if (angle < 0) angle += 360f;

        if (angle < 22.5f || angle >= 337.5f) { animCtrl.h = 0; animCtrl.v = 1; }
        else if (angle < 67.5f) { animCtrl.h = 1; animCtrl.v = 1; }
        else if (angle < 112.5f) { animCtrl.h = 1; animCtrl.v = 0; }
        else if (angle < 157.5f) { animCtrl.h = 1; animCtrl.v = -1; }
        else if (angle < 202.5f) { animCtrl.h = 0; animCtrl.v = -1; }
        else if (angle < 247.5f) { animCtrl.h = -1; animCtrl.v = -1; }
        else if (angle < 292.5f) { animCtrl.h = -1; animCtrl.v = 0; }
        else { animCtrl.h = -1; animCtrl.v = 1; }
    }

    protected void HandlePatrol()
    {
        wanderTimer += Time.deltaTime;
        if (wanderTimer >= wanderWaitTime)
        {
            Vector3 randomDir = (Random.insideUnitSphere * wanderRadius) + transform.position;
            if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, wanderRadius, -1))
                agent.SetDestination(hit.position);
            wanderTimer = 0f;
        }
    }

    protected void ApplyKnockback(Transform target)
    {
        Vector3 dir = (target.position - transform.position).normalized;
        dir.y = 0;
        if (target.TryGetComponent<CharacterController>(out var cc))
            StartCoroutine(KnockbackTick(cc, dir * knockbackForce));
    }

    private IEnumerator KnockbackTick(CharacterController cc, Vector3 force)
    {
        float t = 0;
        while (t < 0.2f) { t += Time.deltaTime; cc?.Move(force * Time.deltaTime); yield return null; }
    }

    protected IEnumerator DeactivateAfterDelay(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (obj != null) obj.SetActive(false);
    }

    protected void PlayDamageSFX()
    {
        if (audioSource && damageSFX) audioSource.PlayOneShot(damageSFX);
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            if (cachedSpriteRenderer) cachedSpriteRenderer.material.SetFloat("_Amount", 0f);
        }
        flashCoroutine = StartCoroutine(DamageFlash());
    }

    private Coroutine flashCoroutine;
    private SpriteRenderer cachedSpriteRenderer;

    private void CacheSpriteRenderer()
    {
        if (cachedSpriteRenderer != null) return;
        cachedSpriteRenderer = animCtrl?.GetComponent<SpriteRenderer>();
    }

    private IEnumerator DamageFlash()
    {
        CacheSpriteRenderer();
        if (!cachedSpriteRenderer) yield break;

        cachedSpriteRenderer.material.SetFloat("_Amount", 1f);
        yield return new WaitForSeconds(0.15f);
        cachedSpriteRenderer.material.SetFloat("_Amount", 0f);
        flashCoroutine = null;
    }

    private void ResetDamageFlash()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }
        CacheSpriteRenderer();
        if (cachedSpriteRenderer) cachedSpriteRenderer.material.SetFloat("_Amount", 0f);
    }

    protected virtual void HandleAnimEvents(string eventName)
    {
        if (eventName == "OnAttackHit")
        {
            animHitFired = true;
            OnAttackHit();
        }
    }
    #endregion

    #region Gizmos
#if UNITY_EDITOR
    protected virtual void OnDrawGizmos()
    {
        Vector3 center = transform.position + Vector3.up * 0.1f;
        float radius = 1.5f;

        UnityEditor.Handles.color = new Color(1f, 1f, 1f, 0.15f);
        UnityEditor.Handles.DrawWireDisc(center, Vector3.up, radius);

        string[] labels = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        float[] angles = { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f };

        for (int i = 0; i < 8; i++)
        {
            float divAngle = (angles[i] - 22.5f) * Mathf.Deg2Rad;
            Vector3 divDir = new Vector3(Mathf.Sin(divAngle), 0f, Mathf.Cos(divAngle));
            Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
            Gizmos.DrawLine(center, center + divDir * radius);

            float labelAngle = angles[i] * Mathf.Deg2Rad;
            Vector3 labelPos = center + new Vector3(Mathf.Sin(labelAngle), 0f, Mathf.Cos(labelAngle)) * (radius + 0.35f);
            UnityEditor.Handles.Label(labelPos, labels[i]);
        }

        if (Application.isPlaying && agent != null && agent.enabled)
        {
            Vector3 vel = agent.velocity;
            if (vel.sqrMagnitude > 0.01f)
            {
                Vector3 dir = new Vector3(vel.x, 0f, vel.z).normalized;
                Gizmos.color = Color.green;
                Gizmos.DrawLine(center, center + dir * radius);
                Gizmos.DrawSphere(center + dir * radius, 0.08f);
            }
        }
    }

    protected virtual void OnGUI()
    {
        if (!Application.isPlaying) return;
        string dirLabel = GetDirectionLabel();
        if (string.IsNullOrEmpty(dirLabel)) return;

        Vector3 worldPos = transform.position + Vector3.up * 2.5f;
        Vector3 screen = Camera.main != null ? Camera.main.WorldToScreenPoint(worldPos) : Vector3.zero;
        if (screen.z <= 0) return;

        GUI.color = Color.yellow;
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        Vector2 size = new Vector2(80, 20);
        Rect rect = new Rect(screen.x - size.x * 0.5f, Screen.height - screen.y - size.y * 0.5f, size.x, size.y);
        GUI.Label(rect, dirLabel, style);
    }

    private string GetDirectionLabel()
    {
        if (agent == null || !agent.enabled) return "";
        Vector3 vel = agent.velocity;
        if (vel.sqrMagnitude < 0.01f) return "IDLE";

        float angle = Mathf.Atan2(vel.x, vel.z) * Mathf.Rad2Deg - 45f;
        if (angle < 0) angle += 360f;

        if (angle < 22.5f || angle >= 337.5f) return "UP";
        else if (angle < 67.5f) return "UP RIGHT";
        else if (angle < 112.5f) return "RIGHT";
        else if (angle < 157.5f) return "DOWN RIGHT";
        else if (angle < 202.5f) return "DOWN";
        else if (angle < 247.5f) return "DOWN LEFT";
        else if (angle < 292.5f) return "LEFT";
        else return "UP LEFT";
    }
#endif
    #endregion
}