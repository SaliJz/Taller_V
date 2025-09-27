using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// ExplosiveHead
/// - Detecta proximidad
/// - Reproduce grito
/// - Tras priming explota, aplica daño y empuje
/// - Debug: OnGUI + Gizmos
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class ExplosiveHead : MonoBehaviour
{
    private enum HazardState { Armed, Priming, Exploded }
    private HazardState currentState = HazardState.Armed;

    [Header("Parámetros")]
    [Tooltip("Tiempo en segundos desde el grito hasta la explosión.")]
    [SerializeField] private float primingDuration = 1.2f;

    [Tooltip("Radio de la explosión para aplicar daño y empuje.")]
    [SerializeField] private float explosionRadius = 5f;

    [Tooltip("Daño máximo infligido por la explosión (en el centro).")]
    [SerializeField] private float explosionDamage = 40f;

    [Tooltip("Fuerza aplicada a Rigidbodies (AddExplosionForce).")]
    [SerializeField] private float rigidbodyKnockbackForce = 700f;

    [Tooltip("Distancia de empuje aplicada a CharacterController (sin Rigidbody).")]
    [SerializeField] private float ccKnockbackDistance = 3f;

    [Header("Comportamiento")]
    [Tooltip("Si true, el daño decrece con la distancia (falloff).")]
    [SerializeField] private bool useDamageFalloff = true;

    [Tooltip("Si true, la priming se cancelará si el jugador sale del área antes de explotar.")]
    [SerializeField] private bool cancelIfPlayerLeaves = false;

    [Header("Filtrado")]
    [Tooltip("Máscara para limitar qué capas reciben daño/knockback.")]
    [SerializeField] private LayerMask affectLayers = ~0;

    [Header("Referencias")]
    [SerializeField] private GameObject visuals;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private ParticleSystem explosionVFX;
    [SerializeField] private AudioClip screamSound;
    [SerializeField] private AudioClip explosionSound;

    [Header("Glow / Priming visuals")]
    [Tooltip("Renderer de la cabeza (para parpadeo/emission).")]
    [SerializeField] private Renderer headRenderer;
    [Tooltip("Color base de emisión (si el shader lo soporta).")]
    [SerializeField] private Color glowColor = Color.red;
    [Tooltip("Intensidad máxima de emisión en el momento de la explosión.")]
    [SerializeField] private float glowMaxIntensity = 3f;
    [Tooltip("Curva que define la progresión del glow de 0..1 (x) -> factor (y).")]
    [SerializeField] private AnimationCurve glowCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Jaw (mandíbula)")]
    [Tooltip("Transform usado como mandíbula (un cubo rotado sobre X para pruebas).")]
    [SerializeField] private Transform jawTransform;
    [Tooltip("Ángulo (grados) de la mandíbula cerrada (normal).")]
    [SerializeField] private float jawClosedAngle = 0f;
    [Tooltip("Ángulo (grados) de la mandíbula abierta en el máximo grito (negativo para rotar hacia abajo).")]
    [SerializeField] private float jawOpenAngle = -45f;
    [Tooltip("Curve para la apertura de la mandíbula (0..1 input -> 0..1 output).")]
    [SerializeField] private AnimationCurve jawCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Overlap settings")]
    [Tooltip("Cantidad máxima de colliders a procesar en la explosión (para OverlapSphereNonAlloc).")]
    [SerializeField] private int maxTargets = 32;

    [Header("Debug (OnGUI/Gizmos)")]
    [SerializeField] private bool showDebugHUD = true;
    [SerializeField, Tooltip("Activa logs más verbosos para debugging (desactivar en build).")]
    private bool verboseDebug = false;

    private Coroutine hazardRoutine;
    private SphereCollider triggerCollider;
    private Collider[] overlapResults;
    private HashSet<Collider> insideColliders = new HashSet<Collider>();
    private float primingTimeLeft = 0f;
    private MaterialPropertyBlock mpb;
    private int emissionColorID;

    private void Awake()
    {
        triggerCollider = GetComponent<SphereCollider>();
        if (triggerCollider == null)
        {
            Debug.LogError("[ExplosiveHead] Requiere SphereCollider.", this);
            enabled = false;
            return;
        }
        triggerCollider.isTrigger = true;

        overlapResults = new Collider[maxTargets];

        if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>();
        if (explosionVFX == null) explosionVFX = GetComponentInChildren<ParticleSystem>();

        mpb = new MaterialPropertyBlock();
        emissionColorID = Shader.PropertyToID("_EmissionColor");

        if (headRenderer == null) headRenderer = GetComponentInChildren<Renderer>();

        glowCurve = new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(0.6f, 0.18f),
                    new Keyframe(0.9f, 0.72f),
                    new Keyframe(1f, 1f)
                    );

        glowCurve = new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(0.6f, 0.18f),
                    new Keyframe(0.9f, 0.72f),
                    new Keyframe(1f, 1f)
                    );

        if (verboseDebug) Debug.Log($"[ExplosiveHead] Awake - trigger center (local): {triggerCollider.center}, radius: {triggerCollider.radius}");
    }

    private void Update()
    {
        if (currentState != HazardState.Armed) return;

        if (DetectTargetsInTrigger(out int count) && count > 0)
        {
            if (verboseDebug) Debug.Log($"[ExplosiveHead] DetectTargetsInTrigger -> {count} targets, iniciando priming.");
            if (hazardRoutine == null)
            {
                hazardRoutine = StartCoroutine(ActivationSequence());
            }
        }
    }

    /// <summary>
    /// Se activa cuando un objeto entra en el SphereCollider (Trigger).
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (!IsLayerInMask(other.gameObject.layer, affectLayers)) return;
        if (verboseDebug) Debug.Log($"[ExplosiveHead] OnTriggerEnter: {other.name}");
    }

    /// <summary>
    /// Se desactiva cuando un objeto entra en el SphereCollider (Trigger).
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        if (!IsLayerInMask(other.gameObject.layer, affectLayers)) return;
        if (verboseDebug) Debug.Log($"[ExplosiveHead] OnTriggerExit: {other.name}");
    }

    /// <summary>
    /// Corrutina que maneja la secuencia de grito -> espera -> explosión.
    /// </summary>
    private IEnumerator ActivationSequence()
    {
        currentState = HazardState.Priming;
        primingTimeLeft = primingDuration;
        PlayAudio(screamSound);

        float elapsed = 0f;
        while (elapsed < primingDuration)
        {
            yield return null;
            float dt = Time.deltaTime;
            elapsed += dt;
            primingTimeLeft = Mathf.Max(0f, primingDuration - elapsed);

            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, primingDuration));
            
            float glowFactor = glowCurve.Evaluate(t);
            SetGlow(glowFactor * glowMaxIntensity);

            if (jawTransform != null)
            {
                float jawT = jawCurve.Evaluate(t);
                float angle = Mathf.Lerp(jawClosedAngle, jawOpenAngle, jawT);
                jawTransform.localRotation = Quaternion.Euler(angle, 0f, 0f);
            }

            if (cancelIfPlayerLeaves)
            {
                bool someoneInside = DetectTargetsInTrigger(out int cnt) && cnt > 0;
                if (!someoneInside)
                {
                    if (verboseDebug) Debug.Log("[ExplosiveHead] Priming cancelado: ya no hay objetivos.");
                    ResetGlow();
                    ResetJaw();
                    currentState = HazardState.Armed;
                    hazardRoutine = null;
                    primingTimeLeft = 0f;
                    yield break;
                }
            }
        }

        primingTimeLeft = 0f;

        if (this != null && gameObject != null)
        {
            Explode();

            currentState = HazardState.Exploded;

            float vfxDuration = explosionVFX != null ? explosionVFX.main.duration : 0f;
            Destroy(gameObject, Mathf.Max(0.1f, vfxDuration + 0.2f));
        }
        hazardRoutine = null;
    }

    /// <summary>
    /// Gestiona la lógica de la explosión: daño, empuje y efectos.
    /// </summary>
    private void Explode()
    {
        if (explosionVFX != null)
        {
            explosionVFX.transform.position = transform.position;
            explosionVFX.Play();
        }

        PlayAudio(explosionSound);

        if (visuals != null) visuals.SetActive(false);

        Vector3 center = GetTriggerWorldCenter();
        float worldExplosionRadius = explosionRadius * GetMaxLossyScale();
        int found = Physics.OverlapSphereNonAlloc(center, worldExplosionRadius, overlapResults, affectLayers);
        if (verboseDebug) Debug.Log($"[ExplosiveHead] Explode found={found}");

        for (int i = 0; i < found; i++)
        {
            Collider collider = overlapResults[i];
            if (collider == null) continue;

            float distance = Vector3.Distance(transform.position, collider.transform.position);
            float normalizedDistance = Mathf.Clamp01(distance / Mathf.Max(0.0001f, worldExplosionRadius));
            float dmg = useDamageFalloff ? Mathf.Lerp(explosionDamage, 0f, normalizedDistance) : explosionDamage;

            IDamageable idamagable = collider.GetComponent<IDamageable>();
            if (idamagable != null)
            {
                idamagable.TakeDamage(dmg);
            }
            else
            {
                PlayerHealth playerHealth = collider.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(dmg);
                }
            }

            Rigidbody rb = collider.attachedRigidbody;
            if (rb != null)
            {
                rb.AddExplosionForce(rigidbodyKnockbackForce, transform.position, explosionRadius, 0.5f, ForceMode.Impulse);
            }
            else
            {
                CharacterController cc = collider.GetComponent<CharacterController>();
                if (cc != null)
                {
                    Vector3 direction = (collider.transform.position - transform.position);
                    direction.y = 0f;
                    if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward * 0.5f;
                    direction.Normalize();

                    float pushStrength = ccKnockbackDistance * (1f - normalizedDistance);
                    Vector3 displacement = (direction + Vector3.up * 0.3f) * pushStrength;

                    cc.Move(displacement);
                }
            }

            overlapResults[i] = null;
        }
    }

    /// <summary>
    /// Detecta colliders dentro del trigger (centro y radio del SphereCollider en world space).
    /// Rellena overlapResults y devuelve true si se han encontrado >0 colliders en affectLayers.
    /// </summary>
    private bool DetectTargetsInTrigger(out int foundCount)
    {
        foundCount = 0;
        Vector3 center = GetTriggerWorldCenter();
        float worldRadius = triggerCollider.radius * GetMaxLossyScale();

        int found = Physics.OverlapSphereNonAlloc(center, worldRadius, overlapResults, affectLayers.value);

        int real = 0;
        for (int i = 0; i < found; i++)
        {
            Collider c = overlapResults[i];
            if (c == null) continue;
            real++;
        }

        foundCount = real;
        for (int i = 0; i < found; i++) overlapResults[i] = null;

        return real > 0;
    }

    /// <summary>
    /// Devuelve el centro del SphereCollider en coordenadas mundo.
    /// </summary>
    private Vector3 GetTriggerWorldCenter()
    {
        if (triggerCollider == null) triggerCollider = GetComponent<SphereCollider>();
        // TransformPoint considera center local y escala/rotación del transform
        return transform.TransformPoint(triggerCollider.center);
    }

    /// <summary>
    /// Obtiene el mayor factor de escala para convertir radios locales a world space.
    /// </summary>
    private float GetMaxLossyScale()
    {
        Vector3 s = transform.lossyScale;
        return Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
    }

    private void ResetGlow()
    {
        if (headRenderer == null) return;
        headRenderer.GetPropertyBlock(mpb);
        mpb.SetColor(emissionColorID, Color.black);
        headRenderer.SetPropertyBlock(mpb);
    }

    private void SetGlow(float intensity)
    {
        if (headRenderer == null) return;
        headRenderer.GetPropertyBlock(mpb);
        Color c = glowColor * Mathf.Clamp01(intensity);
        mpb.SetColor(emissionColorID, c);
        headRenderer.SetPropertyBlock(mpb);
    }

    private void ResetJaw()
    {
        if (jawTransform != null) jawTransform.localRotation = Quaternion.Euler(jawClosedAngle, 0f, 0f);
    }

    private void PlayAudio(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    private static bool IsLayerInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    #region Gizmos & Debug HUD

    private void OnDrawGizmos()
    {
        if (triggerCollider == null) triggerCollider = GetComponent<SphereCollider>();
        if (triggerCollider != null)
        {
            Vector3 center = GetTriggerWorldCenter();
            float radiusWorld = triggerCollider.radius * GetMaxLossyScale();
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(center, radiusWorld);
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(GetTriggerWorldCenter(), explosionRadius * GetMaxLossyScale());
    }

    private void OnGUI()
    {
        if (!showDebugHUD) return;

        GUIStyle box = new GUIStyle(GUI.skin.box)
        {
            fontSize = 12,
            padding = new RectOffset(10, 10, 10, 10)
        };

        Rect rect = new Rect(10, 10, 320, 190);
        GUILayout.BeginArea(rect, box);

        GUILayout.Label($"ExplosiveHead Debug (obj: {gameObject.name})");
        GUILayout.Space(4);
        GUILayout.Label($"State: {currentState}");

        if (currentState == HazardState.Priming)
        {
            GUILayout.Label($"Time to explode (s): {primingTimeLeft:F2}");
        }
        GUILayout.Label($"Trigger center (world): {GetTriggerWorldCenter()}");
        GUILayout.Label($"Trigger radius (world): {triggerCollider.radius * GetMaxLossyScale():F2}");
        GUILayout.Label($"RigidbodyForce: {rigidbodyKnockbackForce:F0}  CC pushDist: {ccKnockbackDistance:F2}");

        GUILayout.Space(6);
        if (GUILayout.Button("Force Explode"))
        {
            if (hazardRoutine != null) StopCoroutine(hazardRoutine);
            Explode();
        }

        GUILayout.EndArea();
    }


    #endregion
}