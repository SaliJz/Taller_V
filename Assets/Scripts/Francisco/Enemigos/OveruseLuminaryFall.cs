using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class OveruseLuminaryFall : MonoBehaviour
{
    #region Enums

    private enum BeamType
    {
        None,
        Lantern,
        Projector
    }

    #endregion

    #region Inspector Fields

    [Header("Stomp Damage")]
    [SerializeField] private float stompDamage = 5f;
    [SerializeField] private float stompRadius = 3f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Beam Emission")]
    [SerializeField] private float beamGroundedDuration = 1.5f;

    [Header("Lantern Beam")]
    [SerializeField] private float lanternConeAngle = 60f;
    [SerializeField] private float lanternConeRadius = 7.5f;
    [SerializeField] private float lanternValuePerSecond = 0.15f;

    [Header("Projector Beam")]
    [SerializeField] private float projectorConeAngle = 120f;
    [SerializeField] private float projectorConeRadius = 5f;
    [SerializeField] private float projectorValuePerSecond = 0.3f;

    [Header("Deterioration")]
    [SerializeField] private int maxImpacts = 5;
    [SerializeField] private float noBeamChanceIncreasePerImpact = 0.1f;

    [Header("VFX")]
    [SerializeField] private GameObject lanternBeamVFXPrefab;
    [SerializeField] private GameObject projectorBeamVFXPrefab;
    [SerializeField] private GameObject flickerVFXPrefab;
    [SerializeField] private Transform beamOrigin;

    #endregion

    #region Private State

    private int impactCount = 0;
    private float currentNoBeamChance = 0f;

    private GameObject activeBeamVFX;
    private GameObject activeFlickerVFX;
    private Coroutine beamRoutine;
    private Coroutine flickerRoutine;

    private Transform playerTransform;
    private OveruseScreenManager playerScreenEffect;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            playerScreenEffect = OveruseScreenManager.Instance;

            if (playerScreenEffect == null)
                Log("OveruseScreenEffect Instance not found in scene.", 2);
        }
        else
        {
            Log("Player not found. Beam effects will not apply screen value.", 2);
        }
    }

    private void OnDestroy()
    {
        CleanupBeamVFX();
        CleanupFlickerVFX();
    }

    #endregion

    #region Public API

    public void OnLanded()
    {
        impactCount++;
        currentNoBeamChance = Mathf.Clamp01(noBeamChanceIncreasePerImpact * (impactCount - 1));

        ApplyStompDamage();

        BeamType selectedBeam = RollBeamType();

        Log($"Impact #{impactCount} | NoBeamChance: {currentNoBeamChance:P0} | Selected beam: {selectedBeam}", 1);

        if (beamRoutine != null)
            StopCoroutine(beamRoutine);

        beamRoutine = StartCoroutine(BeamRoutine(selectedBeam));
    }

    #endregion

    #region Stomp Damage

    private void ApplyStompDamage()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, stompRadius, playerLayer);
        foreach (Collider hit in hits)
        {
            IDamageable damageable = hit.GetComponent<IDamageable>();
            damageable?.TakeDamage(stompDamage, false, AttackDamageType.Melee);
        }

        Log($"Stomp applied {stompDamage} damage in radius {stompRadius}m.", 1);
    }

    #endregion

    #region Beam Selection

    private BeamType RollBeamType()
    {
        float roll = Random.value;

        if (roll < currentNoBeamChance)
            return BeamType.None;

        float lanternChance = (1f - currentNoBeamChance) * 0.5f;
        float projectorChance = (1f - currentNoBeamChance) * 0.5f;

        if (roll < currentNoBeamChance + lanternChance)
            return BeamType.Lantern;

        if (roll < currentNoBeamChance + lanternChance + projectorChance)
            return BeamType.Projector;

        return BeamType.None;
    }

    #endregion

    #region Beam Routine

    private IEnumerator BeamRoutine(BeamType beamType)
    {
        CleanupBeamVFX();

        if (beamType == BeamType.None)
        {
            Log("No beam emitted this impact (deterioration).", 1);
            flickerRoutine = StartCoroutine(FlickerRoutine());
            yield break;
        }

        SpawnBeamVFX(beamType);

        float elapsed = 0f;
        float coneAngle = (beamType == BeamType.Lantern) ? lanternConeAngle : projectorConeAngle;
        float coneRadius = (beamType == BeamType.Lantern) ? lanternConeRadius : projectorConeRadius;
        float valueRate = (beamType == BeamType.Lantern) ? lanternValuePerSecond : projectorValuePerSecond;

        while (elapsed < beamGroundedDuration)
        {
            if (playerTransform != null && IsPlayerInCone(coneAngle, coneRadius))
            {
                if (playerScreenEffect != null)
                {
                    playerScreenEffect.AddValue(valueRate);
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        CleanupBeamVFX();
        beamRoutine = null;
    }

    #endregion

    #region Cone Detection

    private bool IsPlayerInCone(float coneAngle, float coneRadius)
    {
        if (playerTransform == null) return false;

        Vector3 origin = beamOrigin != null ? beamOrigin.position : transform.position;
        Vector3 toPlayer = playerTransform.position - origin;
        float distance = toPlayer.magnitude;

        if (distance > coneRadius) return false;

        float angle = Vector3.Angle(transform.forward, toPlayer);
        return angle <= coneAngle * 0.5f;
    }

    #endregion

    #region Flicker Routine

    private IEnumerator FlickerRoutine()
    {
        CleanupFlickerVFX();

        if (flickerVFXPrefab == null) yield break;

        Vector3 spawnPosition = beamOrigin != null ? beamOrigin.position : transform.position;
        activeFlickerVFX = Instantiate(flickerVFXPrefab, spawnPosition, Quaternion.identity, transform);

        yield return new WaitForSeconds(beamGroundedDuration);

        CleanupFlickerVFX();
        flickerRoutine = null;
    }

    #endregion

    #region VFX Helpers

    private void SpawnBeamVFX(BeamType beamType)
    {
        GameObject prefab = beamType == BeamType.Lantern ? lanternBeamVFXPrefab : projectorBeamVFXPrefab;
        if (prefab == null) return;

        Vector3 spawnPosition = beamOrigin != null ? beamOrigin.position : transform.position;
        activeBeamVFX = Instantiate(prefab, spawnPosition, transform.rotation, transform);
    }

    private void CleanupBeamVFX()
    {
        if (activeBeamVFX != null)
        {
            Destroy(activeBeamVFX);
            activeBeamVFX = null;
        }
    }

    private void CleanupFlickerVFX()
    {
        if (activeFlickerVFX != null)
        {
            Destroy(activeFlickerVFX);
            activeFlickerVFX = null;
        }
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        DrawConeGizmo(lanternConeAngle, lanternConeRadius, new Color(0f, 0.8f, 1f, 0.2f));
        DrawConeGizmo(projectorConeAngle, projectorConeRadius, new Color(1f, 0.9f, 0f, 0.15f));

        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, stompRadius);
    }

    private void DrawConeGizmo(float angle, float radius, Color color)
    {
        Gizmos.color = color;

        Vector3 origin = beamOrigin != null ? beamOrigin.position : transform.position;
        float halfAngle = angle * 0.5f * Mathf.Deg2Rad;
        int segments = 20;

        Vector3 prevPoint = Vector3.zero;

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float currentAngle = Mathf.Lerp(-halfAngle, halfAngle, t);
            Vector3 direction = Quaternion.AngleAxis(currentAngle * Mathf.Rad2Deg, Vector3.up) * transform.forward;
            Vector3 point = origin + direction * radius;

            if (i > 0) Gizmos.DrawLine(prevPoint, point);
            Gizmos.DrawLine(origin, point);

            prevPoint = point;
        }
    }

    #endregion

    #region Debug

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void Log(string message, int level)
    {
        switch (level)
        {
            case 1: Debug.Log($"[OveruseLuminaryFall] {message}"); break;
            case 2: Debug.LogWarning($"[OveruseLuminaryFall] {message}"); break;
            case 3: Debug.LogError($"[OveruseLuminaryFall] {message}"); break;
        }
    }

    #endregion
}