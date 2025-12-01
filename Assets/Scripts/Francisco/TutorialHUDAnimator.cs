using System.Collections;
using UnityEngine;
using TMPro;

public class TutorialHUDAnimator : MonoBehaviour
{
    #region REFERENCES

    [Header("UI References")]
    [SerializeField] private RectTransform maskRect;
    [SerializeField] private GameObject checkmarkIcon;
    [SerializeField] private TextMeshProUGUI instructionText;

    #endregion

    #region CONFIGURATION

    [Header("Animation Configuration")]
    [SerializeField] private float animationDuration = 0.3f;
    [SerializeField] private float fullWidth = 400f;
    [SerializeField] private float checkDisplayTime = 0.5f;

    #endregion

    #region PRIVATE_FIELDS

    private float initialMaskX;
    private Coroutine currentAnimationCoroutine;
    private string currentInstructionText = "";

    #endregion

    #region LIFECYCLE

    private void Awake()
    {
        initialMaskX = maskRect.anchoredPosition.x;
        SetMaskWidth(0);
        if (checkmarkIcon != null) checkmarkIcon.SetActive(false);
        gameObject.SetActive(false);
        if (instructionText != null) currentInstructionText = instructionText.text;
    }

    private void Update()
    {
        if (gameObject.activeSelf && !string.IsNullOrEmpty(currentInstructionText) && InputIconManager.Instance != null)
        {
            string updatedText = ProcessTextWithInputIcons(currentInstructionText);
            if (instructionText != null && instructionText.text != updatedText)
            {
                instructionText.text = updatedText;
            }
        }
    }

    #endregion

    #region PUBLIC_METHODS

    public void SetAndShowInstruction(string newText)
    {
        bool instructionChanged = SetInstructionText(newText);

        if (instructionChanged)
        {
            ShowInstruction();
        }
    }

    public void ShowInstruction()
    {
        if (gameObject.activeSelf) return;

        gameObject.SetActive(true);
        if (currentAnimationCoroutine != null) StopCoroutine(currentAnimationCoroutine);
        currentAnimationCoroutine = StartCoroutine(AnimateInCoroutine());
    }

    public void CompleteInstruction()
    {
        if (!gameObject.activeSelf) return;

        if (currentAnimationCoroutine != null) StopCoroutine(currentAnimationCoroutine);
        currentAnimationCoroutine = StartCoroutine(AnimateCheckAndOutCoroutine());
        currentInstructionText = "";
    }

    public bool SetInstructionText(string newText)
    {
        if (newText.Equals(currentInstructionText))
        {
            Debug.Log($"Ignorando SetInstructionText: El texto '{newText}' ya está activo.");
            return false;
        }

        currentInstructionText = newText;
        if (instructionText != null)
        {
            instructionText.text = ProcessTextWithInputIcons(newText);
        }

        return true;
    }

    #endregion

    #region TEXT_PROCESSING

    private string ProcessTextWithInputIcons(string text)
    {
        if (InputIconManager.Instance == null) return text;

        string processedText = text;
        int startIndex = 0;

        while (startIndex < processedText.Length)
        {
            int exclamationIndex = processedText.IndexOf('!', startIndex);
            if (exclamationIndex == -1) break;

            int spaceIndex = processedText.IndexOf(' ', exclamationIndex);
            int endIndex = spaceIndex == -1 ? processedText.Length : spaceIndex;

            string actionName = processedText.Substring(exclamationIndex + 1, endIndex - exclamationIndex - 1);

            if (!string.IsNullOrEmpty(actionName))
            {
                string iconSprite = InputIconManager.Instance.GetPromptForAction(actionName);
                processedText = processedText.Substring(0, exclamationIndex) + iconSprite + processedText.Substring(endIndex);
                startIndex = exclamationIndex + iconSprite.Length;
            }
            else
            {
                startIndex = exclamationIndex + 1;
            }
        }

        return processedText;
    }

    #endregion

    #region ANIMATION_LOGIC

    private void SetMaskWidth(float width)
    {
        maskRect.sizeDelta = new Vector2(width, maskRect.sizeDelta.y);
    }

    private IEnumerator AnimateInCoroutine()
    {
        SetMaskWidth(0);
        maskRect.anchoredPosition = new Vector2(initialMaskX, maskRect.anchoredPosition.y);
        if (checkmarkIcon != null) checkmarkIcon.SetActive(false);

        float startTime = Time.time;
        float endWidth = fullWidth;

        while (Time.time < startTime + animationDuration)
        {
            float t = (Time.time - startTime) / animationDuration;
            float currentWidth = Mathf.Lerp(0, endWidth, t);
            SetMaskWidth(currentWidth);
            yield return null;
        }

        SetMaskWidth(endWidth);
        currentAnimationCoroutine = null;
    }

    private IEnumerator AnimateCheckAndOutCoroutine()
    {
        if (checkmarkIcon != null)
        {
            checkmarkIcon.SetActive(true);
        }

        yield return new WaitForSeconds(checkDisplayTime);

        float startTime = Time.time;
        float startX = maskRect.anchoredPosition.x;
        float endX = startX - fullWidth;

        while (Time.time < startTime + animationDuration)
        {
            float t = (Time.time - startTime) / animationDuration;
            float currentX = Mathf.Lerp(startX, endX, t);
            maskRect.anchoredPosition = new Vector2(currentX, maskRect.anchoredPosition.y);
            yield return null;
        }

        gameObject.SetActive(false);
        maskRect.anchoredPosition = new Vector2(initialMaskX, maskRect.anchoredPosition.y);
        SetMaskWidth(0);
        if (checkmarkIcon != null) checkmarkIcon.SetActive(false);
        currentAnimationCoroutine = null;
    }

    #endregion
}