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
    [SerializeField] private RectTransform creditsViewport;
    [SerializeField] private RectTransform creditsContainer;
    [SerializeField] private float scrollSpeed = 50f;
    [SerializeField] private float autoScrollDelay = 1f;
    [SerializeField] private bool loopCredits = true;

    [Header("Alignment Offsets")]
    [SerializeField] private float leftAlignmentOffset = 0f;
    [SerializeField] private float centerAlignmentOffset = 0f;
    [SerializeField] private float rightAlignmentOffset = 0f;

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

    private Tweener scrollTween;
    private List<GameObject> instantiatedObjects = new List<GameObject>();
    private float contentHeight;
    private float currentYPosition = 0f;
    private float currentLineMaxHeight = 0f; 

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
        public bool continueOnSameLine = false;
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
        StopAutoScroll();
    }

    #endregion

    #region [ Panel Control ]

    public void OpenPanel()
    {
        gameObject.SetActive(true);

        StopAutoScroll();

        if (creditsContainer != null)
        {
            creditsContainer.anchoredPosition = new Vector2(creditsContainer.anchoredPosition.x, 0f);
        }

        ClearCredits();

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
        currentYPosition = 0f;
        currentLineMaxHeight = 0f;

        if (creditsContainer == null)
        {
            Debug.LogError("CreditsPanel: creditsContainer es nulo!");
            return;
        }

        bool isFirstInLine = true;
        float lineStartY = 0f;
        float finalSpacingForLine = defaultSpacing; 

        for (int i = 0; i < creditsEntries.Length; i++)
        {
            var entry = creditsEntries[i];
            GameObject instance = null;
            float entryHeight = 0f;
            float entrySpacing = entry.customSpacing > 0 ? entry.customSpacing : defaultSpacing;

            if (isFirstInLine)
            {
                lineStartY = currentYPosition;
                currentLineMaxHeight = 0f;
            }

            finalSpacingForLine = entrySpacing;

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
                    entryHeight = entrySpacing * 2;
                    break;
            }

            if (instance != null)
            {
                SetAlignment(instance, entry);

                if (entry.entryType != EntryType.Spacer)
                {
                    Canvas.ForceUpdateCanvases();
                    entryHeight = instance.GetComponent<RectTransform>().rect.height;
                }

                RectTransform rt = instance.GetComponent<RectTransform>();

                float pivotY = rt.pivot.y;
                float posY = -lineStartY - entryHeight * pivotY;

                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, posY);

                if (entryHeight > currentLineMaxHeight)
                {
                    currentLineMaxHeight = entryHeight;
                }

                instantiatedObjects.Add(instance);

                if (!entry.continueOnSameLine)
                {
                    currentYPosition = lineStartY + currentLineMaxHeight + finalSpacingForLine;
                    isFirstInLine = true;
                }
                else
                {
                    isFirstInLine = false;
                }
            }
        }

        Canvas.ForceUpdateCanvases();
        contentHeight = currentYPosition;
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
            Debug.LogError($"CreditsPanel: El prefab '{instance.name}' para {entry.entryType} NO tiene componente 'TextMeshProUGUI'.");
        }
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
            Debug.LogError($"CreditsPanel: El prefab '{instance.name}' para RoleWithName NO tiene al menos 2 componentes 'TextMeshProUGUI'.");
        }
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
            Debug.LogError($"CreditsPanel: El prefab '{instance.name}' para ImageWithText NO tiene componente 'TextMeshProUGUI'.");
        }
    }

    private void SetAlignment(GameObject instance, CreditsEntry entry)
    {
        if (instance == null) return;

        RectTransform rectTransform = instance.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            switch (entry.alignment)
            {
                case EntryAlignment.Left:
                    rectTransform.anchorMin = new Vector2(0f, 0.5f);
                    rectTransform.anchorMax = new Vector2(0f, 0.5f);
                    rectTransform.pivot = new Vector2(0f, 0.5f);
                    rectTransform.anchoredPosition = new Vector2(leftAlignmentOffset, rectTransform.anchoredPosition.y);
                    break;
                case EntryAlignment.Center:
                    rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                    rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    rectTransform.anchoredPosition = new Vector2(centerAlignmentOffset, rectTransform.anchoredPosition.y);
                    break;
                case EntryAlignment.Right:
                    rectTransform.anchorMin = new Vector2(1f, 0.5f);
                    rectTransform.anchorMax = new Vector2(1f, 0.5f);
                    rectTransform.pivot = new Vector2(1f, 0.5f);
                    rectTransform.anchoredPosition = new Vector2(-rightAlignmentOffset, rectTransform.anchoredPosition.y);
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
        StopAutoScroll();

        if (creditsContainer == null || creditsViewport == null)
        {
            Debug.LogError("CreditsPanel: creditsContainer o creditsViewport es nulo. No se puede iniciar scroll.");
            return;
        }

        DOVirtual.DelayedCall(autoScrollDelay, () =>
        {
            if (this != null && gameObject.activeInHierarchy)
            {
                PerformScroll();
            }
        }, true);
    }

    private void PerformScroll()
    {
        if (contentHeight <= 0f)
        {
            Debug.LogWarning("CreditsPanel: La altura del contenido es 0. Asegúrate de que el Content Size Fitter esté configurado.");
            return;
        }

        float viewportHeight = creditsViewport.rect.height;
        float totalDistance = contentHeight + viewportHeight;

        float duration = totalDistance / scrollSpeed;

        creditsContainer.anchoredPosition = new Vector2(creditsContainer.anchoredPosition.x, 0f);

        float targetY = contentHeight + viewportHeight;

        scrollTween = creditsContainer.DOAnchorPosY(targetY, duration)
            .SetEase(Ease.Linear)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                if (loopCredits && this != null && gameObject.activeInHierarchy)
                {
                    PerformScroll();
                }
            });
    }

    private void StopAutoScroll()
    {
        if (scrollTween != null)
        {
            scrollTween.Kill();
            scrollTween = null;
        }
    }

    public void PauseScroll()
    {
        if (scrollTween != null && scrollTween.IsActive())
        {
            scrollTween.Pause();
        }
    }

    public void ResumeScroll()
    {
        if (scrollTween != null && scrollTween.IsActive())
        {
            scrollTween.Play();
        }
        else
        {
            StartAutoScroll();
        }
    }

    public void ResetScroll()
    {
        StopAutoScroll();
        if (creditsContainer != null)
        {
            creditsContainer.anchoredPosition = new Vector2(creditsContainer.anchoredPosition.x, 0f);
        }
        StartAutoScroll();
    }

    #endregion

    #region [ Gizmos ]

    private void OnDrawGizmos()
    {
        if (creditsViewport == null) return;

        Vector3[] viewportCorners = new Vector3[4];
        creditsViewport.GetWorldCorners(viewportCorners);

        float viewportZ = viewportCorners[0].z;

        float leftX = viewportCorners[0].x + leftAlignmentOffset;
        float centerX = (viewportCorners[0].x + viewportCorners[2].x) / 2f + centerAlignmentOffset;
        float rightX = viewportCorners[2].x - rightAlignmentOffset;

        float topY = viewportCorners[1].y;
        float bottomY = viewportCorners[0].y;

        Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
        Gizmos.DrawLine(new Vector3(leftX, bottomY, viewportZ), new Vector3(leftX, topY, viewportZ));

        Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
        Gizmos.DrawLine(new Vector3(centerX, bottomY, viewportZ), new Vector3(centerX, topY, viewportZ));

        Gizmos.color = new Color(0f, 0.5f, 1f, 0.5f);
        Gizmos.DrawLine(new Vector3(rightX, bottomY, viewportZ), new Vector3(rightX, topY, viewportZ));

        Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
        Gizmos.DrawLine(viewportCorners[0], viewportCorners[1]);
        Gizmos.DrawLine(viewportCorners[1], viewportCorners[2]);
        Gizmos.DrawLine(viewportCorners[2], viewportCorners[3]);
        Gizmos.DrawLine(viewportCorners[3], viewportCorners[0]);
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