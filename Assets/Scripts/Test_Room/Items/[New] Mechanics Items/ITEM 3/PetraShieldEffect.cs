using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SedimentsOfPetraShieldEffect", menuName = "Item Effects/Combat/SedimentsOfPetraShield")]
public class PetraShieldEffect : ItemEffectBase
{
    #region Inspector Fields

    [Header("Pinchos de Rastro - Distancia")]
    [Range(0.01f, 1f)] public float smallSpikeDamagePercent = 0.10f;
    public float smallSpikeDuration = 4f;
    public float minDistanceBetweenSpikes = 1.5f;
    public float smallSpikeTiltAngle = 20f;
    [SerializeField] private float spikeScale = 1f; 

    [Header("Rastro Visual en Suelo")]
    public Material trailMaterial;
    public float trailWidth = 0.25f;
    public Color trailColor = new Color(0.45f, 0.28f, 0.10f, 0.75f);
    public float trailFadeDuration = 3f;

    [Header("Compartido")]
    public LayerMask enemyLayer;

    #endregion

    #region Private Fields

    private Vector3 lastSpikePosition = Vector3.positiveInfinity;
    private Vector3 lastShieldPosition = Vector3.positiveInfinity;
    private GameObject activeTrailGO;
    private LineRenderer activeTrail;
    private List<Vector3> trailPoints = new List<Vector3>();

    #endregion

    #region ItemEffectBase

    private void OnEnable()
    {
        EffectID = "Sedimentos de Petra Shield";
        category = EffectCategory.Combat;
    }

    public override void ApplyEffect(PlayerStatsManager statsManager)
    {
        lastSpikePosition = Vector3.positiveInfinity;
        lastShieldPosition = Vector3.positiveInfinity;
        trailPoints.Clear();
        activeTrailGO = null;
        activeTrail = null;

        PlayerCombatEvents.OnShieldMoved += HandleShieldMoved;
        PlayerCombatEvents.OnShieldLanded += HandleShieldLanded;
    }

    public override void RemoveEffect(PlayerStatsManager statsManager)
    {
        PlayerCombatEvents.OnShieldMoved -= HandleShieldMoved;
        PlayerCombatEvents.OnShieldLanded -= HandleShieldLanded;
        CloseCurrentTrail();
    }

    #endregion

    #region Shield Handlers

    private void HandleShieldMoved(Vector3 shieldPosition, float playerBaseDamage)
    {
        Vector3 groundPos = new Vector3(shieldPosition.x, 0.02f, shieldPosition.z);
        AddTrailPoint(groundPos);

        if (float.IsPositiveInfinity(lastSpikePosition.x))
        {
            lastSpikePosition = groundPos;
            lastShieldPosition = shieldPosition;
            SpawnSingleSpike(groundPos, Vector3.forward, playerBaseDamage);
            return;
        }

        float distSinceLast = Vector3.Distance(groundPos, lastSpikePosition);

        while (distSinceLast >= minDistanceBetweenSpikes)
        {
            Vector3 direction = (groundPos - lastSpikePosition).normalized;
            Vector3 spawnPoint = lastSpikePosition + (direction * minDistanceBetweenSpikes);

            lastSpikePosition = spawnPoint;

            Vector3 travelDir = (shieldPosition - lastShieldPosition).normalized;
            if (travelDir == Vector3.zero) travelDir = Vector3.forward;

            SpawnSingleSpike(spawnPoint, travelDir, playerBaseDamage);

            distSinceLast = Vector3.Distance(groundPos, lastSpikePosition);
        }

        lastShieldPosition = shieldPosition;
    }

    private void SpawnSingleSpike(Vector3 position, Vector3 travelDir, float playerBaseDamage)
    {
        Vector3 flatDir = new Vector3(travelDir.x, 0f, travelDir.z).normalized;
        Quaternion rotation = Quaternion.LookRotation(flatDir) * Quaternion.Euler(-smallSpikeTiltAngle, 0f, 0f);
        float spikeDamage = playerBaseDamage * smallSpikeDamagePercent;

        if (ItemEffectPool.Instance != null)
        {
            ItemEffectPool.Instance.SpawnSpikeWithScale(
                position,
                rotation,
                spikeDamage,
                smallSpikeDuration,
                enemyLayer,
                isLargeSpike: false,
                scale: spikeScale);
        }
    }

    private void HandleShieldLanded()
    {
        CloseCurrentTrail();
        lastSpikePosition = Vector3.positiveInfinity;
        lastShieldPosition = Vector3.positiveInfinity;
    }

    #endregion

    #region Trail Visual

    private void AddTrailPoint(Vector3 point)
    {
        if (activeTrailGO == null)
        {
            activeTrailGO = new GameObject("PetraTrail");
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
        lr.endWidth = trailWidth * 0.5f;
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
}