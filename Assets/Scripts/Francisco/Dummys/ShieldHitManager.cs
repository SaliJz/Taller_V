using UnityEngine;
using UnityEngine.Events;

public class ShieldHitManager : MonoBehaviour
{
    public static ShieldHitManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private int hitsToTrigger = 5;
    [SerializeField] private UnityEvent onShieldHitThresholdReached;

    private int currentHitCount = 0;

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

        Debug.Log($"[ShieldHitManager] Inicializado. Se requieren {hitsToTrigger} golpes para activar el evento.");
    }

    public void RegisterShieldHit()
    {
        currentHitCount++;
        Debug.Log($"[ShieldHitManager] Golpe al escudo registrado. Contador: {currentHitCount}/{hitsToTrigger}");

        if (currentHitCount >= hitsToTrigger)
        {
            ExecuteEvent();
            currentHitCount = 0;
        }
    }

    private void ExecuteEvent()
    {
        Debug.Log("[ShieldHitManager] ¡UMBRAL DE GOLPES ALCANZADO! Ejecutando evento.");
        onShieldHitThresholdReached?.Invoke();
    }
}