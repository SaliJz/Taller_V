using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[CreateAssetMenu(fileName = "SedimentsOfPetraDashEffect", menuName = "Item Effects/Combat/SedimentsOfPetraDash")]
public class PetraDashEffect : ItemEffectBase
{
    [Header("Picos de Dash")]
    [Range(0.01f, 2f)]
    [SerializeField] private float dashSpikeDamagePercent = 0.40f;
    [SerializeField] private float dashSpikeDuration = 0.5f;
    [SerializeField] private int spikesPerBurst = 4;
    [SerializeField] private float spawnSpacingMin = 0.4f;
    [SerializeField] private float burstDelayMin = 0f;
    [SerializeField] private float burstDelayMax = 0.09f;

    [Header("Ajustes de Delay")]
    [SerializeField] private float initialDelay = 0.1f;

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

    private PlayerStatsManager _statsManager;
    private Vector3 lastSpikeSpawnPos = Vector3.positiveInfinity;
    private GameObject activeTrailGO;
    private LineRenderer activeTrail;
    private List<Vector3> trailPoints = new List<Vector3>();

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

    private void HandleDashStarted(Vector3 playerPosition, Vector3 dashDirection)
    {
        _statsManager.StartCoroutine(GenerateSpikesDuringDash(dashDirection));
    }

    private System.Collections.IEnumerator GenerateSpikesDuringDash(Vector3 dashDirection)
    {
        yield return new WaitForSeconds(initialDelay);

        float elapsed = initialDelay;
        float dashDuration = 0.3f;

        lastSpikeSpawnPos = _statsManager.transform.position;
        Vector3 startPoint = new Vector3(lastSpikeSpawnPos.x, 0.02f, lastSpikeSpawnPos.z);

        AddTrailPoint(startPoint);
        SpawnBurst(startPoint, dashDirection);

        while (elapsed < dashDuration)
        {
            Vector3 currentPos = _statsManager.transform.position;
            float distSinceLast = Vector3.Distance(currentPos, lastSpikeSpawnPos);

            while (distSinceLast >= spawnSpacingMin)
            {
                Vector3 directionToCurrent = (currentPos - lastSpikeSpawnPos).normalized;
                Vector3 spawnPoint = lastSpikeSpawnPos + (directionToCurrent * spawnSpacingMin);

                lastSpikeSpawnPos = spawnPoint;
                Vector3 groundPoint = new Vector3(spawnPoint.x, 0.02f, spawnPoint.z);

                AddTrailPoint(groundPoint);
                SpawnBurst(groundPoint, dashDirection);

                distSinceLast = Vector3.Distance(currentPos, lastSpikeSpawnPos);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        CloseCurrentTrail(dashSpikeDuration);
    }

    private void SpawnBurst(Vector3 pos, Vector3 dir)
    {
        float damage = 50f * dashSpikeDamagePercent;
        for (int i = 0; i < spikesPerBurst; i++)
        {
            float delay = Random.Range(burstDelayMin, burstDelayMax);
            SpawnDashSpikeDelayed(pos, dir, damage, delay, i, spikesPerBurst);
        }
    }

    private async void SpawnDashSpikeDelayed(Vector3 position, Vector3 dashDirection, float damage, float delay, int spikeIndex, int totalSpikes)
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

        ItemEffectPool.Instance.SpawnSpikeWithScale(position, rotation, damage, dashSpikeDuration, enemyLayer, isLargeSpike: true, scale: scale);
    }

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

    private void CloseCurrentTrail(float overrideDuration = -1f)
    {
        if (activeTrailGO == null) return;
        PetraTrailFader fader = activeTrailGO.AddComponent<PetraTrailFader>();
        float finalFade = overrideDuration > 0 ? overrideDuration : trailFadeDuration;
        fader.Init(activeTrail, finalFade);

        activeTrailGO = null;
        activeTrail = null;
        trailPoints.Clear();
    }

    private void ResetState()
    {
        lastSpikeSpawnPos = Vector3.positiveInfinity;
        activeTrailGO = null;
        activeTrail = null;
        trailPoints.Clear();
    }
}