using DG.Tweening;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SaveGamePanel : MonoBehaviour
{
    public enum PanelDisplayType { AnimatedScale, CanvasFade, Static }

    [Header("Panel Display Settings")]
    [SerializeField] private PanelDisplayType displayType = PanelDisplayType.AnimatedScale;

    [Header("Canvas Group Reference")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Save Slot Buttons")]
    [SerializeField] private List<Button> saveSlotButtons;

    [Header("Delete Buttons")]
    [SerializeField] private List<GameObject> deleteButtons;

    [Header("DOTween Settings")]
    [SerializeField] private float openCloseDuration = 0.3f;
    [SerializeField] private Ease openEase = Ease.OutBack;
    [SerializeField] private Ease closeEase = Ease.InBack;

    [Header("Scale Animation Vectors")]
    [SerializeField] private Vector3 startScale = Vector3.zero;
    [SerializeField] private Vector3 endScale = Vector3.one;

    [Header("Focus Control")]
    [SerializeField] private GameObject firstSelectedButton;

    private bool isPanelOpen = false;

    private void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (displayType == PanelDisplayType.AnimatedScale)
        {
            transform.localScale = startScale;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        SetupSlotButtons();
    }

    private void SetupSlotButtons()
    {
        if (saveSlotButtons.Count != deleteButtons.Count)
        {
            Debug.LogError("[SaveGamePanel] Los arrays de 'Save Slot Buttons' y 'Delete Buttons' deben tener el mismo tamaño.");
            return;
        }

        for (int i = 0; i < saveSlotButtons.Count; i++)
        {
            int slotIndex = i + 1;
            Button slotButton = saveSlotButtons[i];
            GameObject deleteGameObject = deleteButtons[i];

            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(() => OnSlotSelected(slotIndex));

            Button deleteButton = deleteGameObject.GetComponent<Button>();
            if (deleteButton != null)
            {
                deleteButton.onClick.RemoveAllListeners();
                deleteButton.onClick.AddListener(() => HandleSlotDeletion(slotIndex));
            }

            bool slotExists = SaveLoadManager.Instance != null && SaveLoadManager.Instance.DoesSlotExist(slotIndex);

            if (slotExists)
            {
                slotButton.GetComponentInChildren<TextMeshProUGUI>().text = $"Slot {slotIndex}: Partida Guardada";
                deleteGameObject.SetActive(true);
            }
            else
            {
                slotButton.GetComponentInChildren<TextMeshProUGUI>().text = $"Slot {slotIndex}: Nueva Partida";
                deleteGameObject.SetActive(false);
            }
        }
    }

    private void OnSlotSelected(int slotIndex)
    {
        if (SaveLoadManager.Instance != null)
        {
            FadeOutAndLoad(slotIndex);
        }
    }

    private void FadeOutAndLoad(int slotIndex)
    {
        if (!isPanelOpen) return;
        isPanelOpen = false;

        if (EventSystem.current.currentSelectedGameObject != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        if (displayType == PanelDisplayType.AnimatedScale)
        {
            transform.DOKill();
        }

        if (SaveLoadManager.Instance != null)
        {
            SaveLoadManager.Instance.StartGameFromSlot(slotIndex);
        }
    }

    public void HandleSlotDeletion(int slotIndex)
    {
        if (SaveLoadManager.Instance != null)
        {
            GameObject focusedObject = EventSystem.current.currentSelectedGameObject;

            SaveLoadManager.Instance.DeleteGameSlot(slotIndex);

            SetupSlotButtons();

            if (Gamepad.current != null)
            {
                int arrayIndex = slotIndex - 1;

                if (arrayIndex >= 0 && arrayIndex < saveSlotButtons.Count)
                {
                    Button targetButton = saveSlotButtons[arrayIndex];

                    if (targetButton != null && targetButton.gameObject.activeInHierarchy)
                    {
                        EventSystem.current.SetSelectedGameObject(targetButton.gameObject);
                    }
                    else
                    {
                        if (firstSelectedButton != null)
                        {
                            EventSystem.current.SetSelectedGameObject(firstSelectedButton);
                        }
                    }
                }
            }
        }
    }

    public void ShowPanel()
    {
        if (isPanelOpen) return;
        isPanelOpen = true;

        SetupSlotButtons();

        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        if (displayType == PanelDisplayType.AnimatedScale)
        {
            transform.DOScale(endScale, openCloseDuration).SetEase(openEase).SetUpdate(true);
            canvasGroup.DOFade(1f, openCloseDuration).SetUpdate(true);
        }
        else if (displayType == PanelDisplayType.CanvasFade)
        {
            canvasGroup.DOFade(1f, openCloseDuration).SetUpdate(true);
        }
        else
        {
            canvasGroup.alpha = 1f;
        }

        if (firstSelectedButton != null)
        {
            EventSystem.current.SetSelectedGameObject(firstSelectedButton);
        }
    }

    public void HidePanel()
    {
        if (!isPanelOpen) return;
        isPanelOpen = false;

        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        if (EventSystem.current.currentSelectedGameObject != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        if (displayType == PanelDisplayType.AnimatedScale)
        {
            transform.DOScale(startScale, openCloseDuration).SetEase(closeEase).SetUpdate(true);
            canvasGroup.DOFade(0f, openCloseDuration).SetUpdate(true);
        }
        else if (displayType == PanelDisplayType.CanvasFade)
        {
            canvasGroup.DOFade(0f, openCloseDuration).SetUpdate(true);
        }
        else
        {
            canvasGroup.alpha = 0f;
        }
    }
}