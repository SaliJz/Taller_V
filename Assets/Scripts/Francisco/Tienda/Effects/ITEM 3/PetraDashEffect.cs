using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(fileName = "SedimentsOfPetraDashEffect", menuName = "Item Effects/Combat/SedimentsOfPetraDash")]
public class PetraDashEffect : ItemEffectBase
{
    #region Inspector Fields

    [Header("Picos de Dash")]
    [Range(0.01f, 2f)]
    [SerializeField] private float dashSpikeDamagePercent = 0.40f;
    [SerializeField] private float dashSpikeDuration = 0.5f;
    [SerializeField] private int spikesPerBurst = 4;
    [SerializeField] private float spawnSpacingMin = 0.4f;
    [SerializeField] private float burstDelayMin = 0f;
    [SerializeField] private float burstDelayMax = 0.09f;

    [Header("Variacion Visual de Picos")]
    [SerializeField] private float tiltAngleCenter = 20f;
    [SerializeField] private float tiltAngleSide = 35f;
    [SerializeField] private float maxSideYawAngle = 18f;
    [SerializeField] private float scaleMin = 0.7f;
    [SerializeField] private float scaleMax = 1.25f;

    [Header("Rastro Visual en Suelo")]
    [SerializeField] private Material trailMaterial;
    [SerializeField] private float trailWidth = 0.3f;
    [SerializeField] private Color trailColor = new Color(0.45f, 0.28f, 0.10f, 0.80f);
    [SerializeField] private float trailFadeDuration = 2f;

    [Header("Compartido")]
    [SerializeField] private LayerMask enemyLayer;

    #endregion

    #region Private Fields

    private PlayerStatsManager _statsManager;
    private Vector3 lastSpikeSpawnPos = Vector3.positiveInfinity;
    private GameObject activeTrailGO;
    private LineRenderer activeTrail;
    private List<Vector3> trailPoints = new List<Vector3>();

    #endregion

    #region ItemEffectBase

    private void OnEnable()
    {
        EffectID = "Sedimentos de Petra Dash";
        category = EffectCategory.Combat;
    }

    public override void ApplyEffect(PlayerStatsManager statsManager)
    {
        _statsManager = statsManager;
        ResetState();
        PlayerCombatEvents.OnDashStarted += HandleDashStarted;
    }

    public override void RemoveEffect(PlayerStatsManager statsManager)
    {
        PlayerCombatEvents.OnDashStarted -= HandleDashStarted;
        _statsManager = null;
        CloseCurrentTrail();
    }

    #endregion

    #region Dash Handler

    private void HandleDashStarted(Vector3 playerPosition, Vector3 dashDirection)
    {
        Vector3 groundPos = new Vector3(playerPosition.x, 0.02f, playerPosition.z);

        AddTrailPoint(groundPos);
        CloseCurrentTrail();

        bool tooClose = !float.IsPositiveInfinity(lastSpikeSpawnPos.x) &&
                        Vector3.Distance(groundPos, lastSpikeSpawnPos) < spawnSpacingMin;
        if (tooClose) return;

        lastSpikeSpawnPos = groundPos;

        float damage = 50f * dashSpikeDamagePercent;

        for (int i = 0; i < spikesPerBurst; i++)
        {
            float delay = Random.Range(burstDelayMin, burstDelayMax);
            SpawnDashSpikeDelayed(groundPos, dashDirection, damage, delay, i, spikesPerBurst);
        }
    }

    #endregion

    #region Spike Spawning

    private async void SpawnDashSpikeDelayed(
        Vector3 position,
        Vector3 dashDirection,
        float damage,
        float delay,
        int spikeIndex,
        int totalSpikes)
    {
        if (delay > 0f)
            await Task.Delay(System.TimeSpan.FromSeconds(delay));

        if (ItemEffectPool.Instance == null) return;

        float yaw = 0f;
        float tilt = tiltAngleCenter;

        if (totalSpikes > 1)
        {
            float t = (float)spikeIndex / (totalSpikes - 1);
            yaw = Mathf.Lerp(-maxSideYawAngle, maxSideYawAngle, t);

            float absT = Mathf.Abs(t - 0.5f) * 2f;
            tilt = Mathf.Lerp(tiltAngleCenter, tiltAngleSide, absT);

            float lateralSign = (spikeIndex % 2 == 0) ? 1f : -1f;
            tilt *= lateralSign;
        }

        Vector3 spikeDir = Quaternion.Euler(0f, yaw, 0f) * dashDirection;
        Quaternion rotation = Quaternion.LookRotation(spikeDir) * Quaternion.Euler(tilt, 0f, 0f);
        float scale = Random.Range(scaleMin, scaleMax);

        ItemEffectPool.Instance.SpawnSpikeWithScale(
            position, rotation, damage, dashSpikeDuration, enemyLayer, isLargeSpike: true, scale: scale);
    }

    #endregion

    #region Trail Visual

    private void AddTrailPoint(Vector3 point)
    {
        if (activeTrailGO == null)
        {
            activeTrailGO = new GameObject("PetraDashTrail");
            activeTrail = activeTrailGO.AddComponent<LineRenderer>();
            SetupLineRenderer(activeTrail);
            trailPoints.Clear();
        }

        trailPoints.Add(point);
        activeTrail.positionCount = trailPoints.Count;
        activeTrail.SetPositions(trailPoints.ToArray());
    }

    private void SetupLineRenderer(LineRenderer lr)
    {
        lr.useWorldSpace = true;
        lr.startWidth = trailWidth;
        lr.endWidth = trailWidth * 0.4f;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 4;
        lr.alignment = LineAlignment.TransformZ;
        lr.material = trailMaterial != null ? trailMaterial : new Material(Shader.Find("Sprites/Default"));
        lr.startColor = trailColor;
        lr.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
    }

    private void CloseCurrentTrail()
    {
        if (activeTrailGO == null) return;
        PetraTrailFader fader = activeTrailGO.AddComponent<PetraTrailFader>();
        fader.Init(activeTrail, trailFadeDuration);
        activeTrailGO = null;
        activeTrail = null;
        trailPoints.Clear();
    }

    #endregion

    #region Helpers

    private void ResetState()
    {
        lastSpikeSpawnPos = Vector3.positiveInfinity;
        activeTrailGO = null;
        activeTrail = null;
        trailPoints.Clear();
    }

    #endregion
}