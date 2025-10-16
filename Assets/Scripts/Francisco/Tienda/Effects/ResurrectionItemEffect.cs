using UnityEngine;

[CreateAssetMenu(fileName = "ResurrectionEffect", menuName = "Item Effects/Utility/Resurrection")]
public class ResurrectionItemEffect : ItemEffectBase
{
    [Header("Configuraci�n de Resurrecci�n")]
    public float resurrectionChance = 0.20f;
    public bool hasPriority = true;

    [Header("Larvas Explosivas")]
    public GameObject explosiveLarvaPrefab;
    public int totalLarvasToInstantiate = 3;

    [Header("Larva Curativa")]
    public GameObject curativeLarvaPrefab;
    public float curativeLarvaChance = 0.20f;

    public override void ApplyEffect(PlayerStatsManager statsManager)
    {
        CombatEventsManager.OnEnemyKilled += HandleEnemyResurrection;
        Debug.Log($"[ResurrectionEffect] Aplicado. Probabilidad de resurrecci�n: {resurrectionChance * 100}% de aparici�n del evento.");
    }

    public override void RemoveEffect(PlayerStatsManager statsManager)
    {
        CombatEventsManager.OnEnemyKilled -= HandleEnemyResurrection;
        Debug.Log("[ResurrectionEffect] Removido.");
    }

    private void HandleEnemyResurrection(GameObject killedEnemy, float enemyBaseHealth)
    {
        if (Random.value < resurrectionChance)
        {
            Debug.Log($"[ResurrectionEffect] Evento de resurrecci�n activado para {killedEnemy.name}.");

            bool shouldInstantiateCurative = Random.value < curativeLarvaChance;

            int curativeCount = shouldInstantiateCurative ? 1 : 0;
            int explosiveCount = totalLarvasToInstantiate - curativeCount;

            if (explosiveLarvaPrefab == null)
            {
                Debug.LogError("[ResurrectionEffect] explosiveLarvaPrefab es nulo.");
                return;
            }

            if (curativeCount > 0 && curativeLarvaPrefab != null)
            {
                InstantiateLarva(curativeLarvaPrefab, killedEnemy.transform.position, enemyBaseHealth, true);
            }
            else if (curativeLarvaPrefab == null && curativeCount > 0)
            {
                explosiveCount++;
            }

            for (int i = 0; i < explosiveCount; i++)
            {
                InstantiateLarva(explosiveLarvaPrefab, killedEnemy.transform.position, enemyBaseHealth, false);
            }
        }
    }

    private void InstantiateLarva(GameObject prefab, Vector3 position, float enemyBaseHealth, bool isCurative)
    {
        GameObject larva = Instantiate(prefab, position, Quaternion.identity);
        larva.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

        if (isCurative)
        {
            if (larva.TryGetComponent<CurativeLarva>(out var curativeScript))
            {
                curativeScript.Initialize(enemyBaseHealth);
            }
        }
        else
        {
            if (larva.TryGetComponent<ResurrectedLarva>(out var explosiveScript))
            {
                explosiveScript.Initialize(enemyBaseHealth);
            }
        }

        Debug.Log($"[ResurrectionEffect] Instanciada larva: {(isCurative ? "CURATIVA" : "EXPLOSIVA")}");
    }
}