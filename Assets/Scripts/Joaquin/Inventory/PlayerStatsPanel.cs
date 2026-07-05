using UnityEngine;
using TMPro;
using System.Text;

public class PlayerStatsPanel : MonoBehaviour
{
    [Header("Referencias UI")]
    [Tooltip("El contenedor principal del panel (RectTransform).")]
    [SerializeField] private RectTransform panelRect;
    [Tooltip("El componente de texto donde se imprimirán las estadísticas.")]
    [SerializeField] private TextMeshProUGUI statsTextDisplay;

    [Header("Posicionamiento")]
    [Tooltip("Desplazamiento en el mundo 3D respecto a la posición del jugador.")]
    [SerializeField] private Vector3 worldOffset = new Vector3(2f, 0f, 0f);

    private PlayerStatsManager statsManager;
    private Canvas parentCanvas;
    private bool wasInventoryOpen = false;

    private void Start()
    {
        parentCanvas = GetComponentInParent<Canvas>();

        if (panelRect != null)
        {
            panelRect.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        PlayerStatsManager.OnStatChanged += OnStatChangedHandler;
    }

    private void OnDisable()
    {
        PlayerStatsManager.OnStatChanged -= OnStatChangedHandler;
    }

    private void OnStatChangedHandler(StatType type, float newValue)
    {
        if (panelRect != null && panelRect.gameObject.activeSelf)
        {
            UpdateStatsPanel();
        }
    }

    private void Update()
    {
        bool isInventoryOpen = InventoryUIManager.Instance != null &&
                               InventoryUIManager.Instance.IsOpen &&
                               !InventoryUIManager.Instance.IsConfirmPanelOpen;

        if (isInventoryOpen != wasInventoryOpen)
        {
            wasInventoryOpen = isInventoryOpen;

            if (isInventoryOpen)
            {
                if (statsManager == null) statsManager = FindAnyObjectByType<PlayerStatsManager>();
                UpdateStatsPanel();
                if (panelRect != null) panelRect.gameObject.SetActive(true);
            }
            else
            {
                if (panelRect != null) panelRect.gameObject.SetActive(false);
            }
        }

        if (isInventoryOpen && panelRect != null && statsManager != null)
        {
            FollowPlayerPosition();
        }
    }

    /// <summary>
    /// Convierte la posición 3D del jugador a coordenadas 2D del Canvas para posicionar el panel.
    /// </summary>
    private void FollowPlayerPosition()
    {
        if (Camera.main == null || parentCanvas == null) return;

        Vector3 targetWorldPosition = statsManager.transform.position + worldOffset;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(Camera.main, targetWorldPosition);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentCanvas.transform as RectTransform,
            screenPoint,
            parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main,
            out Vector2 localPoint);

        panelRect.localPosition = localPoint;
    }

    public void UpdateStatsPanel()
    {
        if (statsManager == null || statsTextDisplay == null) return;
        StringBuilder sb = new StringBuilder();

        sb.AppendLine(" <b><color=Yellow>Estadística <pos=50%>Base <pos=75%>Actual</color></b>");
        // sb.AppendLine("-------------------------------------------------");

        // Orden y contenido alineado con el Panel Estadístico de la especificación de UI

        // Resistencia (daño recibido, en %): mas alto = peor
        sb.AppendLine(FormatStat("Resistencia", StatType.DamageTaken, inverseColors: true));

        sb.AppendLine(FormatStat("Daño a melé", StatType.MeleeAttackDamage));
        sb.AppendLine(FormatStat("Daño a distancia", StatType.ShieldAttackDamage));
        sb.AppendLine(FormatStat("Vel. Melé", StatType.MeleeAttackSpeed));
        sb.AppendLine(FormatStat("Vel. distancia", StatType.ShieldSpeed));
        sb.AppendLine(FormatStat("Vel. Movimiento", StatType.MoveSpeed));

        // Alcance e impulso (dash): version multiplicador + version aditiva (fija)
        //sb.AppendLine(FormatStat("Alc. impulso", StatType.DashRangeMultiplier));
        sb.AppendLine(FormatStat("Alc. dash", StatType.DashRangeFlatBonus));

        // Cooldown del dash: mas alto = peor (tarda mas en recargar)
        sb.AppendLine(FormatStat("Cooldown dash", StatType.DashCooldownPost, inverseColors: true));

        sb.AppendLine(FormatStat("Desplaz. por golpe", StatType.MeleeComboDisplacement));

        sb.AppendLine(FormatStat("Alc. Distancia", StatType.ShieldMaxDistance));
        sb.AppendLine(FormatStat("Alc. rebote escudo", StatType.ShieldReboundRadius));

        float realBaseRebounds = statsManager.GetBaseStat(StatType.ShieldMaxRebounds);
        float currentRebounds = statsManager.GetCurrentStat(StatType.ShieldMaxRebounds);
        sb.AppendLine(FormatCustomLine("Rebotes del escudo", realBaseRebounds, currentRebounds));

        sb.AppendLine(FormatStat("Empuje a distancia", StatType.ShieldPushForce));

        // Empuje recibido: Mas alto = peor (se recibe mas empuje).
        sb.AppendLine(FormatStat("Empuje recibido", StatType.KnockbackReceived, inverseColors: true));

        // Consumo de energía: mas alto = peor
        // (Se consume mas lo que se traduce en menos duracion de la habilidad sin recibir castigo por uso).
        sb.AppendLine(FormatStat("Consumo de energía", StatType.StaminaConsumption, inverseColors: true));

        sb.AppendLine(FormatStat("Vida al matar", StatType.LifestealOnKill));

        // Estadísticas adicionales que no forman parte del listado del panel visual pero
        // siguen siendo relevantes para el jugador
        //sb.AppendLine(FormatStat("Coste Bers.", StatType.HealthDrainAmount, inverseColors: true));

        statsTextDisplay.text = sb.ToString();
    }

    /// <summary>
    /// Extrae automáticamente del Gestor y formatea.
    /// </summary>
    private string FormatStat(string statName, StatType type, bool inverseColors = false)
    {
        float baseValue = statsManager.GetBaseStat(type);
        float currentValue = statsManager.GetCurrentStat(type);
        return FormatCustomLine(statName, baseValue, currentValue, false, inverseColors);
    }

    /// <summary>
    /// Función base de formateo estructurada en 3 columnas.
    /// </summary>
    private string FormatCustomLine(string statName, float baseValue, float currentValue, bool isPercentage = false, bool inverseColors = false)
    {
        string symbol = isPercentage ? "%" : "";
        string changedString = $"{currentValue:0.##}{symbol}";
        float difference = currentValue - baseValue;

        if (Mathf.Abs(difference) > 0.01f)
        {
            if (difference > 0)
            {
                string color = inverseColors ? "red" : "green";
                changedString = $"<color={color}>{currentValue:0.##}{symbol}</color>";
            }
            else
            {
                string color = inverseColors ? "green" : "red";
                changedString = $"<color={color}>{currentValue:0.##}{symbol}</color>";
            }
        }

        return $" {statName} <pos=50%>{baseValue:0.##}{symbol} <pos=75%>{changedString}";
    }
}