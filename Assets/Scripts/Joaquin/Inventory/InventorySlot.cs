using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Componente para cada slot de ítem en el inventario
/// </summary>
public class InventorySlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Referencias UI")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image rarityBorder;
    [SerializeField] private GameObject temporalEffectObject;
    [SerializeField] private Image pulseGlowImage;

    [Header("Configuración de Efectos")]
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseIntensity = 0.3f;
    [SerializeField] private bool enableDebugLogs = false;

    private ShopItem itemData;
    private InventoryUIManager inventoryManager;
    private Color originalBackgroundColor;
    private Color originalBorderColor;
    private bool hasItem = false;

    private Coroutine pulseCoroutine;

    public void Initialize(InventoryUIManager manager)
    {
        inventoryManager = manager;

        if (backgroundImage != null)
        {
            originalBackgroundColor = backgroundImage.color;
        }

        if (temporalEffectObject != null)
        {
            temporalEffectObject.SetActive(false);
        }

        ClearSlot();

        DebugLog($"Slot inicializado en {gameObject.name}");
    }

    /// <summary>
    /// Asigna un ítem a este slot
    /// </summary>
    public void SetItem(ShopItem item)
    {
        itemData = item;
        hasItem = item != null;

        if (hasItem)
        {
            DebugLog($"Asignando ítem: {item.itemName}");

            // Mostrar icono
            if (iconImage != null)
            {
                if (item.itemIcon != null)
                {
                    iconImage.sprite = item.itemIcon;
                    iconImage.enabled = true;
                }
                else
                {
                    iconImage.enabled = false;
                    DebugLog($"Ítem {item.itemName} no tiene icono asignado");
                }
            }

            // Aplicar color de rareza al borde
            if (rarityBorder != null)
            {
                rarityBorder.color = item.GetRarityColor();
                rarityBorder.enabled = true;
                originalBorderColor = rarityBorder.color;
            }

            // Mostrar efecto temporal si aplica
            if (item.isTemporary)
            {
                ShowTemporalEffect();
            }
            else
            {
                HideTemporalEffect();
            }
        }
        else
        {
            ClearSlot();
        }
    }

    /// <summary>
    /// Limpia el slot
    /// </summary>
    public void ClearSlot()
    {
        itemData = null;
        hasItem = false;

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        if (rarityBorder != null)
        {
            rarityBorder.enabled = false;
        }

        HideTemporalEffect();
    }

    /// <summary>
    /// Muestra el efecto de ítem temporal
    /// </summary>
    private void ShowTemporalEffect()
    {
        if (temporalEffectObject != null)
        {
            temporalEffectObject.SetActive(true);
        }

        // Detener coroutine anterior si existe
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }

        // SOLO iniciar si el GameObject está activo
        if (gameObject.activeInHierarchy)
        {
            pulseCoroutine = StartCoroutine(PulseEffect());
            DebugLog($"Efecto de pulso iniciado para {itemData?.itemName}");
        }
        else
        {
            DebugLog($"No se puede iniciar pulso, GameObject inactivo");
        }
    }

    /// <summary>
    /// Oculta el efecto de ítem temporal
    /// </summary>
    private void HideTemporalEffect()
    {
        if (temporalEffectObject != null)
        {
            temporalEffectObject.SetActive(false);
        }

        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }

        // Restaurar color original del borde
        if (rarityBorder != null && hasItem)
        {
            rarityBorder.color = originalBorderColor;
        }
    }

    /// <summary>
    /// Efecto de pulso para ítems temporales
    /// </summary>
    private IEnumerator PulseEffect()
    {
        if (!hasItem || itemData == null)
        {
            DebugLog("Pulso cancelado: sin ítem");
            yield break;
        }

        Color baseColor = originalBorderColor;

        // Si hay una imagen de glow, usarla también
        Color glowBaseColor = pulseGlowImage != null ?
            new Color(1f, 0.27f, 0f, 0.5f) : // Naranja
            Color.clear;

        while (hasItem && itemData != null && itemData.isTemporary)
        {
            float pulse = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(1f - pulseIntensity, 1f, pulse);

            // Pulsar el borde de rareza
            if (rarityBorder != null)
            {
                rarityBorder.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            }

            // Pulsar el glow si existe
            if (pulseGlowImage != null)
            {
                float glowAlpha = Mathf.Lerp(0.2f, 0.6f, pulse);
                pulseGlowImage.color = new Color(
                    glowBaseColor.r,
                    glowBaseColor.g,
                    glowBaseColor.b,
                    glowAlpha
                );
            }

            yield return null;
        }

        pulseCoroutine = null;
    }

    private void OnEnable()
    {
        // Si hay un ítem temporal, reiniciar el efecto
        if (hasItem && itemData != null && itemData.isTemporary)
        {
            ShowTemporalEffect();
        }
    }

    private void OnDisable()
    {
        // Limpiar coroutine al desactivarse
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }
    }

    #region Event Handlers

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!hasItem) return;

        DebugLog($"Hover en {itemData.itemName}");

        // Aplicar highlight carmesí
        if (backgroundImage != null && inventoryManager != null)
        {
            Color highlightColor = inventoryManager.GetHighlightColor();
            backgroundImage.color = Color.Lerp(originalBackgroundColor, highlightColor, 0.5f);
        }

        // Efecto de escala
        transform.localScale = Vector3.one * 1.1f;

        // Mostrar tooltip
        if (InventoryTooltip.Instance != null && itemData != null)
        {
            InventoryTooltip.Instance.Show(itemData);
        }

        // Reproducir sonido de hover
        if (InventoryAudioManager.Instance != null)
        {
            InventoryAudioManager.Instance.PlayHoverSound();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!hasItem) return;

        DebugLog($"Exit hover de {itemData.itemName}");

        // Restaurar color original
        if (backgroundImage != null)
        {
            backgroundImage.color = originalBackgroundColor;
        }

        // Restaurar escala
        transform.localScale = Vector3.one;

        // Ocultar tooltip
        if (InventoryTooltip.Instance != null)
        {
            InventoryTooltip.Instance.Hide();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        DebugLog($"Click detectado en slot. HasItem: {hasItem}, Manager: {inventoryManager != null}");

        if (!hasItem)
        {
            DebugLog("Click ignorado: slot vacío");
            return;
        }

        if (inventoryManager == null)
        {
            Debug.LogError("[InventorySlot] InventoryUIManager es null. No se puede mostrar detalles.");
            return;
        }

        // Click izquierdo para ver detalles
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            DebugLog($"Mostrando detalles de {itemData.itemName}");
            inventoryManager.ShowItemDetails(itemData);

            // Reproducir sonidos
            if (InventoryAudioManager.Instance != null)
            {
                InventoryAudioManager.Instance.PlayClickSound();
                InventoryAudioManager.Instance.PlayRaritySound(itemData.rarity);
            }
        }
    }

    #endregion

    #region Debug Helpers

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[InventorySlot-{gameObject.name}] {message}");
        }
    }

    [ContextMenu("Test - Log Current State")]
    private void LogCurrentState()
    {
        Debug.Log("=== INVENTORY SLOT STATE ===");
        Debug.Log($"GameObject: {gameObject.name}");
        Debug.Log($"Active: {gameObject.activeInHierarchy}");
        Debug.Log($"Has Item: {hasItem}");
        Debug.Log($"Item Data: {(itemData != null ? itemData.itemName : "null")}");
        Debug.Log($"Manager: {(inventoryManager != null ? "Assigned" : "NULL")}");
        Debug.Log($"Background Image: {(backgroundImage != null ? "Assigned" : "NULL")}");
        Debug.Log($"Icon Image: {(iconImage != null ? "Assigned" : "NULL")}");
        Debug.Log($"Rarity Border: {(rarityBorder != null ? "Assigned" : "NULL")}");
        Debug.Log($"Temporal Effect: {(temporalEffectObject != null ? "Assigned" : "NULL")}");
        Debug.Log($"Pulse Coroutine: {(pulseCoroutine != null ? "Running" : "Stopped")}");
        Debug.Log("===========================");
    }

    #endregion
}