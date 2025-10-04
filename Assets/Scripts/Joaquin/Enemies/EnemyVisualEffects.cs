using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Clase que maneja los efectos visuales del enemigo, incluyendo:
/// - Visualización de números de daño
/// - Parpadeo por material (material swap) mediante BlinkMaterial(...)
/// - Efecto de brillo (armor glow)
/// - Feedback de sonido
/// </summary>
public class EnemyVisualEffects : MonoBehaviour
{
    [Header("Renderers (opcional)")]
    [SerializeField] private Renderer[] renderers;

    [Header("Materials / Material Blink (material swap)")]
    [Tooltip("Renderer objetivo para el material-swap. Si se deja null, usa el primer renderer en 'renderers' (si existe).")]
    [SerializeField] private Renderer blinkTargetRenderer;
    [Tooltip("Material temporal que se usará para el parpadeo por swap (por ejemplo: blanco).")]
    [SerializeField] private Material blinkFlashMaterial;
    [Tooltip("Intervalo entre toggles (segundos). Ej: 0.06 -> parpadeo rápido.")]
    [SerializeField] private float blinkInterval = 0.06f;
    [Tooltip("Número total de toggles (on/off). Ej: 6 = 3 parpadeos completos).")]
    [SerializeField] private int blinkCount = 6;

    [Header("Materials (default / glow)")]
    [SerializeField] private Material defaultMaterial;
    [SerializeField] private Material glowMaterial;

    [Header("Armor Glow Effect")]
    [SerializeField] private float glowIntensity = 2f;

    [Header("Damage Numbers")]
    [SerializeField] private GameObject damageNumberPrefab;
    [SerializeField] private Transform damageNumberParent;

    [Header("Audio Feedback")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip normalHitSFX;
    [SerializeField] private AudioClip criticalHitSFX;

    // material blink management
    private Dictionary<Renderer, Coroutine> materialBlinkRoutines = new Dictionary<Renderer, Coroutine>();
    private Dictionary<Renderer, Material[]> originalMaterialsByRenderer = new Dictionary<Renderer, Material[]>();

    private void Awake()
    {
        if (renderers != null && defaultMaterial != null)
        {
            foreach (var r in renderers)
            {
                if (r != null)
                {
                    r.material = defaultMaterial;
                }
            }
        }

        // si no se asignó target específico, intentar usar el primero
        if (blinkTargetRenderer == null && renderers != null && renderers.Length > 0)
        {
            blinkTargetRenderer = renderers[0];
        }
    }

    private void OnEnable()
    {
        ResetVisuals();
    }

    private void OnDisable()
    {
        // detener y restaurar todos los parpadeos por material activos
        foreach (var kv in new List<KeyValuePair<Renderer, Coroutine>>(materialBlinkRoutines))
        {
            if (kv.Value != null) StopCoroutine(kv.Value);
            RestoreOriginalMaterialsForRenderer(kv.Key);
        }
        materialBlinkRoutines.Clear();
        originalMaterialsByRenderer.Clear();
    }

    #region Damage Feedback (material blink, numbers, sonido)

    /// <summary>
    /// Llama este método cuando el enemigo recibe daño. 
    /// Incluye:
    /// - Números de daño
    /// - Material swap blink (BlinkMaterial) usando los parámetros serializados (si blinkFlashMaterial asignado)
    /// - Sonido
    /// </summary>
    public void PlayDamageFeedback(Vector3 damagePosition, float damage, bool isCritical)
    {
        // Números de daño
        ShowDamageNumber(damagePosition, damage, isCritical);

        // Material swap (parpadeo por swap) — solo si se asignó material de flash
        if (blinkFlashMaterial != null)
        {
            Renderer target = blinkTargetRenderer;
            if (target == null && renderers != null && renderers.Length > 0) target = renderers[0];
            if (target != null)
            {
                BlinkMaterial(target, blinkFlashMaterial, blinkInterval, blinkCount);
            }
        }

        // Sonido
        if (audioSource != null)
        {
            AudioClip clip = isCritical ? criticalHitSFX : normalHitSFX;
            if (clip != null) audioSource.PlayOneShot(clip);
        }
    }

    #endregion

    #region Material Blink (swap materials)

    /// <summary>
    /// Parpadea (swap de materiales) en el renderer objetivo usando 'flashMaterial'.
    /// Si ya existe un parpadeo en ese renderer, lo reinicia y restaura los materiales originales antes de empezar de nuevo.
    /// </summary>
    /// <param name="targetRenderer">Renderer a parpadear (no null)</param>
    /// <param name="flashMaterial">Material temporal de parpadeo</param>
    /// <param name="blinkInterval">intervalo entre toggles</param>
    /// <param name="blinkCount">número total de toggles</param>
    public void BlinkMaterial(Renderer targetRenderer, Material flashMaterial, float blinkInterval, int blinkCount)
    {
        if (targetRenderer == null || flashMaterial == null) return;

        // si hay rutina ya corriendo para ese renderer, detener y restaurar
        if (materialBlinkRoutines.TryGetValue(targetRenderer, out Coroutine existing))
        {
            if (existing != null) StopCoroutine(existing);
            RestoreOriginalMaterialsForRenderer(targetRenderer);
            materialBlinkRoutines.Remove(targetRenderer);
        }

        Coroutine routine = StartCoroutine(MaterialBlinkCoroutine(targetRenderer, flashMaterial, blinkInterval, Mathf.Max(1, blinkCount)));
        materialBlinkRoutines[targetRenderer] = routine;
    }

    private IEnumerator MaterialBlinkCoroutine(Renderer targetRenderer, Material flashMaterial, float blinkInterval, int toggles)
    {
        if (targetRenderer == null || flashMaterial == null)
        {
            if (materialBlinkRoutines.ContainsKey(targetRenderer)) materialBlinkRoutines.Remove(targetRenderer);
            yield break;
        }

        // guardar originales (por instancia)
        Material[] originalMaterials = targetRenderer.materials;
        originalMaterialsByRenderer[targetRenderer] = originalMaterials;

        bool showingFlash = false;

        try
        {
            for (int i = 0; i < toggles; i++)
            {
                if (!showingFlash)
                {
                    // crear array con el material de flash (mismo tamaño que originales)
                    Material[] flashArray = new Material[originalMaterials.Length];
                    for (int j = 0; j < flashArray.Length; j++) flashArray[j] = flashMaterial;
                    try { targetRenderer.materials = flashArray; } catch { }
                    showingFlash = true;
                }
                else
                {
                    // restaurar originales
                    try { targetRenderer.materials = originalMaterials; } catch { }
                    showingFlash = false;
                }

                yield return new WaitForSeconds(blinkInterval);
            }
        }
        finally
        {
            // restaurar por seguridad
            RestoreOriginalMaterialsForRenderer(targetRenderer);

            // limpiar registros
            if (materialBlinkRoutines.ContainsKey(targetRenderer)) materialBlinkRoutines.Remove(targetRenderer);
            if (originalMaterialsByRenderer.ContainsKey(targetRenderer)) originalMaterialsByRenderer.Remove(targetRenderer);
        }
    }

    private void RestoreOriginalMaterialsForRenderer(Renderer r)
    {
        if (r == null) return;
        if (originalMaterialsByRenderer.TryGetValue(r, out Material[] originals))
        {
            if (originals != null && originals.Length > 0)
            {
                try
                {
                    r.materials = originals;
                }
                catch { /* ignorar */ }
            }
        }
    }

    #endregion

    #region Armor Glow

    public void StartArmorGlow()
    {
        if (renderers == null) return;

        foreach (Renderer renderer in renderers)
        {
            if (renderer != null && glowMaterial != null)
            {
                renderer.material = glowMaterial;
                StartCoroutine(AnimateGlow(renderer));
            }
        }
    }

    public void StopArmorGlow()
    {
        if (renderers == null) return;

        foreach (Renderer renderer in renderers)
        {
            if (renderer != null && defaultMaterial != null)
            {
                renderer.material = defaultMaterial;
            }
        }
    }

    private IEnumerator AnimateGlow(Renderer renderer)
    {
        Material material = renderer.material;
        float baseIntensity = 1f;

        while (material == glowMaterial)
        {
            float intensity = baseIntensity + Mathf.Sin(Time.time * 3f) * glowIntensity;
            try { material.SetFloat("_EmissionIntensity", intensity); } catch { }
            yield return null;
        }
    }

    #endregion

    #region Damage Numbers

    /// <summary>
    /// Metodo para mostrar números de daño en la posición especificada.
    /// </summary>
    /// <param name="position"></param>
    /// <param name="damage"></param>
    /// <param name="isCritical"></param>
    public void ShowDamageNumber(Vector3 position, float damage, bool isCritical = false)
    {
        if (damageNumberPrefab != null)
        {
            GameObject damageNumber = Instantiate(damageNumberPrefab, position, Quaternion.identity, damageNumberParent);
            DamageNumber damageNumberScript = damageNumber.GetComponent<DamageNumber>();

            if (damageNumberScript != null)
            {
                damageNumberScript.Initialize(damage, isCritical);
            }
        }
    }

    private void ResetVisuals()
    {
        if (renderers != null && defaultMaterial != null)
        {
            foreach (var r in renderers)
            {
                if (r != null) r.material = defaultMaterial;
            }
        }

        if (damageNumberParent != null)
        {
            for (int i = 0; i < damageNumberParent.childCount; i++)
            {
                Destroy(damageNumberParent.GetChild(i).gameObject);
            }
        }
    }

    #endregion
}

