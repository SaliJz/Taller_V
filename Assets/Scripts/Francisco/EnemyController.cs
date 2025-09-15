using System;
using UnityEngine;

public class EnemyController : MonoBehaviour
{
    private HealthController healthController;
    public Action OnDeath;

    void Start()
    {
        healthController = GetComponent<HealthController>();
        if (healthController != null)
        {
            healthController.OnDamaged += () => Debug.Log("Enemy took damage!");
        }
    }

    void OnDisable()
    {
        OnDeath?.Invoke();
    }

    void OnDestroy()
    {
        OnDeath?.Invoke();
    }
}