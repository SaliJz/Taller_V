using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[System.Serializable]
public struct RarityColorMapping
{
    public EffectRarity rarity;
    [ColorUsage(true, true)]
    public Color color;
}

[RequireComponent(typeof(Collider))]
public class GachaponTrigger : MonoBehaviour, PlayerControlls.IInteractionsActions
{
    [Header("Dependencies")]
    [SerializeField] private GachaponSystem gachaponSystem;
    [SerializeField] private MeshRenderer cubeRenderer;
    [SerializeField] private Material activatedMaterial;

    [Header("UI Dependencies")]
    [SerializeField] private GameObject resultUIPanel;
    [SerializeField] private TextMeshProUGUI nameTMP;
    [SerializeField] private TextMeshProUGUI effectsTMP;

    [Header("Purchase Cooldown")]
    [SerializeField] private UnityEvent onFinish;

    [Header("SFX")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip OpenSFX;
    [SerializeField] private AudioClip riseSFX;

    [Header("Configuración")]
    private bool playerIsNear = false;
    private bool isActivated = false;
    public bool allowMultiplePulls = false;
    private bool isAnimating = false;

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
    public Transform sinkTargetTransform;
    public Transform riseTargetTransform;
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

    private PlayerControlls playerControls;

    private void Awake()
    {
        if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>();

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

        playerControls = new PlayerControlls();
        playerControls.Interactions.SetCallbacks(this);
    }

    private void OnEnable()
    {
        playerControls?.Interactions.Enable();
    }

    private void OnDisable()
    {
        playerControls?.Interactions.Disable();
        if (resultUIPanel != null) resultUIPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        playerControls?.Dispose();
    }

    private void Start()
    {
        if (sinkTargetTransform == null || riseTargetTransform == null)
        {
            Debug.LogError("Las referencias 'sinkTargetTransform' y 'riseTargetTransform' deben ser asignadas en el Inspector. Desactivando GachaponTrigger.");
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

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (!context.started) return;

        if (playerIsNear && !isActivated && !isAnimating)
        {
            StartCoroutine(AnimateAndPull());
        }
    }

    public void OnAdvanceDialogue(InputAction.CallbackContext context) { }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player") && !isAnimating)
        {
            playerIsNear = true;
            if (!isActivated && HUDManager.Instance != null)
            {
                HUDManager.Instance.SetInteractionPrompt(true, "Interact", "TIRAR");
            }
            else
            {
                HUDManager.Instance.SetInteractionPrompt(false, "Interact", "TIRAR");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerIsNear = false;

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.SetInteractionPrompt(false, "Interact", "TIRAR");
            }
        }
    }

    private IEnumerator AnimateAndPull()
    {
        isAnimating = true;
        isActivated = true;
        ShowResultUI(false, "", "");

        if (audioSource != null && OpenSFX != null)
        {
            audioSource.PlayOneShot(OpenSFX);
        }

        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.SetInteractionPrompt(false, "Interact", "TIRAR");
        }

        Coroutine animationCoroutine = StartCoroutine(AnimateGachaponLight(animationDuration));
        yield return animationCoroutine;

        GachaponResult result = gachaponSystem.PullGachapon();

        onFinish?.Invoke();
        Debug.Log("Evento activando");

        if (rarityColorsMap.ContainsKey(result.rarity))
        {
            if (result.effectPair != null)
            {
                gachaponSystem.ApplyEffect(result);
                // Pasar la rareza obtenida al formatear los modificadores
                ShowResultUI(true, result.effectPair.effectName, FormatModifiers(result.effectPair, result.rarity));
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
            isAnimating = false;
            if (playerIsNear && HUDManager.Instance != null)
            {
                HUDManager.Instance.SetInteractionPrompt(false, "Interact", "TIRAR");
            }
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
        isAnimating = true;
        GetComponent<Collider>().enabled = false;
        playerIsNear = false;

        Vector3 startPos = transform.position;
        Vector3 endPos = sinkTargetTransform.position;
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
        isAnimating = false;
    }

    public void StartAnimationRise()
    {
        Debug.Log("Inicia");
        StartCoroutine(RiseGachapon());
    }

    private IEnumerator RiseGachapon()
    {
        isAnimating = true;
        GetComponent<Collider>().enabled = false;

        if (audioSource != null && riseSFX != null)
        {
            audioSource.clip = riseSFX;
            audioSource.Play();
        }

        Vector3 startPos = transform.position;
        Vector3 endPos = riseTargetTransform.position;
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
        GetComponent<Collider>().enabled = true;
        isActivated = false;
        isAnimating = false;

        if (audioSource != null && audioSource.isPlaying && audioSource.clip == riseSFX)
        {
            audioSource.Stop();
        }

        if (playerIsNear && HUDManager.Instance != null)
        {
            HUDManager.Instance.SetInteractionPrompt(true, "Interact", "TIRAR");
        }
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

    private string FormatModifiers(GachaponEffectData effectData, EffectRarity obtainedRarity)
    {
        string description = $"<color=yellow>Rareza: {obtainedRarity}</color>\n\n";

        if (effectData.HasAdvantage)
        {
            description += "<b><color=green>VENTAJA:</color></b>\n";
            var advantages = effectData.GetAdvantageModifiersForRarity(obtainedRarity);
            foreach (var (statType, value, duration, isPercentage, durationType) in advantages)
            {
                string statName = TranslateStatType(statType);
                string durationText = TranslateDurationType(durationType, duration);
                description += $"- {statName}: {value}{(isPercentage ? "%" : "")} ({durationText})\n";
            }
        }

        if (effectData.HasDisadvantage)
        {
            description += "\n<b><color=red>DESVENTAJA:</color></b>\n";
            var disadvantages = effectData.GetDisadvantageModifiersForRarity(obtainedRarity);
            foreach (var (statType, value, duration, isPercentage, durationType) in disadvantages)
            {
                string statName = TranslateStatType(statType);
                string durationText = TranslateDurationType(durationType, duration);
                description += $"- {statName}: {value}{(isPercentage ? "%" : "")} ({durationText})\n";
            }
        }

        return description;
    }

    private string TranslateStatType(StatType statType)
    {
        switch (statType)
        {
            case StatType.MaxHealth: return "Vida Máxima";
            case StatType.MoveSpeed: return "Velocidad de Movimiento";
            case StatType.Gravity: return "Gravedad";
            case StatType.MeleeAttackDamage: return "Daño Ataque Cuerpo a Cuerpo";
            case StatType.MeleeAttackSpeed: return "Velocidad Ataque Cuerpo a Cuerpo";
            case StatType.MeleeRadius: return "Radio de Ataque Cuerpo a Cuerpo";
            case StatType.ShieldAttackDamage: return "Daño Ataque Escudo";
            case StatType.ShieldSpeed: return "Velocidad del Escudo";
            case StatType.ShieldMaxDistance: return "Distancia Máxima del Escudo";
            case StatType.ShieldMaxRebounds: return "Rebotes Máximos del Escudo";
            case StatType.ShieldReboundRadius: return "Radio de Rebote del Escudo";
            case StatType.AttackDamage: return "Daño de Ataque Base";
            case StatType.AttackSpeed: return "Velocidad de Ataque Base";
            case StatType.ShieldBlockUpgrade: return "Mejora de Bloqueo del Escudo";
            case StatType.DamageTaken: return "Daño Recibido";
            case StatType.HealthDrainAmount: return "Drenaje de Vida";
            case StatType.LuckStack: return "Pilas de Suerte";
            case StatType.EssenceCostReduction: return "Reducción de Coste de Esencia";
            case StatType.ShopPriceReduction: return "Reducción de Precio en Tienda";
            case StatType.HealthPerRoomRegen: return "Regeneración de Vida por Sala";
            case StatType.CriticalChance: return "Probabilidad de Crítico";
            case StatType.LifestealOnKill: return "Robo de Vida al Matar";
            case StatType.CriticalDamageMultiplier: return "Multiplicador de Daño Crítico";
            case StatType.DashRangeMultiplier: return "Multiplicador de Alcance de Dash";
            default: return statType.ToString();
        }
    }

    private string TranslateDurationType(EffectDurationType durationType, float durationValue)
    {
        switch (durationType)
        {
            case EffectDurationType.Permanent:
                return "Permanente";
            case EffectDurationType.Rounds:
                return $"por {Mathf.CeilToInt(durationValue)} Sala(s)";
            case EffectDurationType.Time:
                return $"por {durationValue:F1} Segundos";
            default:
                return durationType.ToString();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
        {
            initialPosition = transform.position;
        }

        Vector3 upPosition = initialPosition;

        if (sinkTargetTransform != null)
        {
            Vector3 downPosition = sinkTargetTransform.position;

            Gizmos.color = Color.red;
            Gizmos.DrawLine(upPosition, downPosition);

            Gizmos.color = Color.green;
            Gizmos.DrawSphere(upPosition, 0.05f);

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(downPosition, 0.05f);
        }

        if (riseTargetTransform != null)
        {
            Vector3 riseTargetPosition = riseTargetTransform.position;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(upPosition, riseTargetPosition);

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(riseTargetPosition, 0.05f);
        }
    }
}