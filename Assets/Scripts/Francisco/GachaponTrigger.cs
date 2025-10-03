using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

[System.Serializable]
public struct RarityColorMapping
{
    public EffectRarity rarity;
    [ColorUsage(true, true)]
    public Color color;
}

[RequireComponent(typeof(Collider))]
public class GachaponTrigger : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private GachaponSystem gachaponSystem;
    [SerializeField] private MeshRenderer cubeRenderer;
    [SerializeField] private Material activatedMaterial;

    [Header("UI Dependencies")]
    [SerializeField] private GameObject resultUIPanel;
    [SerializeField] private TextMeshProUGUI nameTMP;
    [SerializeField] private TextMeshProUGUI effectsTMP;

    [Header("Configuración")]
    public KeyCode activationKey = KeyCode.E;
    private bool playerIsNear = false;
    private bool isActivated = false;
    public bool allowMultiplePulls = false;

    [Header("Animation Configuration")]
    public float animationDuration = 1.5f;
    [Range(0.05f, 1f)]
    public float cycleSpeed = 0.15f;
    public AnimationCurve colorCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public float intensityLerpDuration = 0.5f;
    [Range(1.0f, 10.0f)]
    public float finalIntensityMultiplier = 3.0f;
    public float pulseDuration = 3.0f;

    [Header("Sink Animation")]
    public float sinkDuration = 1.0f;
    public float sinkDistance = -0.5f;
    public AnimationCurve sinkCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Rarity Colors")]
    public List<RarityColorMapping> rarityColorMappings = new List<RarityColorMapping>
    {
        new RarityColorMapping { rarity = EffectRarity.Comun, color = new Color(0.5f, 0.5f, 0.5f, 1f) },
        new RarityColorMapping { rarity = EffectRarity.Raro, color = Color.blue },
        new RarityColorMapping { rarity = EffectRarity.Epico, color = new Color(1f, 0f, 1f, 1f) },
        new RarityColorMapping { rarity = EffectRarity.Legendario, color = Color.yellow }
    };
    private Dictionary<object, Color> rarityColorsMap = new Dictionary<object, Color>();

    private Material originalMaterialInstance;
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private Vector3 initialPosition;

    private void Start()
    {
        if (gachaponSystem == null)
            gachaponSystem = FindAnyObjectByType<GachaponSystem>();

        if (cubeRenderer == null)
            cubeRenderer = GetComponent<MeshRenderer>();

        if (gachaponSystem == null || cubeRenderer == null)
        {
            Debug.LogError("GachaponSystem o MeshRenderer no encontrado. Desactivando GachaponTrigger.");
            enabled = false;
            return;
        }

        originalMaterialInstance = cubeRenderer.material;
        initialPosition = transform.position;

        foreach (var mapping in rarityColorMappings)
        {
            rarityColorsMap[mapping.rarity] = mapping.color;
        }

        if (resultUIPanel != null)
        {
            resultUIPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (playerIsNear && !isActivated && Input.GetKeyDown(activationKey))
        {
            StartCoroutine(AnimateAndPull());
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerIsNear = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerIsNear = false;
        }
    }

    private IEnumerator AnimateAndPull()
    {
        isActivated = true;
        ShowResultUI(false, "", "");

        Coroutine animationCoroutine = StartCoroutine(AnimateGachaponLight(animationDuration));
        yield return animationCoroutine;

        GachaponResult result = gachaponSystem.PullGachapon();

        if (rarityColorsMap.ContainsKey(result.rarity))
        {
            if (result.effectPair != null)
            {
                gachaponSystem.ApplyEffect(result);
                ShowResultUI(true, result.effectPair.effectName, FormatModifiers(result.effectPair));
            }
            else
            {
                ShowResultUI(true, "¡Error en la tirada!", "No se pudo obtener un efecto válido.");
            }

            yield return StartCoroutine(SetFinalRarityColor(result.rarity));

            if (rarityColorsMap.TryGetValue(result.rarity, out Color baseColor))
            {
                yield return StartCoroutine(PulseFinalRarityColorWithDuration(baseColor, pulseDuration));
            }
        }
        else
        {
            cubeRenderer.material.SetColor(EmissionColor, Color.black);
            Debug.LogWarning("La gachapon no devolvió una rareza válida. Sistema bloqueado y apagado.");
        }

        ShowResultUI(false, "", "");

        if (allowMultiplePulls)
        {
            cubeRenderer.material = originalMaterialInstance;
            isActivated = false;
        }
        else
        {
            yield return StartCoroutine(SinkGachapon());
        }
    }

    private IEnumerator AnimateGachaponLight(float duration)
    {
        float startTime = Time.time;
        Material animMaterial = cubeRenderer.material;

        if (activatedMaterial != null && animMaterial != activatedMaterial)
        {
            cubeRenderer.material = activatedMaterial;
            animMaterial = cubeRenderer.material;
        }

        List<Color> cycleColors = rarityColorMappings.Select(m => m.color).ToList();

        if (!animMaterial.HasProperty(EmissionColor) || cycleColors.Count < 2)
        {
            yield break;
        }

        int colorIndex = 0;
        float segmentTime = 0f;

        while (Time.time < startTime + duration)
        {
            Color startColor = cycleColors[colorIndex];
            Color endColor = cycleColors[(colorIndex + 1) % cycleColors.Count];

            segmentTime += Time.deltaTime;
            float t = segmentTime / cycleSpeed;
            t = colorCurve.Evaluate(Mathf.Clamp01(t));

            Color currentColor = Color.Lerp(startColor, endColor, t);
            animMaterial.SetColor(EmissionColor, currentColor);

            if (t >= 1.0f)
            {
                colorIndex = (colorIndex + 1) % cycleColors.Count;
                segmentTime = 0f;
            }

            yield return null;
        }
    }

    private IEnumerator SetFinalRarityColor(EffectRarity rarity)
    {
        if (rarityColorsMap.TryGetValue(rarity, out Color baseColor))
        {
            if (activatedMaterial != null)
            {
                if (cubeRenderer.material != activatedMaterial)
                {
                    cubeRenderer.material = activatedMaterial;
                }
            }

            Material finalMaterial = cubeRenderer.material;

            Color startColor = finalMaterial.GetColor(EmissionColor);
            Color targetColor = baseColor * finalIntensityMultiplier;

            float elapsedTime = 0f;

            while (elapsedTime < intensityLerpDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / intensityLerpDuration;
                t = Mathf.SmoothStep(0f, 1f, t);

                Color currentColor = Color.Lerp(startColor, targetColor, t);
                finalMaterial.SetColor(EmissionColor, currentColor);

                yield return null;
            }

            finalMaterial.SetColor(EmissionColor, targetColor);
        }
    }

    private IEnumerator PulseFinalRarityColorWithDuration(Color baseColor, float duration)
    {
        Material finalMaterial = cubeRenderer.material;
        Color highIntensityColor = baseColor * finalIntensityMultiplier;
        Color lowIntensityColor = baseColor;
        float startTime = Time.time;

        while (Time.time < startTime + duration)
        {
            float t = Mathf.PingPong(Time.time / intensityLerpDuration, 1f);
            t = colorCurve.Evaluate(t);

            Color currentColor = Color.Lerp(lowIntensityColor, highIntensityColor, t);
            finalMaterial.SetColor(EmissionColor, currentColor);

            yield return null;
        }

        finalMaterial.SetColor(EmissionColor, highIntensityColor);
    }

    private IEnumerator SinkGachapon()
    {
        Vector3 startPos = transform.position;
        Vector3 endPos = initialPosition + Vector3.up * sinkDistance;
        float elapsedTime = 0f;

        while (elapsedTime < sinkDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / sinkDuration;
            t = sinkCurve.Evaluate(t);

            transform.position = Vector3.Lerp(startPos, endPos, t);

            yield return null;
        }

        transform.position = endPos;
        GetComponent<Collider>().enabled = false;
    }

    private void ShowResultUI(bool show, string name, string effects)
    {
        if (resultUIPanel != null)
        {
            resultUIPanel.SetActive(show);
            if (show)
            {
                if (nameTMP != null)
                {
                    nameTMP.text = name;
                }
                if (effectsTMP != null)
                {
                    effectsTMP.text = effects;
                }
            }
        }
    }

    private string FormatModifiers(GachaponEffectData effectData)
    {
        string description = $"<color=yellow>Rareza: {effectData.rarity}</color>\n\n";

        if (effectData.HasAdvantage)
        {
            description += "<b><color=green>VENTAJA:</color></b>\n";
            foreach (var mod in effectData.advantageModifiers)
            {
                description += $"- {mod.statType}: {mod.modifierValue}{(mod.isPercentage ? "%" : "")} ({mod.durationType})\n";
            }
        }

        if (effectData.HasDisadvantage)
        {
            description += "\n<b><color=red>DESVENTAJA:</color></b>\n";
            foreach (var mod in effectData.disadvantageModifiers)
            {
                description += $"- {mod.statType}: {mod.modifierValue}{(mod.isPercentage ? "%" : "")} ({mod.durationType})\n";
            }
        }

        return description;
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
        {
            initialPosition = transform.position;
        }

        Vector3 start = initialPosition;
        Vector3 end = initialPosition + Vector3.up * sinkDistance;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(start, end);

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(start, 0.05f);

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(end, 0.05f);
    }
}