using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(fileName = "SedimentsOfPetraMeleeEffect", menuName = "Item Effects/Combat/SedimentsOfPetraMelee")]
public class PetraMeleeEffect : ItemEffectBase
{
    #region Inspector Fields
    [Header("Abanico de Picos - Melee")]
    [SerializeField] [Range(0.01f, 2f)] private float largeSpikeDamagePercent = 0.50f;
    [SerializeField] private float largeSpikeDuration = 0.3f;
    [SerializeField] private int largeSpikeCount = 3;
    [SerializeField] private float spikeSpreadAngle = 30f;
    [SerializeField] private float spikeForwardDistance = 2f;
    [SerializeField] private float spikeRandomDelayMin = 0f;
    [SerializeField] private float spikeRandomDelayMax = 0.12f;
    [SerializeField] private float largeSpikeAngle = 25f;

    [Header("Ajustes de Posicionamiento")]
    [SerializeField] private float spawnHeightOffset = -0.1f; 
    [SerializeField] private float meleeSpikeScale = 1.2f;

    [Header("Compartido")]
    [SerializeField] private LayerMask enemyLayer;

    #endregion

    #region ItemEffectBase

    private void OnEnable()
    {
        EffectID = "Sedimentos de Petra Melee";
        category = EffectCategory.Combat;
    }

    public override void ApplyEffect(PlayerStatsManager statsManager)
    {
        PlayerCombatEvents.OnMeleeHit += HandleMeleeHit;
    }

    public override void RemoveEffect(PlayerStatsManager statsManager)
    {
        PlayerCombatEvents.OnMeleeHit -= HandleMeleeHit;
    }

    #endregion

    #region Melee Handlers

    private void HandleMeleeHit(Vector3 playerPosition, Vector3 playerForward, float meleeDamage)
    {
        float spikeDamage = meleeDamage * largeSpikeDamagePercent;
        float angleStep = largeSpikeCount > 1 ? (spikeSpreadAngle * 2f) / (largeSpikeCount - 1) : 0f;
        float startAngle = largeSpikeCount > 1 ? -spikeSpreadAngle : 0f;

        for (int i = 0; i < largeSpikeCount; i++)
        {
            float angle = startAngle + angleStep * i;
            float delay = Random.Range(spikeRandomDelayMin, spikeRandomDelayMax);

            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * playerForward;
            Vector3 spawnPos = new Vector3(
                    playerPosition.x + dir.x * spikeForwardDistance,
                    spawnHeightOffset,
                    playerPosition.z + dir.z * spikeForwardDistance);

            Quaternion rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(largeSpikeAngle, 0f, 0f);

            SpawnSpikeDelayed(spawnPos, rotation, spikeDamage, delay, meleeSpikeScale);
        }
    }

    private async void SpawnSpikeDelayed(Vector3 position, Quaternion rotation, float damage, float delay, float scale)
    {
        if (delay > 0f) await Task.Delay(System.TimeSpan.FromSeconds(delay));

        if (ItemEffectPool.Instance != null)
        {
            ItemEffectPool.Instance.SpawnSpike(position, rotation, damage, largeSpikeDuration, enemyLayer, true, scale);
        }
    }

    #endregion
}
