using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Clase que gestiona la salud de un enemigo,
/// </summary>
public class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("Health statistics")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;
    [SerializeField] private float deathCooldown = 2f;
    [SerializeField] private bool canDestroy = true;
    [SerializeField] private UnityEvent onDeathEvent;

    [Header("UI - Sliders")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Image healthFillImage;
    [SerializeField] private float offsetAboveEnemy = 2f;
    [SerializeField] private float glowDelayAfterCritical = 2f;
    [SerializeField] private TextMeshProUGUI healthPercentageText;
    [SerializeField] private TextMeshProUGUI healthMultiplierText;

    [Header("Dynamic Bar Configuration")]
    [SerializeField] private bool useDynamicHealthBars = false;
    [SerializeField] private float healthPerBar = 100f;
    [SerializeField] private Color healthBaseColor = Color.red;
    [SerializeField, Range(0f, 1f)] private float colorGradientIntensity = 0.3f;

    [Header("Lifesteal Control")]
    [SerializeField] private bool canGrantLifestealOnDeath = true;

    [Header("Toughness System")]
    [SerializeField] private EnemyToughness toughnessSystem;

    private EnemyKnockbackHandler knockbackHandler;
    private PlayerStatsManager playerStatsManager;
    private EnemyAuraManager _auraManager;

    private float _initialHealthMultiplier = 1.0f; 
    private float auraDamageReduction = 0.0f;  

    private bool canHealPlayer = true;
    private bool isDead = false;
    private bool isStunned = false;
    private Renderer enemyRenderer; 
    private Color originalColor;
    private Coroutine stunCoroutine;
    private Coroutine currentCriticalDamageCoroutine;

    private int currentHealthBars;
    private int totalHealthBars;

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

    public bool CanHealPlayer
    {
        get { return canHealPlayer; }
        set { canHealPlayer = value; }
    }

    public bool IsDead
    {
        get { return isDead; }
        set { isDead = value; }
    }

    public bool CanDestroy
    {
        get { return canDestroy; }
        set { canDestroy = value; }
    }

    public float DeathCooldown
    {
        get { return deathCooldown; }
        set { deathCooldown = value; }
    }

    public float MaxHealth => maxHealth;
    public bool ItemEffectHandledDeath { get; set; } = false;
    public static event Action<float, float> OnEnemyHealthChanged;
    public Action<GameObject> OnDeath;
    public event Action OnDamaged;
    public event Action<float, float> OnHealthChanged;
    private EnemyVisualEffects enemyVisualEffects;
    private Transform playerTransform;
    private PlayerHealth playerHealth;

    #region --- Armadura de área (configurable) ---
    [Header("Armadura Demoníaca (Auto)")]
    [Tooltip("Si true, este componente intentará activar la armadura de área cuando la vida <= areaTriggerPercent.")]
    [SerializeField] private bool enableAutoArea = false;
    [SerializeField, Range(0f, 1f)] private float areaTriggerPercent = 0.25f; // 25%
    [SerializeField, Range(0f, 1f)] private float areaReductionPercent = 0.25f; // 25% reducción
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

    #region --- Reducción local (aplicable por armadura propia) ---
    // campo interno que afecta TakeDamage
    private float localReduction = 0f;
    private Coroutine _reduccionLocalRoutine;
    #endregion

    private void Awake()
    {
        enemyVisualEffects = GetComponent<EnemyVisualEffects>();

        if (enemyVisualEffects == null)
        {
            ReportDebug("Componente EnemyVisualEffects no encontrado en el enemigo.", 2);
        }

        toughnessSystem = GetComponent<EnemyToughness>();
        if (toughnessSystem != null)
        {
            ReportDebug("Sistema de dureza detectado.", 1);
        }

        enemyRenderer = GetComponentInChildren<Renderer>();
        if (enemyRenderer != null && enemyRenderer.material.HasProperty("_Color"))
        {
            originalColor = enemyRenderer.material.color;
        }

        currentHealth = maxHealth;

        _auraManager = GetComponent<EnemyAuraManager>();

        ApplyInitialHealth();

        // asegurar inicialización de currentHealth (si no se configuró)
        currentHealth = Mathf.Clamp(currentHealth > 0f ? currentHealth : maxHealth, 0f, maxHealth);

        knockbackHandler = GetComponent<EnemyKnockbackHandler>();

        InitializeHealthUI();
    }

    private void ApplyInitialHealth()
    {
        currentHealth = maxHealth * _initialHealthMultiplier;
        maxHealth *= _initialHealthMultiplier; 
    }

    public void SetInitialHealthMultiplier(float multiplier)
    {
        _initialHealthMultiplier = multiplier;
    }

    private void Start()
    {
        var playerGameObject = GameObject.FindGameObjectWithTag("Player");
        playerTransform = playerGameObject ? playerGameObject.transform : null;
        if (playerTransform == null) ReportDebug("Jugador no encontrado en la escena.", 3);
        else
        {
            playerTransform.TryGetComponent(out playerHealth);
            playerTransform.TryGetComponent(out playerStatsManager);
        }

        // arrancar monitor de vida si está activado
        if (enableAutoArea)
        {
            StartCoroutine(HealthMonitorForArea());
        }

        // emitir estado inicial
        OnEnemyHealthChanged?.Invoke(currentHealth, maxHealth);
        UpdateHealthUI();
    }

    private void OnDestroy()
    {
        if (isDead) StopAllCoroutines();
    }

    public void SetMaxHealth(float health)
    {   
        maxHealth = health;
        currentHealth = maxHealth;
        InitializeHealthUI();
    }

    /// <summary>
    /// Toma daño (respetando la reducción local si existe).
    /// Firma original respetada para compatibilidad.
    /// </summary>
    public void TakeDamage(float damageAmount, bool isCritical = false, AttackDamageType damageType = AttackDamageType.Melee)
    {
        if (currentHealth <= 0) return;

        float finalDamage = damageAmount;
        OnDamaged?.Invoke();

        // Procesar dureza
        if (toughnessSystem != null && toughnessSystem.HasToughness)
        {
            finalDamage = toughnessSystem.ProcessDamage(damageAmount, damageType);

            if (finalDamage <= 0)
            {
                // Todo el daño fue absorbido por la dureza
                ReportDebug($"Daño completamente absorbido por dureza. Tipo: {damageType}", 1);

                if (enemyVisualEffects != null)
                {
                    enemyVisualEffects.PlayToughnessHitFeedback(transform.position + Vector3.up * offsetAboveEnemy);
                }

                return;
            }
            else if (finalDamage < damageAmount)
            {
                ReportDebug($"Daño reducido por dureza: {damageAmount} -> {finalDamage}", 1);
            }
        }

        float damageReductionTotal = localReduction + auraDamageReduction; 
        finalDamage *= (1f - damageReductionTotal);

        currentHealth -= finalDamage;
        ReportDebug($"Dano recibido. Base: {damageAmount}. Reduccion total: {damageReductionTotal * 100}%. Dano Final: {finalDamage}. Vida Restante: {currentHealth}", 1);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        // emitir cambio de vida para listeners
        CurrentHealth = currentHealth;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        UpdateHealthUI();

        if (Mathf.RoundToInt(currentHealth) % 10 == 0) ReportDebug($"El enemigo ha recibido {finalDamage} de daño. Vida actual: {currentHealth}/{maxHealth}", 1);

        // Feedback visual/sonoro/numérico centralizado en EnemyVisualEffects
        if (enemyVisualEffects != null)
        {
            enemyVisualEffects.PlayDamageFeedback(transform.position + Vector3.up * offsetAboveEnemy, finalDamage, isCritical);

            if (isCritical)
            {
                ReportDebug("El enemigo ha recibido daño crítico.", 1);

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

    public void ApplyDamageReduction_Aura(float reductionPercent)
    {
        auraDamageReduction = reductionPercent;
        ReportDebug($"Reduccion de dano por Aura Endurecimiento: {reductionPercent * 100}%.", 1);
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
        UpdateHealthUI();
    }

    public void Die(bool triggerEffects = true)
    {
        if (isDead) return;

        if (triggerEffects)
        {
            CombatEventsManager.TriggerEnemyKilled(gameObject, maxHealth);
        }

        isDead = true;
        currentHealth = 0;
        CurrentHealth = 0;

        onDeathEvent?.Invoke();

        OnDeath?.Invoke(gameObject);
        if (_auraManager != null)
        {
            _auraManager.HandleDeathEffect(transform, maxHealth);
        }

        ReportDebug($"{gameObject.name} ha muerto.", 1);

        if (canGrantLifestealOnDeath && playerHealth != null && playerStatsManager != null)
        {
            float lifestealAmount = playerStatsManager.GetCurrentStat(StatType.LifestealOnKill);

            if (canHealPlayer && lifestealAmount > 0)
            {
                playerHealth.Heal(lifestealAmount);
                ReportDebug($"El jugador ha robado {lifestealAmount} de vida al matar a {gameObject.name} (LifestealOnKill).", 1);
            }
        }
        else if (!canGrantLifestealOnDeath)
        {
            ReportDebug($"{gameObject.name} no otorga lifesteal (deshabilitado para tutorial).", 1);
        }

        if (canDestroy)
        {
            if (GetComponent<ExplosionDelayHandler>() != null)
            {
                StartCoroutine(DeathRoutine());
            }
            else
            {
                Destroy(gameObject);
            }
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private IEnumerator DeathRoutine()
    {
        yield return new WaitForSeconds(deathCooldown + 1.5f);

        Destroy(gameObject);
    }

    public void ApplyStun(float duration) 
    {
        if (isStunned)
        {
            if (stunCoroutine != null) StopCoroutine(stunCoroutine);
        }

        stunCoroutine = StartCoroutine(StunRoutine(duration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        ReportDebug("Iniciando aturdimiento.", 1);
        isStunned = true;

        if (knockbackHandler != null)
        {
            knockbackHandler.StopMovement(true);
        }

        if (enemyVisualEffects != null)
        {
            enemyVisualEffects.StartStunEffect(duration);
        }
        else
        {
            if (enemyRenderer != null && enemyRenderer.material.HasProperty("_Color"))
            {
                enemyRenderer.material.color = Color.yellow;
            }
        }

        ReportDebug($"Aturdido por {duration}s.", 1);

        yield return new WaitForSeconds(duration);

        isStunned = false;

        if (knockbackHandler != null)
        {
            knockbackHandler.StopMovement(false);
        }

        if (enemyVisualEffects != null)
        {
            enemyVisualEffects.StopStunEffect();
        }
        else
        {
            if (enemyRenderer != null && enemyRenderer.material.HasProperty("_Color"))
            {
                enemyRenderer.material.color = originalColor;
            }
        }

        stunCoroutine = null;
        ReportDebug("Aturdimiento finalizado.", 1);
    }

    private void InitializeHealthUI()
    {
        if (useDynamicHealthBars)
        {
            totalHealthBars = Mathf.Max(1, Mathf.CeilToInt(maxHealth / healthPerBar));
            currentHealthBars = Mathf.Max(1, Mathf.CeilToInt(currentHealth / healthPerBar));
        }
        else
        {
            totalHealthBars = 1;
            currentHealthBars = 1;
        }

        if (healthSlider != null)
        {
            if (useDynamicHealthBars)
            {
                healthSlider.maxValue = healthPerBar;
                healthSlider.value = GetCurrentBarValue(currentHealth, healthPerBar);
            }
            else
            {
                healthSlider.maxValue = Mathf.Max(1, maxHealth);
                healthSlider.value = currentHealth;
            }
        }
        UpdateHealthUI(); // Llamar para actualizar colores y texto
    }

    private void UpdateHealthUI()
    {
        if (healthSlider != null)
        {
            if (useDynamicHealthBars)
            {
                // Actualizar contador de barras
                int newBars = Mathf.CeilToInt(currentHealth / healthPerBar);
                if (newBars != currentHealthBars)
                {
                    currentHealthBars = newBars;
                }
                healthSlider.value = GetCurrentBarValue(currentHealth, healthPerBar);
                healthSlider.maxValue = healthPerBar;
            }
            else
            {
                healthSlider.value = currentHealth;
                healthSlider.maxValue = Mathf.Max(1, maxHealth);
            }
            if (!healthSlider.gameObject.activeSelf) healthSlider.gameObject.SetActive(true);
        }

        if (healthFillImage != null)
        {
            // Usar color base o degradado según la configuración
            Color barColor = useDynamicHealthBars ?
                GetGradientColor(healthBaseColor, currentHealthBars, totalHealthBars) :
                healthBaseColor;

            healthFillImage.color = barColor;

            if (!healthFillImage.gameObject.activeSelf) healthFillImage.gameObject.SetActive(true);
        }

        if (healthPercentageText != null)
        {
            float percentage = (maxHealth > 0) ? (currentHealth / maxHealth) * 100f : 0f;
            healthPercentageText.text = $"{percentage:F0}%";
        }

        if (healthMultiplierText != null)
        {
            if (useDynamicHealthBars && currentHealthBars > 1)
            {
                healthMultiplierText.text = $"x{currentHealthBars}";
                healthMultiplierText.gameObject.SetActive(true);
            }
            else
            {
                healthMultiplierText.gameObject.SetActive(false);
            }
        }
    }

    private Color GetGradientColor(Color baseColor, int currentBar, int totalBars)
    {
        if (totalBars <= 1) return baseColor;
        if (currentBar <= 0) return baseColor;
        if (currentBar >= totalBars) return baseColor;

        float t = (float)(currentBar - 1) / (totalBars - 1);
        t = Mathf.Lerp(1f, t, colorGradientIntensity);
        Color lighterColor = Color.Lerp(Color.white, baseColor, 0.6f);
        return Color.Lerp(lighterColor, baseColor, t);
    }

    private float GetCurrentBarValue(float currentValue, float valuePerBar)
    {
        if (currentValue <= 0) return 0;
        float remainder = currentValue % valuePerBar;
        if (Mathf.Approximately(remainder, 0f) && currentValue > 0)
        {
            return valuePerBar;
        }
        return remainder;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, Vector3.up * offsetAboveEnemy);
#if UNITY_EDITOR
        // dibujar radio del área si está activado en inspector
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

    #region --- Reducción local pública (compatible con VidaEnemigoEscudo) ---
    /// <summary>
    /// Aplica una reducción de daño local durante 'duration' segundos.
    /// Firma pública añadida para compatibilidad con otros scripts (ArmaduraDemonicaArea, prefabs, etc.).
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
        localReduction = percent;
        yield return new WaitForSeconds(duration);
        localReduction = 0f;
        _reduccionLocalRoutine = null;
    }
    #endregion

    #region --- Monitor y activación de armadura de área ---
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
    /// Fuerza la activación del efecto de área (pública para pruebas).
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

            // comprobación de "chancado" por diferencia Y
            if (Mathf.Abs(root.transform.position.y - transform.position.y) > flattenHeightThreshold) continue;

            // 1) intentar VidaEnemigoEscudo (tu script español)
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
        Debug.Log($"[{name}] Armadura de área ACTIVADA. Radio={areaRadius} Reducción={(areaReductionPercent * 100f)}%");
    }

    private IEnumerator AreaDurationAndCooldownRoutine()
    {
        yield return new WaitForSeconds(areaDuration);
        // finalizar efecto local en este objeto (si aún activo)
        localReduction = 0f;
        areaActive = false;

        // iniciar cooldown
        areaOnCooldown = true;
        yield return new WaitForSeconds(areaCooldown);
        areaOnCooldown = false;

        areaCoroutine = null;
        Debug.Log($"[{name}] Armadura de área COOLDOWN finalizado.");
    }
    #endregion
}

