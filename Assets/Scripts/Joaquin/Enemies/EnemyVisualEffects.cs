using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Clase que maneja los efectos visuales del enemigo
/// </summary>
public class EnemyVisualEffects : MonoBehaviour
{
    [Header("Renderers Configuration")]
    [Tooltip("Si se deja vacío, buscará MeshRenderers en los hijos.")]
    [SerializeField] private Renderer[] meshRenderers;
    [Tooltip("Si se deja vacío, buscará SpriteRenderers en los hijos.")]
    [SerializeField] private SpriteRenderer[] spriteRenderers;

    [Header("Configuración de Materiales")]
    [Tooltip("Si es true, se ignora 'defaultMaterial' y se usa el material que tenga el renderer al inicio.")]
    [SerializeField] private bool useOriginalMaterials = true;
    [Tooltip("Solo se usa si useOriginalMaterials es false.")]
    [SerializeField] private Material defaultMaterial;

    [Header("Flash / Blink Effect")]
    [SerializeField] private Material blinkFlashMaterial;
    [SerializeField] private float blinkInterval = 0.06f;
    [SerializeField] private int blinkCount = 6;

    [Header("Stun Effect")]
    [SerializeField] private Material stunMaterial;
    [SerializeField] private Color stunGlowColor = Color.yellow;
    [SerializeField] private float stunBlinkSpeed = 0.1f;
    [SerializeField] private GameObject stunVFXPrefab;
    [SerializeField] private float stunVFXHeight = 2f;
    [SerializeField] private AudioClip stunSFX;

    [Header("Armor Glow Effect")]
    [SerializeField] private Material glowMaterial;
    [SerializeField] private float glowIntensity = 2f;

    [Header("Damage Numbers")]
    [SerializeField] private GameObject damageNumberPrefab;
    [SerializeField] private Transform damageNumberParent;
    [SerializeField] private Color normalColor = Color.red;
    [SerializeField] private Color criticalColor = new Color(0.5f, 0f, 0f);

    [Header("Audio Feedback")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip normalHitSFX;
    [SerializeField] private AudioClip criticalHitSFX;

    private bool isStunned = false;
    private Coroutine stunEffectCoroutine = null;
    private GameObject activeStunVFX;
    private Coroutine glowCoroutine;

    private Dictionary<Component, Coroutine> activeBlinkRoutines = new Dictionary<Component, Coroutine>();

    private Dictionary<Renderer, Material> originalMeshMats = new Dictionary<Renderer, Material>();
    private Dictionary<SpriteRenderer, Material> originalSpriteMats = new Dictionary<SpriteRenderer, Material>();

    private List<GameObject> activePersistentEffects = new List<GameObject>();

    #region Unity Lifecycle

    private void Awake()
    {
        ValidateRenderers();
        CacheOriginalMaterials();
    }

    private void OnEnable()
    {
        RestoreAllOriginalMaterials();
    }

    private void OnDisable()
    {
        CleanupAllEffects(true);
    }

    private void OnDestroy()
    {
        CleanupAllEffects(true);
    }

    #endregion

    #region Initialization

    private void ValidateRenderers()
    {
        if (meshRenderers == null || meshRenderers.Length == 0)
            meshRenderers = GetComponentsInChildren<Renderer>();

        if (spriteRenderers == null || spriteRenderers.Length == 0)
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>();

        if ((meshRenderers == null || meshRenderers.Length == 0) && (spriteRenderers == null || spriteRenderers.Length == 0))
        {
            Debug.LogWarning($"[EnemyVisualEffects] No se encontraron Renderers ni Sprites en {gameObject.name}.");
        }
    }

    private void CacheOriginalMaterials()
    {
        originalMeshMats.Clear();
        originalSpriteMats.Clear();

        if (meshRenderers != null)
        {
            foreach (var r in meshRenderers)
            {
                if (r != null)
                {
                    originalMeshMats[r] = useOriginalMaterials ? r.sharedMaterial : defaultMaterial;

                    r.material = originalMeshMats[r];
                }
            }
        }

        if (spriteRenderers != null)
        {
            foreach (var s in spriteRenderers)
            {
                if (s != null)
                {
                    originalSpriteMats[s] = useOriginalMaterials ? s.sharedMaterial : defaultMaterial;
                    s.material = originalSpriteMats[s];
                }
            }
        }
    }

    #endregion

    #region Core Cleanup

    private void CleanupAllEffects(bool forceImmediate = false)
    {
        StopAllCoroutines();

        stunEffectCoroutine = null;
        glowCoroutine = null;
        activeBlinkRoutines.Clear();

        if (activeStunVFX != null)
        {
            if (forceImmediate)
            {
                Destroy(activeStunVFX);
            }
            else
            {
                DetachAndStopStunVFX(activeStunVFX);
            }
            activeStunVFX = null;
        }

        for (int i = activePersistentEffects.Count - 1; i >= 0; i--)
        {
            GameObject fx = activePersistentEffects[i];
            if (fx != null) Destroy(fx);
        }
        activePersistentEffects.Clear();

        RestoreAllOriginalMaterials();
    }

    private void RestoreAllOriginalMaterials()
    {
        if (meshRenderers != null)
        {
            foreach (var r in meshRenderers)
            {
                if (r != null && originalMeshMats.ContainsKey(r))
                    r.material = originalMeshMats[r];
            }
        }

        if (spriteRenderers != null)
        {
            foreach (var s in spriteRenderers)
            {
                if (s != null && originalSpriteMats.ContainsKey(s))
                    s.material = originalSpriteMats[s];
            }
        }

        SetRenderersActive(true);
    }

    #endregion

    public void PlayToughnessHitFeedback(Vector3 position)
    {
        ShowDamageNumber(position, 0, false);
    }

    #region Damage Feedback

    public void PlayDamageFeedback(Vector3 damagePosition, float damage, bool isCritical)
    {
        ShowDamageNumber(damagePosition, damage, isCritical);
        PlayHitSound(isCritical);

        if (isStunned) return;

        if (blinkFlashMaterial != null)
        {
            if (meshRenderers != null)
            {
                foreach (var r in meshRenderers)
                    if (r != null) StartBlinkRoutine(r, blinkFlashMaterial, blinkInterval, blinkCount);
            }

            if (spriteRenderers != null)
            {
                foreach (var s in spriteRenderers)
                    if (s != null) StartBlinkRoutine(s, blinkFlashMaterial, blinkInterval, blinkCount);
            }
        }
    }

    private void PlayHitSound(bool isCritical)
    {
        if (audioSource == null) return;
        AudioClip clip = isCritical ? criticalHitSFX : normalHitSFX;
        if (clip != null) audioSource.PlayOneShot(clip);
    }

    #endregion

    #region Generic Blink System

    private void StartBlinkRoutine(Component target, Material flashMat, float interval, int count)
    {
        if (target == null) return;

        if (activeBlinkRoutines.TryGetValue(target, out Coroutine existing))
        {
            if (existing != null) StopCoroutine(existing);
            activeBlinkRoutines.Remove(target);
            RestoreMaterialSingle(target);
        }

        Coroutine routine = StartCoroutine(BlinkCoroutine(target, flashMat, interval, count));
        activeBlinkRoutines[target] = routine;
    }

    private IEnumerator BlinkCoroutine(Component target, Material flashMat, float interval, int toggles)
    {
        Material originalMat = GetOriginalMaterial(target);
        if (originalMat == null)
        {
            activeBlinkRoutines.Remove(target);
            yield break;
        }

        bool isFlash = false;
        Renderer meshR = target as Renderer;
        SpriteRenderer spriteR = target as SpriteRenderer;

        for (int i = 0; i < toggles; i++)
        {
            if (target == null)
            {
                activeBlinkRoutines.Remove(target);
                yield break;
            }

            isFlash = !isFlash;
            Material matToUse = isFlash ? flashMat : originalMat;

            if (meshR != null)
            {
                meshR.material = matToUse;
            }
            else if (spriteR != null)
            {
                spriteR.material = matToUse;
            }

            yield return new WaitForSeconds(interval);
        }

        if (target != null)
        {
            RestoreMaterialSingle(target);
        }

        activeBlinkRoutines.Remove(target);
    }

    private Material GetOriginalMaterial(Component target)
    {
        if (target is Renderer r && originalMeshMats.ContainsKey(r)) return originalMeshMats[r];
        if (target is SpriteRenderer s && originalSpriteMats.ContainsKey(s)) return originalSpriteMats[s];
        return null;
    }

    private void RestoreMaterialSingle(Component target)
    {
        if (target == null) return;
        Material original = GetOriginalMaterial(target);
        if (original == null) return;

        if (target is Renderer r) r.material = original;
        else if (target is SpriteRenderer s) s.material = original;
    }

    #endregion

    #region Armor Glow

    public void StartArmorGlow()
    {
        if (glowMaterial == null) return;
        if (glowCoroutine != null) StopCoroutine(glowCoroutine);

        ApplyMaterialToAll(glowMaterial);

        glowCoroutine = StartCoroutine(AnimateGlowCoroutine());
    }

    public void StopArmorGlow()
    {
        if (glowCoroutine != null)
        {
            StopCoroutine(glowCoroutine);
            glowCoroutine = null;
        }
        RestoreAllOriginalMaterials();
    }

    private IEnumerator AnimateGlowCoroutine()
    {
        float baseIntensity = 1f;
        while (true)
        {
            float intensity = baseIntensity + Mathf.Sin(Time.time * 3f) * glowIntensity;

            UpdateGlowIntensity(meshRenderers, intensity);
            UpdateGlowIntensity(spriteRenderers, intensity);

            yield return null;
        }
    }

    private void UpdateGlowIntensity<T>(T[] renderers, float intensity) where T : Renderer
    {
        if (renderers == null) return;
        foreach (var r in renderers)
        {
            if (r != null && r.material.HasProperty("_EmissionIntensity"))
            {
                r.material.SetFloat("_EmissionIntensity", intensity);
            }
        }
    }

    #endregion

    #region Damage Numbers

    public void ShowDamageNumber(Vector3 position, float damage, bool isCritical = false)
    {
        if (damageNumberPrefab != null)
        {
            GameObject damageNumber = Instantiate(damageNumberPrefab, position, Quaternion.identity, damageNumberParent);
            DamageNumber script = damageNumber.GetComponent<DamageNumber>();
            if (script != null)
            {
                script.SetColor(normalColor, criticalColor);
                script.Initialize(damage, isCritical);
            }
        }
    }

    #endregion

    #region Stun Effect Management

    public void StartStunEffect(float duration)
    {
        activeBlinkRoutines.Clear();

        if (stunEffectCoroutine != null)
        {
            StopCoroutine(stunEffectCoroutine);
        }

        stunEffectCoroutine = StartCoroutine(StunEffectCoroutine(duration));
    }

    public void StopStunEffect()
    {
        if (stunEffectCoroutine != null)
        {
            StopCoroutine(stunEffectCoroutine);
            stunEffectCoroutine = null;
        }
        CleanupStunEffect();
    }

    private IEnumerator StunEffectCoroutine(float duration)
    {
        isStunned = true;

        if (stunMaterial != null) ApplyMaterialToAll(stunMaterial);
        else ApplyColorToAll(stunGlowColor);

        if (audioSource != null && stunSFX != null) audioSource.PlayOneShot(stunSFX);

        if (stunVFXPrefab != null)
        {
            if (activeStunVFX == null)
            {
                activeStunVFX = Instantiate(stunVFXPrefab, transform.position + Vector3.up * stunVFXHeight, Quaternion.identity, transform);
            }
        }

        float elapsed = 0f;
        float nextBlink = 0f;
        bool visible = true;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= nextBlink)
            {
                visible = !visible;
                SetRenderersActive(visible);
                nextBlink = elapsed + stunBlinkSpeed;
            }
            yield return null;
        }

        CleanupStunEffect();
    }

    private void CleanupStunEffect()
    {
        isStunned = false;

        SetRenderersActive(true);
        RestoreAllOriginalMaterials();

        if (activeStunVFX != null)
        {
            DetachAndStopStunVFX(activeStunVFX);
            activeStunVFX = null;
        }
    }

    private void DetachAndStopStunVFX(GameObject vfxToStop)
    {
        if (vfxToStop == null) return;

        if (vfxToStop.transform.parent == transform)
        {
            vfxToStop.transform.SetParent(null);
        }

        var ps = vfxToStop.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        else
        {
            Destroy(vfxToStop);
        }
    }

    #endregion

    #region Helpers

    private void ApplyMaterialToAll(Material mat)
    {
        if (meshRenderers != null)
        {
            foreach (var r in meshRenderers)
            {
                if (r != null) r.material = mat;
            }
        }

        if (spriteRenderers != null)
        {
            foreach (var s in spriteRenderers)
            {
                if (s != null) s.material = mat;
            }
        }
    }

    private void ApplyColorToAll(Color col)
    {
        if (meshRenderers != null)
        {
            foreach (var r in meshRenderers)
            {
                if (r != null && r.material.HasProperty("_Color")) r.material.color = col;
            }
        }

        if (spriteRenderers != null)
        {
            foreach (var s in spriteRenderers)
            {
                if (s != null) s.color = col;
            }
        }
    }

    private void SetRenderersActive(bool active)
    {
        if (meshRenderers != null)
        {
            foreach (var r in meshRenderers)
            {
                if (r != null) r.enabled = active;
            }
        }
        if (spriteRenderers != null)
        {
            foreach (var s in spriteRenderers)
            {
                if (s != null) s.enabled = active;
            }
        }
    }

    #endregion

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(transform.position, transform.position + (Vector3.up * stunVFXHeight));
    }
}