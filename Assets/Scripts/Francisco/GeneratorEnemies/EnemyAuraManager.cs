using UnityEngine;
using System.Collections;

[RequireComponent(typeof(EnemyHealth))]
public class EnemyAuraManager : MonoBehaviour
{
    private EnemyHealth enemyHealth;
    private DevilConfiguration config;

    public DevilAuraType ActiveAura { get; private set; } = DevilAuraType.None;
    public ResurrectionLevel ActiveResurrectionLevel { get; private set; } = ResurrectionLevel.None;

    public float MoveSpeedMultiplier { get; private set; } = 1.0f;
    public float AttackSpeedMultiplier { get; private set; } = 1.0f;
    public float DamageReductionPercent { get; private set; } = 0.0f; 
    public float StunningStunChance { get; private set; } = 0.0f;
    public float CritDamageMultiplier { get; private set; } = 1.0f; 

    private Coroutine regenCoroutine;

    private void Awake()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        config = Resources.Load<DevilConfiguration>("DevilConfig"); 
        if (config == null)
        {
            Debug.LogError("[EnemyAuraManager] No se encontro DevilConfiguration en Resources.");
        }
    }

    public void ApplyAura(DevilAuraType aura, ResurrectionLevel level)
    {
        if (config == null) return;

        ActiveAura = aura;
        ActiveResurrectionLevel = level;

        MoveSpeedMultiplier = 1.0f;
        AttackSpeedMultiplier = 1.0f;
        DamageReductionPercent = 0.0f;
        StunningStunChance = 0.0f;
        CritDamageMultiplier = 1.0f;

        switch (aura)
        {
            case DevilAuraType.Frenzy:
                MoveSpeedMultiplier += config.FrenzySpeedIncrease;
                AttackSpeedMultiplier += config.FrenzySpeedIncrease;
                break;
            case DevilAuraType.Hardening:
                DamageReductionPercent = config.HardeningDamageReduction;
                if (enemyHealth != null)
                {
                    enemyHealth.ApplyDamageReduction(DamageReductionPercent, -1f); 
                }
                regenCoroutine = StartCoroutine(RegenerationRoutine());
                break;
            case DevilAuraType.Stunning:
                StunningStunChance = config.StunningStunChance;
                CritDamageMultiplier += config.StunningCritDamageIncrease; 
                break;
            case DevilAuraType.Explosive:
                break;
            case DevilAuraType.PartialResurrection:
                break;
        }

        BroadcastMessage("UpdateAuraStats", SendMessageOptions.DontRequireReceiver);

        ReportDebug($"Aura '{ActiveAura}' aplicada. Multiplicador de Velocidad: {MoveSpeedMultiplier}", 2);
    }

    private IEnumerator RegenerationRoutine()
    {
        if (enemyHealth == null || config == null) yield break;

        float regenRate = config.HardeningHealthRegenPerSecond;
        float delay = 1f; 

        while (true)
        {
            yield return new WaitForSeconds(delay);
            if (!enemyHealth.IsDead)
            {
                enemyHealth.Heal(enemyHealth.MaxHealth * regenRate);
            }
        }
    }

    public void HandleDeathEffect(Transform deathLocation)
    {
        if (enemyHealth != null && enemyHealth.ItemEffectHandledDeath)
        {
            ReportDebug("El amuleto del jugador ya manejo el efecto de muerte. Ignorando Aura del Diablo.", 1);
            return;
        }

        switch (ActiveAura)
        {
            case DevilAuraType.Explosive:
                Explode(deathLocation);
                break;
            case DevilAuraType.PartialResurrection:
                ResurrectAsMinions(deathLocation);
                break;
        }

        if (regenCoroutine != null) StopCoroutine(regenCoroutine);
    }

    private void Explode(Transform explosionCenter)
    {
        float damage = enemyHealth.MaxHealth * config.ExplosiveDamagePercent;
        float radius = config.ExplosiveRadius;

        ReportDebug($"Explosion activada. Dano: {damage} en radio {radius}.", 3);

        Collider[] hitColliders = Physics.OverlapSphere(explosionCenter.position, radius);
        foreach (var hit in hitColliders)
        {
            if (hit.CompareTag("Player"))
            {
                hit.GetComponent<IDamageable>()?.TakeDamage(damage); 
            }
        }
    }

    private void ResurrectAsMinions(Transform spawnCenter)
    {
        if (config.EscurridizoPrefab == null)
        {
            ReportDebug("Prefab de Escurridizo no asignado. No se pueden generar minions.", 3);
            return;
        }

        for (int i = 0; i < config.ResurrectionSplitCount; i++)
        {
            Vector3 offset = UnityEngine.Random.insideUnitSphere * 1.5f;
            offset.y = 0; 
            GameObject minion = Instantiate(config.EscurridizoPrefab, spawnCenter.position + offset, Quaternion.identity);

            // minion.GetComponent<Larva>()?.ApplyResurrectionEffect(ActiveResurrectionLevel); 
        }

        ReportDebug($"Resurreccion Parcial: Generados {config.ResurrectionSplitCount} minions (Nivel {ActiveResurrectionLevel}).", 3);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void ReportDebug(string message, int reportPriorityLevel)
    {
        switch (reportPriorityLevel)
        {
            case 1: Debug.Log($"[EnemyAuraManager] {message}"); break;
            case 2: Debug.LogWarning($"[EnemyAuraManager] {message}"); break;
            case 3: Debug.LogError($"[EnemyAuraManager] {message}"); break;
        }
    }
}