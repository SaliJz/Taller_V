using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// EnemyHealth: conserva todo lo anterior y a�ade:
/// - ApplyDamageReduction(percent, duration) para reducci�n local.
/// - Monitor de vida que al caer <=25% activa armadura de �rea (aplica reducci�n a s� mismo y aliados cercanos).
/// - ForceActivateArea() para pruebas.
/// Cambios m�nimos en API para mantener compatibilidad.
/// </summary>
public class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("Health statistics")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;
    [SerializeField] private float deathCooldown = 2f;
    [SerializeField] private bool canDestroy = false;

    [Header("Health Stealing Mechanics")]
    [SerializeField] private float healthSteal = 5;

    [Header("UI - Sliders (optional)")]
    [SerializeField] private Slider firstLifeSlider;
    [SerializeField] private Image firstFillImage;
    [SerializeField] private float offsetAboveEnemy = 2f;
    [SerializeField] private float glowDelayAfterCritical = 2f;

    private bool isDead = false;
    private Coroutine currentCriticalDamageCoroutine;

    public float CurrentHealth
    {
        get
        { 
            return currentHealth; 
        }
        private set
        {
            if (!Mathf.Approximately(currentHealth, value))
            {
                currentHealth = value;
                OnEnemyHealthChanged?.Invoke(currentHealth, maxHealth);
            }
        }
    }

    public bool IsDead
    { 
        get 
        { 
            return isDead; 
        }
        private set
        {
            isDead = value;
        }
    }

    public bool CanDestroy 
    { 
        get 
        { 
            return canDestroy; 
        }
        set
        {
            canDestroy = value;
        }
    }

    public float MaxHealth => maxHealth;
    public static event Action<float, float> OnEnemyHealthChanged;
    public event Action<GameObject> OnDeath;
    private EnemyVisualEffects enemyVisualEffects;
    private Transform playerTransform;
    private PlayerHealth playerHealth;

    #region --- Armadura de �rea (configurable) ---
    [Header("Armadura Demon�aca (Auto)")]
    [Tooltip("Si true, este componente intentar� activar la armadura de �rea cuando la vida <= areaTriggerPercent.")]
    [SerializeField] private bool enableAutoArea = true;
    [SerializeField, Range(0f, 1f)] private float areaTriggerPercent = 0.25f; // 25%
    [SerializeField, Range(0f, 1f)] private float areaReductionPercent = 0.25f; // 25% reducci�n
    [SerializeField] private float areaDuration = 10f;
    [SerializeField] private float areaCooldown = 4.5f;
    [SerializeField] private float areaRadius = 8f;
    [SerializeField] private LayerMask areaLayers = ~0;
    [SerializeField] private float flattenHeightThreshold = 1.2f;
    [SerializeField] private float areaCheckInterval = 0.25f;

    // runtime
    private bool areaActive = false;
    private bool areaOnCooldown = false;
    private Coroutine areaCoroutine;
    #endregion

    #region --- Reducci�n local (aplicable por armadura propia) ---
    // campo interno que afecta TakeDamage
    private float _reduccionLocal = 0f;
    private Coroutine _reduccionLocalRoutine;
    #endregion

    private void Awake()
    {
        enemyVisualEffects = GetComponent<EnemyVisualEffects>();

        if (enemyVisualEffects == null)
        {
            ReportDebug("Componente EnemyVisualEffects no encontrado en el enemigo.", 2);
        }

        // asegurar inicializaci�n de currentHealth (si no se configur�)
        currentHealth = Mathf.Clamp(currentHealth > 0f ? currentHealth : maxHealth, 0f, maxHealth);
    }

    private void Start()
    {
        var playerGameObject = GameObject.FindGameObjectWithTag("Player");
        playerTransform = playerGameObject ? playerGameObject.transform : null;
        if (playerTransform == null) ReportDebug("Jugador no encontrado en la escena.", 3);
        else playerTransform.TryGetComponent(out playerHealth);

        // arrancar monitor de vida si est� activado
        if (enableAutoArea)
        {
            StartCoroutine(HealthMonitorForArea());
        }

        // emitir estado inicial
        OnEnemyHealthChanged?.Invoke(currentHealth, maxHealth);
        UpdateSlidersSafely();
    }

    private void OnDestroy()
    {
        if (isDead) StopAllCoroutines();
    }

    public void SetMaxHealth(float health)
    {
        maxHealth = health;
        currentHealth = maxHealth;
        UpdateSlidersSafely();
    }

    /// <summary>
    /// Toma da�o (respetando la reducci�n local si existe).
    /// Firma original respetada para compatibilidad.
    /// </summary>
    public void TakeDamage(float damageAmount, bool isCritical = false)
    {
        if (currentHealth <= 0) return;

        // aplicar reducci�n local si existe
        float finalDamage = damageAmount;
        if (_reduccionLocal > 0f)
        {
            finalDamage = finalDamage * (1f - _reduccionLocal);
        }

        currentHealth -= finalDamage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        // emitir cambio de vida para listeners
        CurrentHealth = currentHealth;

        UpdateSlidersSafely();

        if (Mathf.RoundToInt(currentHealth) % 10 == 0) ReportDebug($"El enemigo ha recibido {finalDamage} de da�o. Vida actual: {currentHealth}/{maxHealth}", 1);

        if (enemyVisualEffects != null)
        {
            enemyVisualEffects.PlayDamageFeedback(transform.position + Vector3.up * offsetAboveEnemy, finalDamage, isCritical);

            if (isCritical)
            {
                ReportDebug("El enemigo ha recibido da�o cr�tico.", 1);

                enemyVisualEffects.StartArmorGlow();

                if (currentCriticalDamageCoroutine != null) StopCoroutine(currentCriticalDamageCoroutine);
                currentCriticalDamageCoroutine = StartCoroutine(StopGlowAfterDelay(glowDelayAfterCritical));
            }
        }

        if (currentHealth <= 0)
        {
            CurrentHealth = 0;
            Die();
        }
    }

    private IEnumerator StopGlowAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (enemyVisualEffects != null)
        {
            enemyVisualEffects.StopArmorGlow();
        }
    }

    public void Heal(float healAmount)
    {
        if (currentHealth <= 0) return;

        currentHealth += healAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        CurrentHealth = currentHealth;

        ReportDebug($"{gameObject.name} ha sido curado por {healAmount}. Vida actual: {currentHealth}/{maxHealth}", 1);
        UpdateSlidersSafely();
    }

    public void Die()
    {
        isDead = true;

        ReportDebug($"{gameObject.name} ha muerto.", 1);
        OnDeath?.Invoke(gameObject);

        if (playerHealth != null)
        {
            playerHealth.Heal(healthSteal);
            ReportDebug($"El jugador ha robado {healthSteal} de vida al matar a {gameObject.name}.", 1);
        }

        if (!canDestroy) Destroy(gameObject, deathCooldown);
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
            firstFillImage.color = Color.red;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, Vector3.up * offsetAboveEnemy);
#if UNITY_EDITOR
        // dibujar radio del �rea si est� activado en inspector
        if (enableAutoArea)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, areaRadius);
        }
#endif
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[EnemyHealth] {message}");
                break;
            case 2:
                Debug.LogWarning($"[EnemyHealth] {message}");
                break;
            case 3:
                Debug.LogError($"[EnemyHealth] {message}");
                break;
            default:
                Debug.Log($"[EnemyHealth] {message}");
                break;
        }
    }

    #region --- Reducci�n local p�blica (compatible con VidaEnemigoEscudo) ---
    /// <summary>
    /// Aplica una reducci�n de da�o local durante 'duration' segundos.
    /// Firma p�blica a�adida para compatibilidad con otros scripts (ArmaduraDemonicaArea, prefabs, etc.).
    /// </summary>
    public void ApplyDamageReduction(float percent, float duration)
    {
        if (percent <= 0f || duration <= 0f) return;

        // si ya hay rutina, reiniciar
        if (_reduccionLocalRoutine != null) StopCoroutine(_reduccionLocalRoutine);
        _reduccionLocalRoutine = StartCoroutine(RutinaReduccionLocal(percent, duration));
    }

    private IEnumerator RutinaReduccionLocal(float percent, float duration)
    {
        _reduccionLocal = percent;
        yield return new WaitForSeconds(duration);
        _reduccionLocal = 0f;
        _reduccionLocalRoutine = null;
    }
    #endregion

    #region --- Monitor y activaci�n de armadura de �rea ---
    private IEnumerator HealthMonitorForArea()
    {
        // esperar a que MaxHealth tenga sentido.
        while (maxHealth <= 0f)
            yield return null;

        while (true)
        {
            if (!areaActive && !areaOnCooldown)
            {
                float percent = currentHealth / Mathf.Max(1f, maxHealth);
                if (percent <= areaTriggerPercent)
                {
                    ActivateArea();
                }
            }
            yield return new WaitForSeconds(areaCheckInterval);
        }
    }

    /// <summary>
    /// Fuerza la activaci�n del efecto de �rea (p�blica para pruebas).
    /// </summary>
    public void ForceActivateArea()
    {
        if (areaCoroutine != null) StopCoroutine(areaCoroutine);
        areaOnCooldown = false;
        ActivateArea();
    }

    private void ActivateArea()
    {
        if (areaActive || areaOnCooldown) return;
        areaActive = true;

        // aplicarse localmente
        ApplyDamageReduction(areaReductionPercent, areaDuration);

        // aplicar a aliados cercanos: priorizamos VidaEnemigoEscudo, fallback a EnemyHealth
        Collider[] hits = Physics.OverlapSphere(transform.position, areaRadius, areaLayers, QueryTriggerInteraction.Ignore);
        foreach (var c in hits)
        {
            if (c == null) continue;
            GameObject root = c.transform.root != null ? c.transform.root.gameObject : c.gameObject;
            if (root == this.gameObject) continue;

            // comprobaci�n de "chancado" por diferencia Y
            if (Mathf.Abs(root.transform.position.y - transform.position.y) > flattenHeightThreshold) continue;

            // 1) intentar VidaEnemigoEscudo (tu script espa�ol)
            var vidaEscudo = root.GetComponent<VidaEnemigoEscudo>();
            if (vidaEscudo != null)
            {
                try { vidaEscudo.ApplyDamageReduction(areaReductionPercent, areaDuration); }
                catch { }
                continue;
            }

            // 2) intentar EnemyHealth (este mismo tipo)
            var enemyH = root.GetComponent<EnemyHealth>();
            if (enemyH != null)
            {
                try { enemyH.ApplyDamageReduction(areaReductionPercent, areaDuration); }
                catch { }
                continue;
            }

            // 3) fallback SendMessage (no rompe si no existe)
            try
            {
                root.SendMessage("ApplyDamageReduction", new object[] { areaReductionPercent, areaDuration }, SendMessageOptions.DontRequireReceiver);
            }
            catch { }
        }

        areaCoroutine = StartCoroutine(AreaDurationAndCooldownRoutine());
        Debug.Log($"[{name}] Armadura de �rea ACTIVADA. Radio={areaRadius} Reducci�n={(areaReductionPercent * 100f)}%");
    }

    private IEnumerator AreaDurationAndCooldownRoutine()
    {
        yield return new WaitForSeconds(areaDuration);
        // finalizar efecto local en este objeto (si a�n activo)
        _reduccionLocal = 0f;
        areaActive = false;

        // iniciar cooldown
        areaOnCooldown = true;
        yield return new WaitForSeconds(areaCooldown);
        areaOnCooldown = false;

        areaCoroutine = null;
        Debug.Log($"[{name}] Armadura de �rea COOLDOWN finalizado.");
    }
    #endregion
}


