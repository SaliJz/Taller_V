using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "UniversalLarvaDashEffect", menuName = "Item Effects/Utility/Universal Larva Dash")]
public class ItemLarvaDashEffect : ItemEffectBase
{
    #region Settings

    [Header("Prefabs de Larvas")]
    public GameObject attackLarvaPrefab;
    public GameObject curativeLarvaPrefab;

    [Header("Ciclo")]
    [SerializeField] private float resetIdleTime = 2f; 

    #endregion

    #region Private Fields

    private int dashCount = 0;
    private float lastDashTime = -999f;

    #endregion

    #region ItemEffectBase

    private void OnEnable()
    {
        EffectID = "Larva Dash";
        category = EffectCategory.Combat;
    }

    public override void ApplyEffect(PlayerStatsManager statsManager)
    {
        dashCount = 0;
        lastDashTime = -999f;
        PlayerCombatEvents.OnDashStarted += HandleDashStarted;
    }

    public override void RemoveEffect(PlayerStatsManager statsManager)
    {
        PlayerCombatEvents.OnDashStarted -= HandleDashStarted;
    }

    #endregion

    #region Dash Handler

    private void HandleDashStarted(Vector3 playerPosition, Vector3 _)
    {
        float now = Time.time;

        if (now - lastDashTime > resetIdleTime)
            dashCount = 0;

        lastDashTime = now;
        dashCount++;

        Vector3 spawnPos = playerPosition;

        switch (dashCount)
        {
            case 1:
                SpawnLarva(curativeLarvaPrefab, spawnPos); 
                break;
            case 2:
                SpawnLarva(curativeLarvaPrefab, spawnPos); 
                break;
            case 3:
                SpawnLarva(curativeLarvaPrefab, spawnPos); 
                SpawnLarva(curativeLarvaPrefab, spawnPos);
                break;
            case 4:
            default:
                SpawnLarva(attackLarvaPrefab, spawnPos);   
                SpawnLarva(attackLarvaPrefab, spawnPos);
                break;
        }
    }

    #endregion

    #region Logic

    private void SpawnLarva(GameObject prefab, Vector3 position)
    {
        if (prefab == null) return;

        GameObject larva = Object.Instantiate(prefab, position, Quaternion.identity);
        larva.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

        if (larva.TryGetComponent<ResurrectedLarva>(out var res))
            res.Initialize(15f);
        else if (larva.TryGetComponent<CurativeLarva>(out var cur))
            cur.Initialize(15f);
    }

    #endregion
}