using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using TMPro; 

public class CreditsPanel : MonoBehaviour
{
    public enum PanelDisplayType { AnimatedScale, CanvasFade, Animator, Static }
    public enum EntryAlignment { Left, Center, Right }
    public enum EntryType { Title, Subtitle, Role, Name, RoleWithName, ImageWithText, Spacer }

    #region [ References ]

    [Header("Panel Display Settings")]
    public PanelDisplayType displayType = PanelDisplayType.AnimatedScale;

    [Header("Canvas Group Reference")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Animator Reference")]
    [SerializeField] private Animator panelAnimator;
    [SerializeField] private string openTrigger = "Open";
    [SerializeField] private string closeTrigger = "Close";

    [Header("DOTween Settings")]
    [SerializeField] private float openCloseDuration = 0.3f;
    [SerializeField] private Ease openEase = Ease.OutBack;
    [SerializeField] private Ease closeEase = Ease.InBack;

    [Header("Scale Animation Vectors")]
    [SerializeField] private Vector3 startScale = Vector3.zero;
    [SerializeField] private Vector3 endScale = Vector3.one;

    [Header("Focus Control")]
    [SerializeField] private GameObject firstSelectedButton;

    [Header("Credits Container")]
    [SerializeField] private RectTransform creditsContainer;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private float scrollSpeed = 30f;
    [SerializeField] private float autoScrollDelay = 1f;

    [Header("Credits Content")]
    [SerializeField] private CreditsEntry[] creditsEntries;

    [Header("Prefab References")]
    [SerializeField] private GameObject titlePrefab;
    [SerializeField] private GameObject subtitlePrefab;
    [SerializeField] private GameObject rolePrefab;
    [SerializeField] private GameObject namePrefab;
    [SerializeField] private GameObject roleWithNamePrefab;
    [SerializeField] private GameObject imageWithTextPrefab;
    [SerializeField] private GameObject spacerPrefab;

    [Header("Style Settings")]
    [SerializeField] private float defaultSpacing = 20f;
    [SerializeField] private Color titleColor = Color.white;
    [SerializeField] private Color subtitleColor = Color.gray;
    [SerializeField] private Color roleColor = new Color(0.8f, 0.8f, 0.8f);
    [SerializeField] private Color nameColor = Color.white;

    private bool isScrolling = false;
    private Coroutine autoScrollCoroutine;
    private List<GameObject> instantiatedObjects = new List<GameObject>();

    #endregion

    #region [ Data Structures ]

    [System.Serializable]
    public class CreditsEntry
    {
        public EntryType entryType;
        public string textContent;
        public string secondaryText;
        public Sprite image;
        public EntryAlignment alignment = EntryAlignment.Center;
        public float customSpacing = 0f;
        public bool useCustomColor = false;
        public Color customColor = Color.white;
        public int fontSize = 0;
    }

    #endregion

    #region [ Unity Methods ]

    private void Start()
    {
        if (displayType == PanelDisplayType.AnimatedScale)
        {
            transform.localScale = startScale;
        }
        else if (displayType == PanelDisplayType.CanvasFade && canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (autoScrollCoroutine != null)
        {
            StopCoroutine(autoScrollCoroutine);
        }
    }

    #endregion

    #region [ Panel Control ]

    public void OpenPanel()
    {
        gameObject.SetActive(true);
        BuildCredits();

        if (displayType == PanelDisplayType.AnimatedScale)
        {
            transform.localScale = startScale;
            transform.DOScale(endScale, openCloseDuration)
                .SetEase(openEase)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    SetInitialFocus();
                    StartAutoScroll();
                });
        }
        else if (displayType == PanelDisplayType.CanvasFade && canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = true;

            canvasGroup.DOFade(1f, openCloseDuration)
                .SetEase(Ease.OutSine)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    SetInitialFocus();
                    StartAutoScroll();
                });
        }
        else if (displayType == PanelDisplayType.Animator && panelAnimator != null)
        {
            panelAnimator.SetTrigger(openTrigger);
            SetInitialFocus();
            StartAutoScroll();
        }
        else
        {
            SetInitialFocus();
            StartAutoScroll();
        }
    }

    public void ClosePanel()
    {
        StopAutoScroll();

        if (displayType == PanelDisplayType.AnimatedScale)
        {
            transform.DOScale(startScale, openCloseDuration)
                .SetEase(closeEase)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    gameObject.SetActive(false);
                    ClearCredits();
                });
        }
        else if (displayType == PanelDisplayType.CanvasFade && canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;

            canvasGroup.DOFade(0f, openCloseDuration)
                .SetEase(Ease.InSine)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    gameObject.SetActive(false);
                    ClearCredits();
                });
        }
        else if (displayType == PanelDisplayType.Animator && panelAnimator != null)
        {
            panelAnimator.SetTrigger(closeTrigger);
            StartCoroutine(DelayedDeactivate());
        }
        else
        {
            gameObject.SetActive(false);
            ClearCredits();
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private IEnumerator DelayedDeactivate()
    {
        yield return new WaitForSecondsRealtime(openCloseDuration);
        gameObject.SetActive(false);
        ClearCredits();
    }

    #endregion

    #region [ Credits Building ]

    private void BuildCredits()
    {
        ClearCredits();

        if (creditsContainer == null) return;

        foreach (var entry in creditsEntries)
        {
            GameObject instance = null;

            switch (entry.entryType)
            {
                case EntryType.Title:
                    instance = InstantiatePrefab(titlePrefab);
                    SetupTextElement(instance, entry.textContent, entry);
                    break;

                case EntryType.Subtitle:
                    instance = InstantiatePrefab(subtitlePrefab);
                    SetupTextElement(instance, entry.textContent, entry);
                    break;

                case EntryType.Role:
                    instance = InstantiatePrefab(rolePrefab);
                    SetupTextElement(instance, entry.textContent, entry);
                    break;

                case EntryType.Name:
                    instance = InstantiatePrefab(namePrefab);
                    SetupTextElement(instance, entry.textContent, entry);
                    break;

                case EntryType.RoleWithName:
                    instance = InstantiatePrefab(roleWithNamePrefab);
                    SetupRoleWithName(instance, entry);
                    break;

                case EntryType.ImageWithText:
                    instance = InstantiatePrefab(imageWithTextPrefab);
                    SetupImageWithText(instance, entry);
                    break;

                case EntryType.Spacer:
                    instance = InstantiatePrefab(spacerPrefab);
                    SetupSpacer(instance, entry);
                    break;
            }

            if (instance != null)
            {
                SetAlignment(instance, entry.alignment);
                instantiatedObjects.Add(instance);
            }
        }

        Canvas.ForceUpdateCanvases();
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    private void ClearCredits()
    {
        foreach (var obj in instantiatedObjects)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }
        instantiatedObjects.Clear();
    }

    private GameObject InstantiatePrefab(GameObject prefab)
    {
        if (prefab == null) return null;
        return Instantiate(prefab, creditsContainer);
    }

    private void SetupTextElement(GameObject instance, string text, CreditsEntry entry)
    {
        if (instance == null) return;

        TextMeshProUGUI textComponent = instance.GetComponent<TextMeshProUGUI>();
        if (textComponent != null)
        {
            textComponent.text = text;

            if (entry.useCustomColor)
            {
                textComponent.color = entry.customColor;
            }
            else
            {
                textComponent.color = GetDefaultColor(entry.entryType);
            }

            if (entry.fontSize > 0)
            {
                textComponent.fontSize = entry.fontSize;
            }
        }
        else
        {
            Debug.LogError($"CreditsPanel: El prefab '{instance.name}' para {entry.entryType} NO tiene componente 'TextMeshProUGUI'. ¡Revisar Prefab!");
        }

        SetSpacing(instance, entry.customSpacing > 0 ? entry.customSpacing : defaultSpacing);
    }

    private void SetupRoleWithName(GameObject instance, CreditsEntry entry)
    {
        if (instance == null) return;

        TextMeshProUGUI[] texts = instance.GetComponentsInChildren<TextMeshProUGUI>();

        if (texts.Length >= 2)
        {
            texts[0].text = entry.textContent;
            texts[1].text = entry.secondaryText;

            if (entry.useCustomColor)
            {
                texts[0].color = entry.customColor;
                texts[1].color = entry.customColor;
            }
            else
            {
                texts[0].color = roleColor;
                texts[1].color = nameColor;
            }

            if (entry.fontSize > 0)
            {
                texts[0].fontSize = entry.fontSize;
                texts[1].fontSize = entry.fontSize;
            }
        }
        else
        {
            Debug.LogError($"CreditsPanel: El prefab '{instance.name}' para RoleWithName NO tiene al menos 2 componentes 'TextMeshProUGUI'. ¡Revisar Prefab!");
        }

        SetSpacing(instance, entry.customSpacing > 0 ? entry.customSpacing : defaultSpacing);
    }

    private void SetupImageWithText(GameObject instance, CreditsEntry entry)
    {
        if (instance == null) return;

        Image imageComponent = instance.GetComponentInChildren<Image>();
        if (imageComponent != null && entry.image != null)
        {
            imageComponent.sprite = entry.image;
        }

        TextMeshProUGUI textComponent = instance.GetComponentInChildren<TextMeshProUGUI>();
        if (textComponent != null)
        {
            textComponent.text = entry.textContent;

            if (entry.useCustomColor)
            {
                textComponent.color = entry.customColor;
            }

            if (entry.fontSize > 0)
            {
                textComponent.fontSize = entry.fontSize;
            }
        }
        else
        {
            Debug.LogError($"CreditsPanel: El prefab '{instance.name}' para ImageWithText NO tiene componente 'TextMeshProUGUI'. ¡Revisar Prefab!");
        }

        SetSpacing(instance, entry.customSpacing > 0 ? entry.customSpacing : defaultSpacing);
    }

    private void SetupSpacer(GameObject instance, CreditsEntry entry)
    {
        if (instance == null) return;

        LayoutElement layoutElement = instance.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = instance.AddComponent<LayoutElement>();
        }

        float spacerHeight = entry.customSpacing > 0 ? entry.customSpacing : defaultSpacing * 2;
        layoutElement.minHeight = spacerHeight;
        layoutElement.preferredHeight = spacerHeight;
    }

    private void SetSpacing(GameObject instance, float spacing)
    {
        LayoutElement layoutElement = instance.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = instance.AddComponent<LayoutElement>();
        }
        layoutElement.minHeight = spacing;
    }

    private void SetAlignment(GameObject instance, EntryAlignment alignment)
    {
        if (instance == null) return;

        HorizontalLayoutGroup horizontalLayout = instance.GetComponent<HorizontalLayoutGroup>();
        if (horizontalLayout != null)
        {
            switch (alignment)
            {
                case EntryAlignment.Left:
                    horizontalLayout.childAlignment = TextAnchor.MiddleLeft;
                    break;
                case EntryAlignment.Center:
                    horizontalLayout.childAlignment = TextAnchor.MiddleCenter;
                    break;
                case EntryAlignment.Right:
                    horizontalLayout.childAlignment = TextAnchor.MiddleRight;
                    break;
            }
        }

        TextMeshProUGUI textComponent = instance.GetComponent<TextMeshProUGUI>();
        if (textComponent != null)
        {
            switch (alignment)
            {
                case EntryAlignment.Left:
                    textComponent.alignment = TextAlignmentOptions.MidlineLeft;
                    break;
                case EntryAlignment.Center:
                    textComponent.alignment = TextAlignmentOptions.Midline;
                    break;
                case EntryAlignment.Right:
                    textComponent.alignment = TextAlignmentOptions.MidlineRight;
                    break;
            }
        }
    }

    private Color GetDefaultColor(EntryType type)
    {
        switch (type)
        {
            case EntryType.Title:
                return titleColor;
            case EntryType.Subtitle:
                return subtitleColor;
            case EntryType.Role:
                return roleColor;
            case EntryType.Name:
                return nameColor;
            default:
                return Color.white;
        }
    }

    #endregion

    #region [ Gamepad Focus Control ]

    private void SetInitialFocus()
    {
        if (firstSelectedButton == null) return;

        if (Gamepad.current == null) return;

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
        ResetInputDevices();
        EventSystem.current.SetSelectedGameObject(firstSelectedButton);
    }

    private void ResetInputDevices()
    {
        if (Gamepad.current != null)
        {
            InputSystem.ResetDevice(Gamepad.current);
        }
        if (Keyboard.current != null)
        {
            InputSystem.ResetDevice(Keyboard.current);
        }
        if (Mouse.current != null)
        {
            InputSystem.ResetDevice(Mouse.current);
        }
    }

    #endregion

    #region [ Auto Scroll ]

    private void StartAutoScroll()
    {
        if (autoScrollCoroutine != null)
        {
            StopCoroutine(autoScrollCoroutine);
        }
        autoScrollCoroutine = StartCoroutine(AutoScrollRoutine());
    }

    private void StopAutoScroll()
    {
        isScrolling = false;
        if (autoScrollCoroutine != null)
        {
            StopCoroutine(autoScrollCoroutine);
            autoScrollCoroutine = null;
        }
    }

    private IEnumerator AutoScrollRoutine()
    {
        yield return new WaitForSecondsRealtime(autoScrollDelay);

        isScrolling = true;

        if (scrollRect == null || creditsContainer == null)
        {
            Debug.LogError("CreditsPanel Routine: scrollRect o creditsContainer es nulo. Deteniendo scroll.");
            yield break;
        }

        while (isScrolling && scrollRect != null)
        {
            float contentHeight = creditsContainer.rect.height;

            if (contentHeight <= 0f)
            {
                //Debug.LogWarning("CreditsPanel Routine: Altura del Contenido es 0. Asegúrate de configurar Content Size Fitter.");
                yield return null;
                continue;
            }

            float normalizedSpeed = scrollSpeed / contentHeight;

            scrollRect.verticalNormalizedPosition += normalizedSpeed * Time.unscaledDeltaTime;

            if (scrollRect.verticalNormalizedPosition >= 1f)
            {
                scrollRect.verticalNormalizedPosition = 0f;
            }

            yield return null;
        }
    }

    public void PauseScroll()
    {
        isScrolling = false;
    }

    public void ResumeScroll()
    {
        if (!isScrolling && autoScrollCoroutine == null)
        {
            StartAutoScroll();
        }
        else
        {
            isScrolling = true;
        }
    }

    public void ResetScroll()
    {
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f;
        }
        StartAutoScroll();
    }

    #endregion

    #region [ Public Methods ]

    public void SetScrollSpeed(float speed)
    {
        scrollSpeed = Mathf.Max(0f, speed);
    }

    public void AddCreditsEntry(CreditsEntry entry)
    {
        var list = new List<CreditsEntry>(creditsEntries);
        list.Add(entry);
        creditsEntries = list.ToArray();
    }

    public void ClearAllEntries()
    {
        creditsEntries = new CreditsEntry[0];
        ClearCredits();
    }

    #endregion
}