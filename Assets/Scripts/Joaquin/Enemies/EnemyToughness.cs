using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

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
    [Tooltip("Valor de cada segmento de dureza.")]
    [SerializeField] private float toughnessPerBar = 1f;

    [Header("Bar Colors")]
    [SerializeField] private Color toughnessBaseColor = Color.cyan;
    [Tooltip("Degradado de color entre barras (0 = sin degradado, 1 = degradado completo).")]
    [SerializeField, Range(0f, 1f)] private float colorGradientIntensity = 0.3f;

    [Header("UI References")]
    [SerializeField] private Slider toughnessSlider;
    [SerializeField] private Image toughnessFillImage;
    [SerializeField] private TextMeshProUGUI toughnessPercentageText;
    [SerializeField] private TextMeshProUGUI toughnessMultiplierText;
    [SerializeField] private GameObject toughnessUIGroup;

    // Estado interno
    private EnemyHealth enemyHealth;
    private int currentToughnessBars;
    private int totalToughnessBars;
    private bool isInitialized = false;

    // Eventos
    public event Action<float, float> OnToughnessChanged;
    public event Action OnToughnessBreak;

    private Coroutine temporaryBuffRoutine;

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
            if (toughnessUIGroup != null && !toughnessUIGroup.activeSelf) toughnessUIGroup.SetActive(true);
        }
        else
        {
            if (toughnessUIGroup != null && toughnessUIGroup.activeSelf) toughnessUIGroup.SetActive(false);
        }
    }

    private void Start()
    {
        StartCoroutine(DelayedInitialization());
    }

    private System.Collections.IEnumerator DelayedInitialization()
    {
        yield return null; // Esperar un frame

        InitializeSystem();
        UpdateUI();
        isInitialized = true;

        ReportDebug($"Sistema inicializado - Vida: {enemyHealth.CurrentHealth}/{enemyHealth.MaxHealth}, Dureza: {currentToughness}/{maxToughness}", 1);
    }

    #region Initialization

    private void InitializeSystem()
    {
        if (enemyHealth == null || enemyHealth.MaxHealth <= 0)
        {
            ReportDebug("No se puede inicializar: EnemyHealth no válido", 3);
            return;
        }

        if (useToughness)
        {
            totalToughnessBars = Mathf.Max(1, Mathf.CeilToInt(maxToughness / toughnessPerBar));
            currentToughnessBars = totalToughnessBars; // Dureza siempre empieza llena
        }

        // Reportar estado inicial
        if (useDynamicBars)
        {
            ReportDebug($"Barras dinámicas de dureza: {currentToughnessBars}/{totalToughnessBars}", 1);
        }

        // Configurar UI de dureza
        if (toughnessUIGroup != null)
        {
            toughnessUIGroup.SetActive(useToughness);
        }

        // Configurar slider de dureza
        if (toughnessSlider != null && useToughness)
        {
            if (useDynamicBars)
            {
                toughnessSlider.maxValue = toughnessPerBar;
                toughnessSlider.value = GetCurrentBarValue(currentToughness, toughnessPerBar);
            }
            else
            {
                toughnessSlider.maxValue = maxToughness;
                toughnessSlider.value = currentToughness;
            }
        }
    }

    #endregion

    #region Damage Processing

    /// <summary>
    /// Procesa el daño considerando la dureza y el tipo de ataque.
    /// Retorna el daño que debe aplicarse a la vida.
    /// </summary>
    public float ProcessDamage(float rawDamage, AttackDamageType damageType, float attackerToughnessBonus = 0f)
    {
        if (!useToughness || currentToughness <= 0)
        {
            // Sin dureza o dureza rota: daño directo a vida
            return rawDamage;
        }

        // Calcular multiplicador según tipo de ataque
        float baseMultiplier = GetToughnessMultiplier(damageType);
        float finalMultiplier = baseMultiplier + attackerToughnessBonus;
        float toughnessDamage = rawDamage * finalMultiplier;

        // Aplicar daño a dureza
        float overflow = ApplyToughnessDamage(toughnessDamage);

        // Si hay overflow, el exceso va a la vida
        if (overflow > 0)
        {
            ReportDebug($"Dureza rota. Overflow de {overflow} daño aplicado a vida.", 1);
            return overflow / finalMultiplier; // Convertir overflow proporcional de vuelta
        }

        // Dureza absorbió todo el daño
        ReportDebug($"Dureza absorbió {toughnessDamage} de daño ({finalMultiplier * 100}% del total).", 1);
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

    // Metodo para obtener el valor actual dentro de la barra
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

    #endregion

    #region UI Updates

    private void UpdateUI()
    {
        if (!isInitialized) return;

        if (useToughness)
        {
            UpdateToughnessUI();
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

        if (toughnessUIGroup != null)
        {
            bool shouldBeActive = currentToughness > 0;
            if (toughnessUIGroup.activeSelf != shouldBeActive)
            {
                toughnessUIGroup.SetActive(shouldBeActive);
            }
        }
    }

    private Color GetGradientColor(Color baseColor, int currentBar, int totalBars)
    {
        // Si solo hay una barra total, siempre usar color base
        if (totalBars <= 1) return baseColor;

        // Si no quedan barras, usar color base
        if (currentBar <= 0) return baseColor;

        // Si es la última barra (currentBar == totalBars), usar color base
        if (currentBar >= totalBars) return baseColor;

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

    public float AddCurrentToughness(float amount)
    {
        if (!useToughness || amount <= 0) return 0f;

        float previousToughness = currentToughness;
        currentToughness = Mathf.Clamp(currentToughness + amount, 0f, maxToughness);

        float addedAmount = currentToughness - previousToughness;

        if (addedAmount > 0)
        {
            OnToughnessChanged?.Invoke(currentToughness, maxToughness);
            UpdateUI();
        }

        return addedAmount;
    }

    public void ApplyToughnessBuff(float amount, float duration)
    {
        if (temporaryBuffRoutine != null)
        {
            StopCoroutine(temporaryBuffRoutine);
            temporaryBuffRoutine = null;
        }

        temporaryBuffRoutine = StartCoroutine(ToughnessBuffRoutine(amount, duration));
    }

    private IEnumerator ToughnessBuffRoutine(float amount, float duration)
    {
        if (amount <= 0f)
        {
            temporaryBuffRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            AddCurrentToughness(amount);

            OnToughnessChanged?.Invoke(currentToughness, maxToughness);
            UpdateUI();

            int remaining = Mathf.Max(0, Mathf.CeilToInt(duration - elapsed));
            ReportDebug($"Aplicando buff de dureza: +{amount} (Tiempo restante: {remaining} s)", 1);

            yield return new WaitForSeconds(1f);
            elapsed += 1f;
        }

        ReportDebug("Buff de dureza finalizado.", 1);

        temporaryBuffRoutine = null;
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