using UnityEngine;

public class AporiaEnemyLevel3 : AporiaEnemyBase
{
    #region Nivel 3 — VFX
    [Header("Nivel 3 — VFX")]
    [SerializeField] private GameObject tongueVFXPrefab;
    [SerializeField] private GameObject nestPrefab;

    private GameObject pooledTongue;
    private GameObject pooledNest;
    #endregion

    #region Pools
    protected override void SetupPools()
    {
        if (tongueVFXPrefab) { pooledTongue = Instantiate(tongueVFXPrefab); pooledTongue.SetActive(false); }
        if (nestPrefab) { pooledNest = Instantiate(nestPrefab); pooledNest.SetActive(false); }
    }
    #endregion

    #region OnAttackHit: lengua + nido
    public override void OnAttackHit()
    {
        if (audioSource && attackSFX) audioSource.PlayOneShot(attackSFX);

        Vector3 spawnPosition = hitPoint.position;

        if (pooledTongue)
        {
            pooledTongue.transform.position = spawnPosition;
            pooledTongue.transform.rotation = transform.rotation;
            pooledTongue.SetActive(true);
            StartCoroutine(DeactivateAfterDelay(pooledTongue, 0.2f));
        }

        Collider[] targets = Physics.OverlapSphere(spawnPosition, attackRadius, playerLayer);
        foreach (var t in targets)
        {
            if (t.TryGetComponent<PlayerHealth>(out var pHealth))
            {
                pHealth.TakeDamage(attackDamage);
                ApplyKnockback(t.transform);
            }
        }

        if (pooledNest)
        {
            pooledNest.SetActive(false);
            pooledNest.transform.position = spawnPosition;
            pooledNest.SetActive(true);
        }
    }
    #endregion
}