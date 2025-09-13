using UnityEngine;
using UnityEngine.UI;

public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance { get; private set; }

    [Header("Componentes del HUD")]
    [SerializeField] private Image healthBar;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    private void OnEnable()
    {
        PlayerHealth.OnHealthChanged += UpdateHealthBar;
    }

    private void OnDisable()
    {
        PlayerHealth.OnHealthChanged -= UpdateHealthBar;
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
}