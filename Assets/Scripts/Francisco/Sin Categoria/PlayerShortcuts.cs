using UnityEngine;

public class PlayerShortcuts : MonoBehaviour
{
    [Header("Shortcut Configuration")]
    [SerializeField] private KeyCode healKey = KeyCode.H;
    [SerializeField] private KeyCode damageKey = KeyCode.J;
    [SerializeField] private KeyCode maxHealthKey = KeyCode.K;

    [Header("Modification Values")]
    [SerializeField] private float healAmount = 20f;
    [SerializeField] private float damageAmount = 10f;

    private PlayerHealth playerHealth;

    private void Start()
    {
        playerHealth = GetComponent<PlayerHealth>();

        if (playerHealth == null)
        {
            Debug.LogError("[PlayerHealthDebugShortcuts] No se encontró PlayerHealth en este GameObject.");
            enabled = false;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(healKey))
        {
            playerHealth.Heal(healAmount);
            Debug.Log($"<color=green>[DEBUG] Jugador curado +{healAmount} vida. Vida actual: {playerHealth.CurrentHealth}/{playerHealth.MaxHealth}</color>");
        }

        if (Input.GetKeyDown(damageKey))
        {
            playerHealth.TakeDamage(damageAmount, true); 
            Debug.Log($"<color=yellow>[DEBUG] Jugador dañado -{damageAmount} vida. Vida actual: {playerHealth.CurrentHealth}/{playerHealth.MaxHealth}</color>");
        }

        if (Input.GetKeyDown(maxHealthKey))
        {
            float missingHealth = playerHealth.MaxHealth - playerHealth.CurrentHealth;
            if (missingHealth > 0)
            {
                playerHealth.Heal(missingHealth);
                Debug.Log($"<color=cyan>[DEBUG] Vida restaurada al máximo: {playerHealth.MaxHealth}</color>");
            }
            else
            {
                Debug.Log($"<color=cyan>[DEBUG] La vida ya está al máximo: {playerHealth.MaxHealth}</color>");
            }
        }
    }
}
