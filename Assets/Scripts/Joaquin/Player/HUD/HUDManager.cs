using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Clase que maneja el HUD (Heads-Up Display) del jugador.
/// </summary> 
public class HUDManager : MonoBehaviour
{
    [System.Serializable]
    public class LifeStageIcon
    {
        public PlayerHealth.LifeStage stage;
        public Sprite icon;
    }

    public static HUDManager Instance { get; private set; }

    [Header("Componentes del HUD")]
    [SerializeField] private Image healthBar;
    [SerializeField] private Image temporaryHealthBar;
    [SerializeField] private Image lifeStageIconImage;
    [SerializeField] private List<LifeStageIcon> lifeStageIcons;

    [Header("Prompt de Interacción")]
    [SerializeField] private GameObject interactionPromptPanel;
    [SerializeField] private TextMeshProUGUI interactionPromptText;

    [Header("Etapas de Vida - Root Objects")]
    [SerializeField] private GameObject adultRootStage;
    [SerializeField] private GameObject elderRootStage;

    [Header("Low Health VFX")]
    [SerializeField] private Image screenFlashOverlay;
    [SerializeField] private Color lowHealthScreenFlashColor = new Color(1f, 0f, 0f, 0.3f);
    [SerializeField] private Color lowHealthBarFlashColor = Color.white;
    [SerializeField] private float healthBarFlashInterval = 0.5f;
    [SerializeField] private float screenFlashInterval = 1f;
    [SerializeField] private float lowHealthThreshold = 0.25f;
    [SerializeField] private float lowHealthEffectDuration = 2.5f;

    [Header("Vida Temporal - Animacion")]
    [SerializeField] private float temporaryHealthLerpSpeed = 5f;
    [SerializeField] private RectTransform healthBarParentRect;

    private float targetTempHealthPercentage = 0f;
    private float maxHealthForTempBar = 1f;

    private bool isLowHealth = false;
    private Coroutine healthBarFlashCoroutine;
    private Coroutine screenFlashCoroutine;
    private Coroutine lowHealthEffectCoroutine;
    private Color originalHealthBarColor;
    private float lowHealthTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;

        // Guardar color original de la barra de vida
        if (healthBar != null)
        {
            originalHealthBarColor = healthBar.color;
        }

        // Inicializar overlay de pantalla
        if (screenFlashOverlay != null)
        {
            // Asegurarse de que el overlay esté invisible al inicio
            Color transparent = lowHealthScreenFlashColor;
            transparent.a = 0f;
            screenFlashOverlay.color = transparent;
            screenFlashOverlay.raycastTarget = false; // No bloquear interacciones
        }
    }

    private void Update()
    {
        if (temporaryHealthBar != null && healthBar != null)
        {
            if (healthBarParentRect != null)
            {
                float healthBarEndPosition = healthBar.fillAmount;

                float targetAnchorMinX = healthBarEndPosition;

                RectTransform tempBarRect = temporaryHealthBar.rectTransform;

                float currentWidth = tempBarRect.anchorMax.x - tempBarRect.anchorMin.x;

                Vector2 currentAnchorMin = tempBarRect.anchorMin;

                tempBarRect.anchorMin = Vector2.Lerp(
                    currentAnchorMin,
                    new Vector2(targetAnchorMinX, currentAnchorMin.y),
                    Time.deltaTime * temporaryHealthLerpSpeed
                );

                tempBarRect.anchorMax = new Vector2(
                    tempBarRect.anchorMin.x + currentWidth,
                    tempBarRect.anchorMax.y
                );

                temporaryHealthBar.fillAmount = Mathf.Lerp(temporaryHealthBar.fillAmount, targetTempHealthPercentage, Time.deltaTime * temporaryHealthLerpSpeed);
            }
        }
    }

    private void OnEnable()
    {
        PlayerHealth.OnHealthChanged += UpdateHealthBar;
        PlayerHealth.OnLifeStageChanged += UpdateLifeStageIcon;
    }

    private void OnDisable()
    {
        PlayerHealth.OnHealthChanged -= UpdateHealthBar;
        PlayerHealth.OnLifeStageChanged -= UpdateLifeStageIcon;

        if (healthBarFlashCoroutine != null) StopCoroutine(healthBarFlashCoroutine);
        if (screenFlashCoroutine != null) StopCoroutine(screenFlashCoroutine);
        if (lowHealthEffectCoroutine != null) StopCoroutine(lowHealthEffectCoroutine);
    }

    /// <summary>
    /// Función que actualiza la barra de salud en el HUD.
    /// </summary>
    /// <param name="currentHealth"> Vida actual del jugador </param>
    /// <param name="maxHealth"> Vida máxima del jugador </param>
    private void UpdateHealthBar(float currentHealth, float maxHealth)
    {
        if (healthBar == null) return;

        if (maxHealth <= 0f)
        {
            Debug.LogWarning("[HUDManager] maxHealth inválido en UpdateHealthBar: " + maxHealth);
            healthBar.fillAmount = 0f;
            return;
        }

        float healthPercentage = Mathf.Clamp01(currentHealth / maxHealth);
        healthBar.fillAmount = healthPercentage;

        // Verificar si está en vida baja
        bool shouldShowLowHealthEffects = healthPercentage < lowHealthThreshold;

        if (shouldShowLowHealthEffects && !isLowHealth)
        {
            // Entrar en estado de vida baja
            isLowHealth = true;
            StartLowHealthEffects();
        }
        else if (shouldShowLowHealthEffects && isLowHealth)
        {
            // Reiniciar efectos si aún está en vida baja
            ResetLowHealthEffects();
        }
        else if (!shouldShowLowHealthEffects && isLowHealth)
        {
            // Salir del estado de vida baja
            isLowHealth = false;
            StopLowHealthEffects();
        }
    }

    public void SetTemporaryHealthValues(float currentTempHealth, float maxHealth)
    {
        if (temporaryHealthBar == null) return;

        maxHealthForTempBar = maxHealth > 0 ? maxHealth : 1f;

        targetTempHealthPercentage = currentTempHealth / maxHealthForTempBar;

        ReportDebug($"Vida temporal establecida. Valor: {currentTempHealth}/{maxHealth} (Target Fill: {targetTempHealthPercentage})", 1);
    }

    public void UpdateTemporaryHealthBar(float currentTempHealth, float maxLimit)
    {
        if (temporaryHealthBar != null)
        {
            float fillAmount = maxLimit > 0 ? currentTempHealth / maxLimit : 0f;
            temporaryHealthBar.fillAmount = fillAmount;

            ReportDebug($"Vida temporal actualizada. Valor: {currentTempHealth}/{maxLimit} (Fill: {fillAmount})", 1);
        }
    }

    /// <summary>
    /// Inicia los efectos visuales de vida baja.
    /// </summary>
    private void StartLowHealthEffects()
    {
        // Reiniciar temporizador
        lowHealthTimer = 0f;

        // Iniciar parpadeo de la barra de vida
        if (healthBarFlashCoroutine != null) StopCoroutine(healthBarFlashCoroutine);
        healthBarFlashCoroutine = StartCoroutine(HealthBarFlashRoutine());

        // Iniciar parpadeo de pantalla
        if (screenFlashCoroutine != null) StopCoroutine(screenFlashCoroutine);
        screenFlashCoroutine = StartCoroutine(ScreenFlashRoutine());

        // Iniciar corrutina de duración del efecto
        if (lowHealthEffectCoroutine != null) StopCoroutine(lowHealthEffectCoroutine);
        lowHealthEffectCoroutine = StartCoroutine(LowHealthEffectDurationRoutine());

        ReportDebug("Efectos de vida baja activados.", 1);
    }

    public void SetInteractionPrompt(bool active, string actionText = "")
    {
        if (interactionPromptPanel != null)
        {
            interactionPromptPanel.SetActive(active);
        }

        if (interactionPromptText != null && active)
        {
            interactionPromptText.text = actionText;
        }
    }

    /// <summary>
    /// Reinicia los efectos de vida baja (reinicia el temporizador)
    /// </summary>
    private void ResetLowHealthEffects()
    {
        lowHealthTimer = 0f;
        ReportDebug("Efectos de vida baja reiniciados.", 1);
    }

    /// <summary>
    /// Detiene los efectos visuales de vida baja.
    /// </summary>
    private void StopLowHealthEffects()
    {
        // Detener parpadeo de la barra de vida
        if (healthBarFlashCoroutine != null)
        {
            StopCoroutine(healthBarFlashCoroutine);
            healthBarFlashCoroutine = null;
        }

        // Restaurar color original de la barra
        if (healthBar != null)
        {
            healthBar.color = originalHealthBarColor;
        }

        // Detener parpadeo de pantalla
        if (screenFlashCoroutine != null)
        {
            StopCoroutine(screenFlashCoroutine);
            screenFlashCoroutine = null;
        }

        // Detener corrutina de duración
        if (lowHealthEffectCoroutine != null)
        {
            StopCoroutine(lowHealthEffectCoroutine);
            lowHealthEffectCoroutine = null;
        }

        // Asegurarse de que el overlay esté invisible
        if (screenFlashOverlay != null)
        {
            Color transparent = lowHealthScreenFlashColor;
            transparent.a = 0f;
            screenFlashOverlay.color = transparent;
        }

        ReportDebug("Efectos de vida baja desactivados.", 1);
    }

    /// <summary>
    /// Rutina que controla la duración total del efecto de vida baja
    /// </summary>
    private IEnumerator LowHealthEffectDurationRoutine()
    {
        while (lowHealthTimer < lowHealthEffectDuration)
        {
            lowHealthTimer += Time.deltaTime;
            yield return null;
        }

        // Tiempo cumplido, detener efectos
        if (isLowHealth)
        {
            isLowHealth = false;
            StopLowHealthEffects();
        }
    }

    /// <summary>
    /// Rutina de parpadeo de la barra de vida.
    /// </summary>
    private IEnumerator HealthBarFlashRoutine()
    {
        if (healthBar == null) yield break;

        if (originalHealthBarColor == default(Color))
        {
            originalHealthBarColor = healthBar.color;
        }

        Color flashColor = lowHealthBarFlashColor;

        while (true)
        {
            if (healthBar != null)
            {
                healthBar.color = flashColor;
            }

            yield return new WaitForSeconds(healthBarFlashInterval);

            if (healthBar != null)
            {
                healthBar.color = originalHealthBarColor;
            }

            yield return new WaitForSeconds(healthBarFlashInterval);
        }
    }

    /// <summary>
    /// Rutina de parpadeo de pantalla (overlay rojo).
    /// </summary>
    private IEnumerator ScreenFlashRoutine()
    {
        if (screenFlashOverlay == null) yield break;

        Color visibleColor = lowHealthScreenFlashColor;
        Color transparentColor = lowHealthScreenFlashColor;
        transparentColor.a = 0f;

        while (true)
        {
            // Fade in
            float elapsed = 0f;
            float fadeDuration = screenFlashInterval * 0.5f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;
                screenFlashOverlay.color = Color.Lerp(transparentColor, visibleColor, t);
                yield return null;
            }

            screenFlashOverlay.color = visibleColor;

            // Fade out
            elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;
                screenFlashOverlay.color = Color.Lerp(visibleColor, transparentColor, t);
                yield return null;
            }

            screenFlashOverlay.color = transparentColor;

            // Esperar antes del siguiente flash
            yield return new WaitForSeconds(screenFlashInterval * 0.5f);
        }
    }

    /// <summary>
    /// Función que actualiza el icono de la etapa de vida en el HUD.
    /// </summary>
    /// <param name="newStage"> Nueva etapa de vida del jugador </param>
    private void UpdateLifeStageIcon(PlayerHealth.LifeStage newStage)
    {
        LifeStageIcon foundIcon = lifeStageIcons.Find(icon => icon.stage == newStage);
        if (foundIcon != null && lifeStageIconImage != null)
        {
            lifeStageIconImage.sprite = foundIcon.icon;
            ReportDebug($"Icono del HUD actualizado a: {newStage}", 1);
        }

        if (newStage == PlayerHealth.LifeStage.Adult)
        {
            if (adultRootStage != null) adultRootStage.SetActive(true);
            if (elderRootStage != null) elderRootStage.SetActive(false);
        }
        else if (newStage == PlayerHealth.LifeStage.Elder)
        {
            if (adultRootStage != null) adultRootStage.SetActive(false);
            if (elderRootStage != null) elderRootStage.SetActive(true);
        }
        else
        {
            if (adultRootStage != null) adultRootStage.SetActive(false);
            if (elderRootStage != null) elderRootStage.SetActive(false);
        }
    }

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
                Debug.Log($"[HUDManager] {message}");
                break;
            case 2:
                Debug.LogWarning($"[HUDManager] {message}");
                break;
            case 3:
                Debug.LogError($"[HUDManager] {message}");
                break;
            default:
                Debug.Log($"[HUDManager] {message}");
                break;
        }
    }
}