using UnityEngine;

public class AporiaEnemyLevel3 : AporiaEnemyBase
{
    [Header("Nivel 3 - Stats")]
    [SerializeField] private float currentLarvaSpawnRate = 2f;

    #region Inspector - Nivel 3 VFX

    [Header("Nivel 3 - VFX")]
    [SerializeField] private GameObject tongueVFXPrefab;
    [SerializeField] private GameObject nestPrefab;

    #endregion

    #region Inspector - QuickSheet Balance

    [Header("QuickSheet Balance")]
    [SerializeField] private Enemies enemiesSheet;
    [SerializeField] private int ENEMY_ID = 11;

    #endregion

    #region Internal State

    private GameObject pooledTongue;
    private GameObject pooledNest;

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        //LoadStatsFromSheet();
        base.Awake();
    }

    #endregion

    #region Initialization & Data Sync

    private void LoadStatsFromSheet()
    {
        if (enemiesSheet == null) return;

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

            currentLarvaSpawnRate = row.Larvaspawnrate;

            Debug.Log($"[AporiaLevel3] Cargado ID {ENEMY_ID}. SpawnRate: {currentLarvaSpawnRate}");
            return;
        }
    }

    protected override void SetupPools()
    {
        if (tongueVFXPrefab) 
        { 
            pooledTongue = Instantiate(tongueVFXPrefab); 
            pooledTongue.SetActive(false); 
        }

        if (nestPrefab)
        {
            pooledNest = Instantiate(nestPrefab); 
            pooledNest.SetActive(false);
            pooledNest.GetComponent<AporiaNest>()?.SetRateSpawn(currentLarvaSpawnRate); // Configura la tasa de spawn en el AporiaNest
        }
    }

    #endregion

    #region Core Health & Combat

    public override void OnAttackHit()
    {
        if (enemyHealth != null && (enemyHealth.IsStunned || enemyHealth.IsDead)) return;

        SpawnAttackVFX();

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