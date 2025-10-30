using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Gestor principal del UI del inventario
/// </summary>
public class InventoryUIManager : MonoBehaviour
{
    [Header("Animador de Inventario")]
    [SerializeField] private InventoryAnimator inventoryAnimator;

    [Header("Referencias de Paneles")]
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private GameObject leftPanel; // Panel de estad�sticas
    [SerializeField] private GameObject rightPanel; // Panel de �tems

    [Header("Panel Izquierdo - Estad�sticas")]
    [SerializeField] private TextMeshProUGUI statsTitle;
    [SerializeField] private TextMeshProUGUI statsContent;
    [SerializeField] private Button closeDetailButton;

    [Header("Panel Derecho - Categor�as")]
    [SerializeField] private ScrollRect categoryScrollRect;
    [SerializeField] private Transform categoryContainer;
    [SerializeField] private GameObject categoryPrefab;

    [Header("Prefabs")]
    [SerializeField] private GameObject itemSlotPrefab;

    [Header("Configuraci�n")]
    [SerializeField] private int slotsPerRow = 4;
    [SerializeField] private Color highlightColor = new Color(0.8f, 0.1f, 0.1f); // Carmes�
    [SerializeField] private Color positiveStatColor = Color.green;
    [SerializeField] private Color negativeStatColor = Color.red;

    [Header("Fuentes")]
    [SerializeField] private TMP_FontAsset demonicFont;

    private PlayerControlls playerControls;
    private PlayerStatsManager statsManager;
    private PlayerHealth playerHealth;
    private InventoryManager inventoryManager;
    private PauseController pauseController;

    private bool isInventoryOpen = false;
    private ShopItem selectedItem = null;

    public bool IsInventoryOpen => isInventoryOpen;

    private Dictionary<ItemCategory, CategorySection> categorySections = new Dictionary<ItemCategory, CategorySection>();

    private class CategorySection
    {
        public GameObject sectionObject;
        public TextMeshProUGUI titleText;
        public Transform slotsContainer;
        public List<InventorySlot> slots = new List<InventorySlot>();
    }

    private void Awake()
    {
        playerControls = new PlayerControlls();
        statsManager = FindAnyObjectByType<PlayerStatsManager>();
        playerHealth = FindAnyObjectByType<PlayerHealth>();
        inventoryManager = FindAnyObjectByType<InventoryManager>();
        pauseController = FindAnyObjectByType<PauseController>();

        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
        }
    }

    private void OnEnable()
    {
        playerControls?.Enable();
    }

    private void OnDisable()
    {
        playerControls?.Disable();
    }

    private void Start()
    {
        InitializeCategorySections();

        if (closeDetailButton != null)
        {
            closeDetailButton.onClick.AddListener(ShowPlayerStats);
            closeDetailButton.gameObject.SetActive(false);
        }

        if (statsTitle != null && demonicFont != null)
        {
            statsTitle.font = demonicFont;
        }
    }

    private void Update()
    {
        // Toggle inventario con I o Tab
        if (Keyboard.current != null)
        {
            if (Keyboard.current.iKey.wasPressedThisFrame || Keyboard.current.tabKey.wasPressedThisFrame)
            {
                ToggleInventory();
            }
        }

        // Soporte para gamepad (bot�n Select/View)
        if (Gamepad.current != null)
        {
            if (Gamepad.current.selectButton.wasPressedThisFrame)
            {
                ToggleInventory();
            }
        }

        // ESC para cerrar
        if (isInventoryOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseInventory();
        }
    }

    /// <summary>
    /// Inicializa las secciones de categor�as
    /// </summary>
    private void InitializeCategorySections()
    {
        if (categoryContainer == null || categoryPrefab == null) return;

        foreach (ItemCategory category in System.Enum.GetValues(typeof(ItemCategory)))
        {
            GameObject sectionObj = Instantiate(categoryPrefab, categoryContainer);
            CategorySection section = new CategorySection
            {
                sectionObject = sectionObj,
                titleText = sectionObj.GetComponentInChildren<TextMeshProUGUI>(),
                slotsContainer = sectionObj.transform.Find("SlotsContainer")
            };

            if (section.titleText != null)
            {
                section.titleText.text = GetCategoryName(category);
                if (demonicFont != null)
                {
                    section.titleText.font = demonicFont;
                }
            }

            categorySections[category] = section;

            // Crear slots iniciales (4 por categor�a)
            CreateSlotsForCategory(category, slotsPerRow);
        }
    }

    /// <summary>
    /// Crea slots para una categor�a espec�fica
    /// </summary>
    private void CreateSlotsForCategory(ItemCategory category, int count)
    {
        if (!categorySections.ContainsKey(category)) return;

        CategorySection section = categorySections[category];

        for (int i = 0; i < count; i++)
        {
            GameObject slotObj = Instantiate(itemSlotPrefab, section.slotsContainer);
            InventorySlot slot = slotObj.GetComponent<InventorySlot>();

            if (slot != null)
            {
                slot.Initialize(this);
                section.slots.Add(slot);
            }
        }
    }

    /// <summary>
    /// Toggle del inventario
    /// </summary>
    public void ToggleInventory()
    {
        if (isInventoryOpen)
        {
            CloseInventory();
        }
        else
        {
            OpenInventory();
        }
    }

    /// <summary>
    /// Abre el inventario
    /// </summary>
    public void OpenInventory()
    {
        if (inventoryPanel == null) return;

        isInventoryOpen = true;

        // Deshabilitar el controlador de pausa si existe
        if (pauseController != null)
        {
            pauseController.enabled = false;
        }

        RefreshInventory();
        ShowPlayerStats();

        // Pausar el juego
        Time.timeScale = 0f;

        // Usar animaci�n si est� disponible
        if (inventoryAnimator != null)
        {
            inventoryAnimator.AnimateOpen();
        }
        else
        {
            inventoryPanel.SetActive(true);
        }
    }

    /// <summary>
    /// Cierra el inventario
    /// </summary>
    public void CloseInventory()
    {
        if (inventoryPanel == null) return;

        isInventoryOpen = false;
        selectedItem = null;

        if (pauseController != null)
        {
            pauseController.enabled = true;
        }

        // Reanudar el juego
        Time.timeScale = 1f;

        // Usar animaci�n si est� disponible
        if (inventoryAnimator != null)
        {
            inventoryAnimator.AnimateClose();
        }
        else
        {
            inventoryPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Refresca todo el inventario
    /// </summary>
    public void RefreshInventory()
    {
        if (inventoryManager == null) return;

        // Limpiar todos los slots
        foreach (var section in categorySections.Values)
        {
            foreach (var slot in section.slots)
            {
                slot.ClearSlot();
            }
        }

        // Agrupar �tems por categor�a
        var itemsByCategory = InventoryManager.CurrentRunItems
            .GroupBy(item => item.category)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Llenar los slots
        foreach (var kvp in itemsByCategory)
        {
            ItemCategory category = kvp.Key;
            List<ShopItem> items = kvp.Value;

            if (!categorySections.ContainsKey(category)) continue;

            CategorySection section = categorySections[category];

            // Si hay m�s �tems que slots, crear m�s slots
            while (items.Count > section.slots.Count)
            {
                CreateSlotsForCategory(category, slotsPerRow);
            }

            // Asignar �tems a slots
            for (int i = 0; i < items.Count; i++)
            {
                section.slots[i].SetItem(items[i]);
            }
        }
    }

    /// <summary>
    /// Muestra las estad�sticas del jugador en el panel izquierdo
    /// </summary>
    public void ShowPlayerStats()
    {
        selectedItem = null;

        if (statsTitle != null)
        {
            statsTitle.text = "Estad�sticas del Jugador";
        }

        if (closeDetailButton != null)
        {
            closeDetailButton.gameObject.SetActive(false);
        }

        if (statsContent != null && statsManager != null)
        {
            statsContent.text = GeneratePlayerStatsText();
        }
    }

    /// <summary>
    /// Muestra los detalles de un �tem en el panel izquierdo
    /// </summary>
    public void ShowItemDetails(ShopItem item)
    {
        selectedItem = item;

        if (statsTitle != null)
        {
            statsTitle.text = "Descripci�n del �tem";
        }

        if (closeDetailButton != null)
        {
            closeDetailButton.gameObject.SetActive(true);
        }

        if (statsContent != null)
        {
            statsContent.text = GenerateItemDetailsText(item);
        }
    }

    /// <summary>
    /// Genera el texto de estad�sticas del jugador
    /// </summary>
    private string GeneratePlayerStatsText()
    {
        if (statsManager == null || playerHealth == null) return "No hay datos disponibles";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        // Vida
        float currentHealth = playerHealth.CurrentHealth;
        float currentMaxHealth = statsManager.GetStat(StatType.MaxHealth);
        float baseMaxHealth = statsManager.GetBaseStat(StatType.MaxHealth);

        sb.AppendLine($"<b>Vida:</b> {currentHealth:F0} / {FormatStatValue(currentMaxHealth, baseMaxHealth)}");

        // Otras estad�sticas principales
        StatType[] mainStats = new StatType[]
        {
            StatType.MoveSpeed,
            StatType.AttackDamage,
            StatType.AttackSpeed,
            StatType.MeleeAttackDamage,
            StatType.MeleeAttackSpeed,
            StatType.ShieldAttackDamage,
            StatType.ShieldSpeed,
            StatType.CriticalChance,
            StatType.LifestealOnKill
        };

        foreach (var statType in mainStats)
        {
            float current = statsManager.GetStat(statType);
            float baseVal = statsManager.GetBaseStat(statType);
            string statName = GetStatDisplayName(statType);

            // Formateador decimal F2
            sb.AppendLine($"<b>{statName}:</b> {FormatStatValueDecimal(current, baseVal)}");
        }

        if (inventoryManager != null)
        {
            var activeBehavioralEffects = inventoryManager.ActiveBehavioralEffects;

            if (activeBehavioralEffects != null && activeBehavioralEffects.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("<b><color=#FFD700>--- Efectos Activos ---</color></b>");

                // Agrupar por categor�a
                var effectsByCategory = activeBehavioralEffects
                    .Where(e => e != null)
                    .GroupBy(e => e.category)
                    .OrderBy(g => g.Key);

                foreach (var categoryGroup in effectsByCategory)
                {
                    sb.AppendLine();
                    sb.AppendLine($"<b><color=#FFA500>{GetEffectCategoryName(categoryGroup.Key)}</color></b>");

                    foreach (var effect in categoryGroup)
                    {
                        string shortSummary = effect.GetShortSummary();
                        sb.AppendLine($"<color=#FFE4B5>  � {shortSummary}</color>");
                    }
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Genera el texto de detalles de un �tem
    /// </summary>
    private string GenerateItemDetailsText(ShopItem item)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        // Nombre
        sb.AppendLine($"<size=120%><b>{item.itemName}</b></size>");
        sb.AppendLine();

        // Rareza
        string rarityColor = ColorUtility.ToHtmlStringRGB(item.GetRarityColor());
        sb.AppendLine($"<color=#{rarityColor}><b>Rareza:</b> {item.rarity}</color>");
        sb.AppendLine();

        // Descripci�n
        sb.AppendLine($"<i>{item.description}</i>");
        sb.AppendLine();

        // Beneficios
        if (item.benefits != null && item.benefits.Count > 0)
        {
            sb.AppendLine("<b>Beneficios:</b>");
            foreach (var benefit in item.benefits)
            {
                string effectText = FormatEffect(benefit, true);
                sb.AppendLine($"<color=#{ColorUtility.ToHtmlStringRGB(positiveStatColor)}>{effectText}</color>");
            }
            sb.AppendLine();
        }

        // Desventajas
        if (item.drawbacks != null && item.drawbacks.Count > 0)
        {
            sb.AppendLine("<b>Desventajas:</b>");
            foreach (var drawback in item.drawbacks)
            {
                string effectText = FormatEffect(drawback, false);
                sb.AppendLine($"<color=#{ColorUtility.ToHtmlStringRGB(negativeStatColor)}>{effectText}</color>");
            }
            sb.AppendLine();
        }

        // Efectos especiales de amuletos
        if (item.behavioralEffects != null && item.behavioralEffects.Count > 0)
        {
            sb.AppendLine("<b><color=#FFD700>Efectos Especiales:</color></b>");
            foreach (var behavioralEffect in item.behavioralEffects)
            {
                if (behavioralEffect != null)
                {
                    string formattedDescription = behavioralEffect.GetFormattedDescription();
                    sb.AppendLine($"<color=#FFA500>� {formattedDescription}</color>");
                }
            }
            sb.AppendLine();
        }

        // Informaci�n temporal
        if (item.isTemporary)
        {
            sb.AppendLine("<b><color=orange>? TEMPORAL</color></b>");
            if (item.temporaryDuration > 0)
            {
                sb.AppendLine($"Duraci�n: {item.temporaryDuration:F0} segundos");
            }
            if (item.temporaryRooms > 0)
            {
                sb.AppendLine($"Duraci�n: {item.temporaryRooms} salas");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formatea un efecto para mostrar
    /// </summary>
    private string FormatEffect(ItemEffect effect, bool isBenefit)
    {
        string statName = GetStatDisplayName(effect.type);
        string sign = isBenefit ? "+" : "-";

        if (effect.isPercentage)
        {
            return $"{sign}{effect.amount:F0}% {statName}";
        }
        else
        {
            return $"{sign}{effect.amount:F2} {statName}";
        }
    }

    /// <summary>
    /// Formatea un valor de estad�stica con su modificador
    /// </summary>
    private string FormatStatValue(float current, float baseValue)
    {
        float diff = current - baseValue;

        if (Mathf.Abs(diff) < 0.01f)
        {
            return $"{current:F0}";
        }
        else if (diff > 0)
        {
            string color = ColorUtility.ToHtmlStringRGB(positiveStatColor);
            return $"{baseValue:F0} <color=#{color}>+{diff:F0}</color>";
        }
        else
        {
            string color = ColorUtility.ToHtmlStringRGB(negativeStatColor);
            return $"{baseValue:F0} <color=#{color}>{diff:F0}</color>";
        }
    }

    /// <summary>
    /// Formatea un valor de estad�stica con su modificador (CON DECIMALES)
    /// </summary>
    private string FormatStatValueDecimal(float current, float baseValue)
    {
        float diff = current - baseValue;

        if (Mathf.Abs(diff) < 0.01f)
        {
            return $"{current:F2}";
        }
        else if (diff > 0)
        {
            string color = ColorUtility.ToHtmlStringRGB(positiveStatColor);
            return $"{baseValue:F2} <color=#{color}>+{diff:F2}</color>";
        }
        else
        {
            string color = ColorUtility.ToHtmlStringRGB(negativeStatColor);
            return $"{baseValue:F2} <color=#{color}>{diff:F2}</color>";
        }
    }

    /// <summary>
    /// Obtiene el nombre de visualizaci�n de una estad�stica
    /// </summary>
    private string GetStatDisplayName(StatType type)
    {
        switch (type)
        {
            case StatType.MaxHealth: return "Vida M�xima";
            case StatType.MoveSpeed: return "Velocidad";
            case StatType.AttackDamage: return "Da�o";
            case StatType.AttackSpeed: return "Velocidad de Ataque";
            case StatType.MeleeAttackDamage: return "Da�o Cuerpo a Cuerpo";
            case StatType.MeleeAttackSpeed: return "Vel. Ataque Cuerpo aC";
            case StatType.ShieldAttackDamage: return "Da�o de Escudo";
            case StatType.ShieldSpeed: return "Velocidad de Escudo";
            case StatType.CriticalChance: return "Prob. Cr�tico";
            case StatType.LifestealOnKill: return "Robo de Vida";
            default: return type.ToString();
        }
    }

    /// <summary>
    /// Obtiene el nombre de una categor�a
    /// </summary>
    private string GetCategoryName(ItemCategory category)
    {
        switch (category)
        {
            case ItemCategory.Reliquia: return "Reliquias";
            case ItemCategory.Ganga: return "Gangas";
            case ItemCategory.Acondicionador: return "Acondicionadores";
            case ItemCategory.Potenciador: return "Potenciadores";
            case ItemCategory.Debilitador: return "Debilitadores";
            case ItemCategory.Maldicion: return "Maldiciones";
            case ItemCategory.Amuleto: return "Amuletos";
            default: return category.ToString();
        }
    }

    /// <summary>
    /// Obtiene el nombre de una categor�a de efecto
    /// </summary>
    private string GetEffectCategoryName(EffectCategory category)
    {
        switch (category)
        {
            case EffectCategory.Combat: return "Combate";
            case EffectCategory.Defense: return "Defensa";
            case EffectCategory.Utility: return "Utilidad";
            case EffectCategory.Healing: return "Curaci�n";
            case EffectCategory.Damage: return "Da�o";
            case EffectCategory.Special: return "Especial";
            default: return category.ToString();
        }
    }

    public Color GetHighlightColor() => highlightColor;
}