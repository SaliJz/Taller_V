using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Enumeración para tipos de daño.
/// </summary>
public enum AttackDamageType
{
    Melee,
    Ranged,
}

public class EnemyToughness : MonoBehaviour
{
    [Header("Toughness Configuration")]
    [SerializeField] private bool useToughness = false;
    [Tooltip("Cantidad máxima de dureza del enemigo.")]
    [SerializeField] private float maxToughness = 10f;
    [SerializeField] private float currentToughness;

    [Header("Damage Modifiers")]
    [Tooltip("Porcentaje de daño que los ataques melee infligen a la dureza (1.0 = 100%).")]
    [SerializeField, Range(0f, 2f)] private float meleeToughnessDamageMultiplier = 1.0f;
    [Tooltip("Porcentaje de daño que los ataques a distancia infligen a la dureza (0.25 = 25%).")]
    [SerializeField, Range(0f, 2f)] private float rangedToughnessDamageMultiplier = 0.25f;

    [Header("Dynamic Bars Configuration")]
    [SerializeField] private bool useDynamicBars = false;
    [Tooltip("Valor de cada segmento de vida.")]
    [SerializeField] private float healthPerBar = 100f;
    [Tooltip("Valor de cada segmento de dureza.")]
    [SerializeField] private float toughnessPerBar = 1f;

    [Header("Bar Colors")]
    [SerializeField] private Color healthBaseColor = Color.red;
    [SerializeField] private Color toughnessBaseColor = Color.cyan;
    [Tooltip("Degradado de color entre barras (0 = sin degradado, 1 = degradado completo).")]
    [SerializeField, Range(0f, 1f)] private float colorGradientIntensity = 0.3f;

    [Header("UI References")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Image healthFillImage;
    [SerializeField] private Slider toughnessSlider;
    [SerializeField] private Image toughnessFillImage;
    [SerializeField] private TextMeshProUGUI healthPercentageText;
    [SerializeField] private TextMeshProUGUI healthMultiplierText;
    [SerializeField] private TextMeshProUGUI toughnessMultiplierText;
    [SerializeField] private GameObject toughnessUIGroup;

    // Estado interno
    private EnemyHealth enemyHealth;
    private int currentHealthBars;
    private int totalHealthBars;
    private int currentToughnessBars;
    private int totalToughnessBars;

    // Eventos
    public event Action<float, float> OnToughnessChanged;
    public event Action OnToughnessBreak;

    public float CurrentToughness => currentToughness;
    public float MaxToughness => maxToughness;
    public bool HasToughness => useToughness && currentToughness > 0;

    private void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        if (enemyHealth == null)
        {
            ReportDebug("EnemyHealth no encontrado. EnemyToughnessSystem requiere EnemyHealth.", 3);
            enabled = false;
            return;
        }

        if (useToughness)
        {
            currentToughness = maxToughness;
            if (!toughnessUIGroup.activeSelf) toughnessUIGroup.SetActive(true);
        }
        else
        {
            if (toughnessUIGroup.activeSelf) toughnessUIGroup.SetActive(false);
        }
    }

    private void Start()
    {
        InitializeSystem();
        UpdateUI();
    }

    private void OnEnable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnHealthChanged += HandleHealthChanged;
        }
    }

    private void OnDisable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnHealthChanged -= HandleHealthChanged;
        }
    }

    #region Initialization

    private void InitializeSystem()
    {
        // Calcular barras dinámicas
        if (useDynamicBars)
        {
            totalHealthBars = Mathf.CeilToInt(enemyHealth.MaxHealth / healthPerBar);
            currentHealthBars = Mathf.CeilToInt(enemyHealth.CurrentHealth / healthPerBar);

            if (useToughness)
            {
                totalToughnessBars = Mathf.CeilToInt(maxToughness / toughnessPerBar);
                currentToughnessBars = Mathf.CeilToInt(currentToughness / toughnessPerBar);
            }

            ReportDebug($"Barras dinámicas inicializadas - Vida: {currentHealthBars}/{totalHealthBars}, Dureza: {currentToughnessBars}/{totalToughnessBars}", 1);
        }

        // Configurar UI de dureza
        if (toughnessUIGroup != null)
        {
            toughnessUIGroup.SetActive(useToughness);
        }

        // Configurar sliders
        if (healthSlider != null)
        {
            healthSlider.maxValue = useDynamicBars ? healthPerBar : enemyHealth.MaxHealth;
            healthSlider.value = useDynamicBars ? GetCurrentBarValue(enemyHealth.CurrentHealth, healthPerBar) : enemyHealth.CurrentHealth;
        }

        if (toughnessSlider != null && useToughness)
        {
            toughnessSlider.maxValue = useDynamicBars ? toughnessPerBar : maxToughness;
            toughnessSlider.value = useDynamicBars ? GetCurrentBarValue(currentToughness, toughnessPerBar) : currentToughness;
        }
    }

    #endregion

    #region Damage Processing

    /// <summary>
    /// Procesa el daño considerando la dureza y el tipo de ataque.
    /// Retorna el daño que debe aplicarse a la vida.
    /// </summary>
    public float ProcessDamage(float rawDamage, AttackDamageType damageType)
    {
        if (!useToughness || currentToughness <= 0)
        {
            // Sin dureza o dureza rota: daño directo a vida
            return rawDamage;
        }

        // Calcular multiplicador según tipo de ataque
        float toughnessMultiplier = GetToughnessMultiplier(damageType);
        float toughnessDamage = rawDamage * toughnessMultiplier;

        // Aplicar daño a dureza
        float overflow = ApplyToughnessDamage(toughnessDamage);

        // Si hay overflow, el exceso va a la vida
        if (overflow > 0)
        {
            ReportDebug($"Dureza rota. Overflow de {overflow} daño aplicado a vida.", 1);
            return overflow / toughnessMultiplier; // Convertir overflow proporcional de vuelta
        }

        // Dureza absorbió todo el daño
        ReportDebug($"Dureza absorbió {toughnessDamage} de daño ({toughnessMultiplier * 100}% del total).", 1);
        return 0f;
    }

    private float GetToughnessMultiplier(AttackDamageType damageType)
    {
        switch (damageType)
        {
            case AttackDamageType.Melee:
                return meleeToughnessDamageMultiplier;
            case AttackDamageType.Ranged:
                return rangedToughnessDamageMultiplier;
            default:
                return 1.0f;
        }
    }

    private float ApplyToughnessDamage(float damage)
    {
        float previousToughness = currentToughness;
        currentToughness -= damage;

        float overflow = 0f;
        if (currentToughness < 0)
        {
            overflow = Mathf.Abs(currentToughness);
            currentToughness = 0;
            OnToughnessBreak?.Invoke();
            ReportDebug("¡Dureza rota!", 1);
        }

        currentToughness = Mathf.Max(0, currentToughness);
        OnToughnessChanged?.Invoke(currentToughness, maxToughness);

        UpdateToughnessBars();
        UpdateUI();

        return overflow;
    }

    #endregion

    #region Bar Management

    private void UpdateToughnessBars()
    {
        if (!useDynamicBars) return;

        int newBars = Mathf.CeilToInt(currentToughness / toughnessPerBar);
        if (newBars != currentToughnessBars)
        {
            currentToughnessBars = newBars;
            ReportDebug($"Barras de dureza actualizadas: {currentToughnessBars}/{totalToughnessBars}", 1);
        }
    }

    private void HandleHealthChanged(float current, float max)
    {
        if (!useDynamicBars) return;

        int newBars = Mathf.CeilToInt(current / healthPerBar);
        if (newBars != currentHealthBars)
        {
            currentHealthBars = newBars;
            ReportDebug($"Barras de vida actualizadas: {currentHealthBars}/{totalHealthBars}", 1);
        }

        UpdateUI();
    }

    private float GetCurrentBarValue(float currentValue, float valuePerBar)
    {
        float remainder = currentValue % valuePerBar;
        return remainder > 0 ? remainder : (currentValue > 0 ? valuePerBar : 0);
    }

    #endregion

    #region UI Updates

    private void UpdateUI()
    {
        UpdateHealthUI();
        if (useToughness)
        {
            UpdateToughnessUI();
        }
    }

    private void UpdateHealthUI()
    {
        if (healthSlider != null)
        {
            if (useDynamicBars)
            {
                healthSlider.value = GetCurrentBarValue(enemyHealth.CurrentHealth, healthPerBar);
                healthSlider.maxValue = healthPerBar;
            }
            else
            {
                healthSlider.value = enemyHealth.CurrentHealth;
                healthSlider.maxValue = enemyHealth.MaxHealth;
            }
        }

        if (healthFillImage != null)
        {
            Color barColor = GetGradientColor(healthBaseColor, currentHealthBars, totalHealthBars);
            healthFillImage.color = barColor;
        }

        if (healthPercentageText != null)
        {
            float percentage = (enemyHealth.CurrentHealth / enemyHealth.MaxHealth) * 100f;
            healthPercentageText.text = $"{percentage:F0}%";
        }

        if (healthMultiplierText != null && useDynamicBars)
        {
            if (currentHealthBars > 1)
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

    private void UpdateToughnessUI()
    {
        if (toughnessSlider != null)
        {
            if (useDynamicBars)
            {
                toughnessSlider.value = GetCurrentBarValue(currentToughness, toughnessPerBar);
                toughnessSlider.maxValue = toughnessPerBar;
            }
            else
            {
                toughnessSlider.value = currentToughness;
                toughnessSlider.maxValue = maxToughness;
            }
        }

        if (toughnessFillImage != null)
        {
            Color barColor = GetGradientColor(toughnessBaseColor, currentToughnessBars, totalToughnessBars);
            toughnessFillImage.color = barColor;
        }

        if (toughnessMultiplierText != null && useDynamicBars)
        {
            if (currentToughnessBars > 1)
            {
                toughnessMultiplierText.text = $"x{currentToughnessBars}";
                toughnessMultiplierText.gameObject.SetActive(true);
            }
            else
            {
                toughnessMultiplierText.gameObject.SetActive(false);
            }
        }

        if (currentToughness <= 0 && toughnessUIGroup != null)
        {
            if (toughnessUIGroup.activeSelf) toughnessUIGroup.gameObject.SetActive(false); // Ocultar si la dureza está rota
        }
        else
        {
            if (!toughnessUIGroup.activeSelf) toughnessUIGroup.gameObject.SetActive(true); // Asegurar que esté activo si la dureza no está rota
        }
    }

    private Color GetGradientColor(Color baseColor, int currentBar, int totalBars)
    {
        if (totalBars <= 1 || currentBar <= 0) return baseColor;

        // Calcular posición en el degradado (la última barra es la más intensa)
        float t = (float)(currentBar - 1) / (totalBars - 1);
        
        // Aplicar intensidad de degradado
        t = Mathf.Lerp(1f, t, colorGradientIntensity);

        // Crear color más claro para barras anteriores
        Color lighterColor = Color.Lerp(Color.white, baseColor, 0.6f);
        
        return Color.Lerp(lighterColor, baseColor, t);
    }

    #endregion

    #region Public API

    public void SetMaxToughness(float value)
    {
        maxToughness = value;
        currentToughness = maxToughness;
        
        if (useDynamicBars)
        {
            totalToughnessBars = Mathf.CeilToInt(maxToughness / toughnessPerBar);
            currentToughnessBars = totalToughnessBars;
        }
        
        UpdateUI();
    }

    public void ResetToughness()
    {
        currentToughness = maxToughness;
        
        if (useDynamicBars)
        {
            currentToughnessBars = totalToughnessBars;
        }
        
        OnToughnessChanged?.Invoke(currentToughness, maxToughness);
        UpdateUI();
        ReportDebug("Dureza restaurada.", 1);
    }

    public void SetUseToughness(bool value)
    {
        useToughness = value;
        if (toughnessUIGroup != null)
        {
            toughnessUIGroup.SetActive(value);
        }
        UpdateUI();
    }

    #endregion

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1:
                Debug.Log($"[EnemyToughnessSystem] {message}");
                break;
            case 2:
                Debug.LogWarning($"[EnemyToughnessSystem] {message}");
                break;
            case 3:
                Debug.LogError($"[EnemyToughnessSystem] {message}");
                break;
        }
    }
}