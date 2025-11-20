using System.Collections.Generic;
using UnityEngine;

public class FleshPulseController : MonoBehaviour
{
    [Header("Configuración Global")]
    [SerializeField] private LayerMask _obstacleLayer;
    [SerializeField] private LayerMask _playerLayer;

    // Datos recibidos del Boss
    private float _maxRadius;
    private float _expansionDuration;
    private float _damage;
    private float _slowPercentage;
    private float _slowDuration;

    private bool _isExpanding = false;
    private float _expansionSpeed;

    // Evita que múltiples brazos dañen al mismo jugador
    private HashSet<GameObject> _hitTargets = new HashSet<GameObject>();

    public void Initialize(float maxRadius, float duration, float damage, float slowPercent, float slowDur)
    {
        _maxRadius = maxRadius;
        _expansionDuration = duration;
        _damage = damage;
        _slowPercentage = slowPercent;
        _slowDuration = slowDur;

        // Configurar velocidad de expansión
        _expansionSpeed = (_maxRadius * 2) / _expansionDuration;

        // Inicializar nervios hijos
        FleshPulseNerve[] arms = GetComponentsInChildren<FleshPulseNerve>();
        foreach (var arm in arms)
        {
            arm.Initialize(this, _obstacleLayer, _playerLayer);
        }

        transform.localScale = Vector3.zero; // Empezar pequeños
        _isExpanding = true;
    }

    private void Update()
    {
        if (!_isExpanding) return;

        // Expansión del objeto padre
        float growth = _expansionSpeed * Time.deltaTime;
        Vector3 newScale = transform.localScale;

        newScale.x += growth;
        newScale.z += growth;
        newScale.y = 1f; // Mantener altura Y constante si se desea

        transform.localScale = newScale;

        // Condición de término basada en diámetro
        if (newScale.x / 2 >= _maxRadius)
        {
            _isExpanding = false;
            Destroy(gameObject, 0.5f);
        }
    }

    public void TryHitPlayer(GameObject player)
    {
        if (_hitTargets.Contains(player)) return;

        _hitTargets.Add(player);

        ApplyEffects(player);
    }

    private void ApplyEffects(GameObject player)
    {
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        PlayerStatsManager stats = player.GetComponent<PlayerStatsManager>();
        PlayerMovement movement = player.GetComponent<PlayerMovement>();

        // Lógica de daño real
        if (health != null)
        {
            ExecuteAttack(player, _damage);
            health.IsMarked = true;
        }

        if (stats != null)
        {
            float currentSpeed = stats.GetStat(StatType.MoveSpeed);
            float slowAmount = currentSpeed * -_slowPercentage;
            string uniqueKey = $"SlowEffect_{GetInstanceID()}";

            stats.ApplyTimedModifier(uniqueKey, StatType.MoveSpeed, slowAmount, _slowDuration);
        }

        if (movement != null)
        {
            movement.DisableDashForDuration(_slowDuration);
        }

        Debug.Log($"<color=red>[FleshPulseController] Jugador {player.name} golpeado por Nerves Pulse: Daño {_damage}, Slow {_slowPercentage * 100}% por {_slowDuration} segundos.</color>");
    }

    private void ExecuteAttack(GameObject target, float damageAmount)
    {
        if (target.TryGetComponent<PlayerBlockSystem>(out var blockSystem) && target.TryGetComponent<PlayerHealth>(out var health))
        {
            // Verificar si el ataque es bloqueado
            if (blockSystem.IsBlocking && blockSystem.CanBlockAttack(transform.position))
            {
                float remainingDamage = blockSystem.ProcessBlockedAttack(damageAmount);

                if (remainingDamage > 0f)
                {
                    health.TakeDamage(remainingDamage, false, AttackDamageType.Melee);
                }

                Debug.Log($"<color=red>[FleshPulseController] Ataque bloqueado por el jugador.</color>");
                return;
            }

            health.TakeDamage(damageAmount, false, AttackDamageType.Melee);
        }
        else if (target.TryGetComponent<PlayerHealth>(out var healthOnly))
        {
            healthOnly.TakeDamage(damageAmount, false, AttackDamageType.Melee);
        }
    }
}