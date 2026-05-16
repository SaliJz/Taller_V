using System.Collections;
using UnityEngine;

#pragma warning disable 0649 // Private fields are read defensively and may be assigned by disabled/editor flows.

public class AporiaEnemyLevel2 : AporiaEnemyBase
{
    #region Nivel 2 — Configuración Vidrio
    [Header("Fragmentos de Vidrio — Ataque")]
    [SerializeField] private GameObject glassShardAttackPrefab;
    [SerializeField] private float shardAttackRadius = 1f;
    [SerializeField] private float shardAttackDamagePerSec = 4f;
    [SerializeField] private float shardAttackDuration = 3f;

    [Header("Estela de Vidrio — Dash")]
    [SerializeField] private GameObject glassShardDashPrefab;
    [SerializeField] private float shardDashDamagePerSec = 4f;
    [SerializeField] private float shardDashDuration = 1f;
    [SerializeField] private float shardDashSpacing = 1f;

    [Header("Muerte — Explosión")]
    [SerializeField] private float deathWarningDuration = 1.5f;
    [SerializeField] private float deathExplosionRadius = 3f;
    [SerializeField] private float deathExplosionDamage = 15f;
    [SerializeField] private float deathExplosionKnockback = 10f;
    [SerializeField] private GameObject glassShardDeathPrefab;
    [SerializeField] private float shardDeathRadius = 1f;
    [SerializeField] private float shardDeathDamagePerSec = 4f;
    [SerializeField] private float shardDeathDuration = 2f;
    [SerializeField] private AudioClip explosionSFX;

    [Header("QuickSheet Balance")]
    [SerializeField] private Enemies enemiesSheet;
    [SerializeField] private int ENEMY_ID = 7;

    #endregion

    #region Referencias Coroutines
    private Coroutine _dashCoroutine;
    private Coroutine _attackCoroutine;
    private Coroutine _deathCoroutine;
    #endregion

    #region
    protected override void Awake()
    {
        //LoadStatsFromSheet();
        base.Awake();
    }

    private void LoadStatsFromSheet()
    {
        if (enemiesSheet == null)
        {
            Debug.LogWarning($"[AporiaEnemyLevel1] No hay Enemies asset asignado en {name}. Usando valores del Inspector.");
            return;
        }

        foreach (var row in enemiesSheet.dataArray)
        {
            if (row.ID != ENEMY_ID) continue;

            health = row.Health;
            moveSpeed = row.Movespeed;
            attackDamage = row.Regulardamage;

            EnemyToughness toughnessComp = GetComponent<EnemyToughness>();
            if (toughnessComp != null)
            {
                if (row.Superarmor > 0)
                {
                    toughnessComp.SetUseToughness(true);
                    toughnessComp.SetMaxToughness(row.Superarmor);
                }
                else
                {
                    toughnessComp.SetUseToughness(false);
                }
            }

            shardAttackDamagePerSec = row.Minedamage;
            shardDashDamagePerSec = row.Dashtraildamage;
            deathExplosionDamage = row.Explosionareadamage;

            if (row.Attackfrequency > 0f)
            {
                float interval = 1f / row.Attackfrequency;
                cooldownShort = interval * 0.75f;
                cooldownMedium = interval;
                cooldownLong = interval * 1.5f;
            }

            Debug.Log($"[AporiaEnemyLevel2] ID {ENEMY_ID} cargado. SA: {row.Superarmor}, Mina: {row.Minedamage}");
            return;
        }

        Debug.LogWarning($"[AporiaEnemyLevel1] ID {ENEMY_ID} no encontrado en el sheet.");
    }
    #endregion

    #region Ataque
    public override void OnAttackHit()
    {
        if (audioSource && attackSFX) audioSource.PlayOneShot(attackSFX);

        Collider[] targets = Physics.OverlapSphere(hitPoint.position, attackRadius, playerLayer);
        foreach (var t in targets)
        {
            if (t.TryGetComponent<PlayerHealth>(out var pHealth))
            {
                pHealth.TakeDamage(attackDamage);
                ApplyKnockback(t.transform);
                SpawnGlassArea(glassShardAttackPrefab, t.transform.position, shardAttackRadius,
                               shardAttackDamagePerSec, shardAttackDuration);
            }
        }
    }
    #endregion

    #region Dash
    protected override IEnumerator PerformDash(Vector3 startPos, Vector3 dashEnd, Vector3 attackDirection)
    {
        float elapsed = 0;
        float distanceCovered = 0;
        Vector3 lastShardPos = startPos;

        while (elapsed < dashDuration)
        {
            elapsed += Time.deltaTime;
            Vector3 nextPos = Vector3.Lerp(startPos, dashEnd, elapsed / dashDuration);

            if (Physics.Raycast(nextPos + Vector3.up, Vector3.down, 2f, groundLayer))
                transform.position = nextPos;

            distanceCovered += Vector3.Distance(lastShardPos, transform.position);
            if (distanceCovered >= shardDashSpacing)
            {
                SpawnGlassShard(glassShardDashPrefab, transform.position,
                                shardDashDamagePerSec, shardDashDuration);
                lastShardPos = transform.position;
                distanceCovered = 0;
            }

            if (Vector3.Distance(transform.position, playerTransform.position) < 1.2f)
                break;

            yield return null;
        }
    }
    #endregion

    #region Muerte
    protected override void HandleDeath(GameObject e)
    {
        if (e != gameObject) return;
        _deathCoroutine = StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        if (_dashCoroutine != null) StopCoroutine(_dashCoroutine);
        if (_attackCoroutine != null) StopCoroutine(_attackCoroutine);

        agent.isStopped = true;
        agent.enabled = false;
        isAttacking = true;

        if (animCtrl) animCtrl.SendMessage("PlayDeathWarning", SendMessageOptions.DontRequireReceiver);
        yield return new WaitForSeconds(deathWarningDuration);

        if (audioSource && explosionSFX) audioSource.PlayOneShot(explosionSFX);
        else if (audioSource && deathSFX) audioSource.PlayOneShot(deathSFX);

        TriggerExplosion();

        animCtrl?.PlayDeath();

        GameObject shard = Instantiate(glassShardDeathPrefab, transform.position, Quaternion.identity);
        shard.transform.localScale = Vector3.one * (shardDeathRadius * 2f);

        if (shard.TryGetComponent<GlassShardDamage>(out var dmg))
        {
            dmg.damagePerSecond = shardDeathDamagePerSec;
            dmg.playerLayer = playerLayer;
            dmg.shardDeathDuration = shardDeathDuration;
        }

        this.enabled = false;
    }

    private void TriggerExplosion()
    {
        Collider[] targets = Physics.OverlapSphere(transform.position, deathExplosionRadius, playerLayer);
        foreach (var t in targets)
        {
            if (t.TryGetComponent<PlayerHealth>(out var pHealth))
            {
                pHealth.TakeDamage(deathExplosionDamage);
                ApplyKnockbackWithForce(t.transform, deathExplosionKnockback);
            }
        }
    }
    #endregion

    #region Helpers
    private void SpawnGlassArea(GameObject prefab, Vector3 position, float radius,
                                 float damagePerSec, float duration)
    {
        if (prefab == null) return;
        GameObject shard = Instantiate(prefab, position, Quaternion.identity);
        shard.transform.localScale = Vector3.one * (radius * 2f);

        if (shard.TryGetComponent<GlassShardDamage>(out var dmg))
        {
            dmg.damagePerSecond = damagePerSec;
            dmg.playerLayer = playerLayer;
        }

        Destroy(shard, duration);
    }

    private void SpawnGlassShard(GameObject prefab, Vector3 position,
                                  float damagePerSec, float duration)
    {
        if (prefab == null) return;
        GameObject shard = Instantiate(prefab, position, Quaternion.identity);

        if (shard.TryGetComponent<GlassShardDamage>(out var dmg))
        {
            dmg.damagePerSecond = damagePerSec;
            dmg.playerLayer = playerLayer;
            dmg.shardDeathDuration = duration;
        }
    }

    private void ApplyKnockbackWithForce(Transform target, float force)
    {
        Vector3 dir = (target.position - transform.position).normalized;
        dir.y = 0;
        if (target.TryGetComponent<CharacterController>(out var cc))
            StartCoroutine(KnockbackTickCustom(cc, dir * force));
    }

    private IEnumerator KnockbackTickCustom(CharacterController cc, Vector3 force)
    {
        float t = 0;
        while (t < 0.25f) { t += Time.deltaTime; cc?.Move(force * Time.deltaTime); yield return null; }
    }
    #endregion
}