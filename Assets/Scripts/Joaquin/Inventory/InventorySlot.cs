using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Slot individual del inventario.
/// </summary>
public class InventorySlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    #region Inspector - References

    [Header("Referencias UI")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image rarityBorder;
    [SerializeField] private GameObject temporalEffectObject;
    [SerializeField] private Image pulseGlowImage;

    #endregion

    #region Inspector - Effect Settings

    [Header("Efectos de Pulso")]
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseIntensity = 0.3f;

    #endregion

    #region Internal State

    private ShopItem itemData;
    private InventoryUIManager inventoryManager;
    private Color originalBackgroundColor;
    private Color originalBorderColor;
    private bool hasItem;
    private bool isGoldenSlot;
    private Coroutine pulseCoroutine;

    #endregion

    #region Public Properties & Events

    /// <summary>Item actualmente asignado (puede ser null).</summary>
    public ShopItem CurrentItem => itemData;

    /// <summary>True si el slot tiene un item.</summary>
    public bool HasItem => hasItem;

    /// <summary>RectTransform de este slot.</summary>
    public RectTransform SlotRect => GetComponent<RectTransform>();

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (hasItem && itemData != null && itemData.isTemporary) ShowTemporalEffect();
    }

    private void OnDisable()
    {
        if (pulseCoroutine != null) { StopCoroutine(pulseCoroutine); pulseCoroutine = null; }
    }

    #endregion

    #region Initialization & Data Sync

    public void Initialize(InventoryUIManager manager)
    {
        inventoryManager = manager;
        if (backgroundImage != null) originalBackgroundColor = backgroundImage.color;
        if (temporalEffectObject != null) temporalEffectObject.SetActive(false);
        ClearSlot();
    }

    public void SetGolden(bool golden, Color color)
    {
        isGoldenSlot = golden;
        if (backgroundImage != null && golden)
        {
            backgroundImage.color = new Color(color.r, color.g, color.b, 0.15f);
            originalBackgroundColor = backgroundImage.color;
        }
        if (rarityBorder != null && golden)
        {
            rarityBorder.color = color;
            rarityBorder.enabled = true;
            originalBorderColor = color;
        }
    }

    #endregion

    #region Item Management

    public void SetItem(ShopItem item)
    {
        itemData = item;
        hasItem = item != null;

        if (!hasItem)
        {
            ClearSlot();
            return;
        }

        if (iconImage != null)
        {
            iconImage.sprite = item.itemIcon;
            iconImage.enabled = item.itemIcon != null;
        }

        if (rarityBorder != null)
        {
            rarityBorder.color = isGoldenSlot ? originalBorderColor : item.GetRarityColor();
            rarityBorder.enabled = true;
            if (!isGoldenSlot) originalBorderColor = rarityBorder.color;
        }

        if (item.isTemporary) ShowTemporalEffect();
        else HideTemporalEffect();
    }

    public void ClearSlot()
    {
        itemData = null;
        hasItem = false;

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        if (!isGoldenSlot && rarityBorder != null)
        {
            rarityBorder.enabled = false;
        }

        HideTemporalEffect();
    }

    #endregion

    #region Visual & Audio Effects

    private void ShowTemporalEffect()
    {
        if (temporalEffectObject != null) temporalEffectObject.SetActive(true);

        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }

        if (gameObject.activeInHierarchy) pulseCoroutine = StartCoroutine(PulseEffect());
    }

    private void HideTemporalEffect()
    {
        if (temporalEffectObject != null) temporalEffectObject.SetActive(false);

        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }

        if (rarityBorder != null && hasItem) rarityBorder.color = originalBorderColor;
    }

    private IEnumerator PulseEffect()
    {
        Color base_ = originalBorderColor;
        while (hasItem && itemData != null && itemData.isTemporary)
        {
            float p = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(1f - pulseIntensity, 1f, p);
            if (rarityBorder != null)
            {
                rarityBorder.color = new Color(base_.r, base_.g, base_.b, alpha);
            }

            if (pulseGlowImage != null)
            {
                pulseGlowImage.color = new Color(1f, 0.27f, 0f, Mathf.Lerp(0.2f, 0.6f, p));
            }
            yield return null;
        }
        pulseCoroutine = null;
    }

    #endregion

    #region Pointer & Hover Interactions

    /// <summary>
    /// Aplica el estado visual y sonoro de "hover enter".
    /// <paramref name="gamepadMode"/> true => el tooltip se posiciona centrado en el slot.
    /// </summary>
    private void ApplyHoverEnter(bool gamepadMode)
    {
        if (!hasItem) return;

        if (backgroundImage != null && inventoryManager != null)
        {
            backgroundImage.color = Color.Lerp(originalBackgroundColor,
                                                inventoryManager.GetHighlightColor(), 0.5f);
        }
        transform.localScale = Vector3.one * 1.05f;

        if (gamepadMode) InventoryTooltip.Instance?.ShowForSlot(itemData, SlotRect);
        else InventoryTooltip.Instance?.Show(itemData);

        if (isGoldenSlot) InventoryAudioManager.Instance?.PlayGoldenSlotHoverSound();
        else InventoryAudioManager.Instance?.PlayCommonSlotHoverSound();
    }

    /// <summary>Aplica el estado visual de "hover exit" y oculta el tooltip.</summary>
    private void ApplyHoverExit()
    {
        if (backgroundImage != null) backgroundImage.color = originalBackgroundColor;
        transform.localScale = Vector3.one;
        InventoryTooltip.Instance?.Hide();
    }

    /// <summary>Simula que el cursor del mando entra en este slot.</summary>
    public void SimulatePointerEnter(bool gamepadMode = true) => ApplyHoverEnter(gamepadMode);

    /// <summary>Simula que el cursor del mando sale de este slot.</summary>
    public void SimulatePointerExit()
    {
        ApplyHoverExit();
    }

    /// <summary>Simula un clic del mando sobre este slot (Button North).</summary>
    public void SimulateClick()
    {
        if (!hasItem) return;
        inventoryManager?.OnSlotClicked(this, isGamepad: true);
        InventoryAudioManager.Instance?.PlayClickSound();
        InventoryAudioManager.Instance?.PlayRaritySound(itemData.rarity);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ApplyHoverEnter(gamepadMode: false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!hasItem) return;
        ApplyHoverExit();
    }

    /// <summary>
    /// Delega la logica de seleccion al manager en lugar de manejar isClicked localmente.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!hasItem) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;

        inventoryManager?.OnSlotClicked(this, isGamepad: false);
        InventoryAudioManager.Instance?.PlayClickSound();
        InventoryAudioManager.Instance?.PlayRaritySound(itemData.rarity);
    }

    #endregion
}