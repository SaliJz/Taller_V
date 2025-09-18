using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Clase que maneja el HUD (Heads-Up Display) del jugador.
/// </summary> 
public class HUDManager : MonoBehaviour
{
    [System.Serializable]
    // Clase para asociar etapas de vida con iconos
    public class LifeStageIcon
    {
        public PlayerHealth.LifeStage stage;
        public Sprite icon;
    }

    public static HUDManager Instance { get; private set; }

    [Header("Componentes del HUD")]
    [SerializeField] private Image healthBar;
    [SerializeField] private Image lifeStageIconImage;
    [SerializeField] private List<LifeStageIcon> lifeStageIcons;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void OnEnable()
    {
        PlayerHealth.OnHealthChanged += UpdateHealthBar;
        PlayerHealth.OnLifeStageChanged += UpdateLifeStageIcon;
    }

    private void OnDisable()
    {
        PlayerHealth.OnHealthChanged -= UpdateHealthBar;
        PlayerHealth.OnLifeStageChanged += UpdateLifeStageIcon;
    }

    /// <summary>
    /// Función que actualiza la barra de salud en el HUD.
    /// </summary>
    /// <param name="currentHealth"> Vida actual del jugador </param>
    /// <param name="maxHealth"> Vida máxima del jugador </param>
    private void UpdateHealthBar(float currentHealth, float maxHealth)
    {
        if (healthBar != null)
        {
            maxHealth = Mathf.Clamp(maxHealth, 0, maxHealth);
            healthBar.fillAmount = currentHealth / maxHealth;
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