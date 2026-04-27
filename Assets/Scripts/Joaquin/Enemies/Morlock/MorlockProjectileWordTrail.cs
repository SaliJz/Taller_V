using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class MorlockProjectileWordTrail : MonoBehaviour
{
    [Header("Letter Visuals")]
    [SerializeField] private GameObject letterPrefab;

    [Tooltip("Scale multiplier applied to each spawned letter prefab.")]
    [SerializeField, Min(0.1f)] private float letterScaleMultiplier = 0.7f;

    [Tooltip("Time between one visible letter and the next.")]
    [FormerlySerializedAs("revealDelay")]
    [SerializeField, Min(0f)] private float letterRevealDelay = 0.03f;

    [Tooltip("Initial offset of the first letter relative to the projectile.")]
    [FormerlySerializedAs("firstLetterLocalOffset")]
    [SerializeField] private Vector3 firstLetterOffset = new Vector3(-0.08f, 0.12f, 0.04f);

    [Tooltip("Minimum spacing kept between letters, even for narrow glyphs.")]
    [SerializeField, Min(0f)] private float baseLetterSpacing = 0.06f;

    [Tooltip("How much of each letter's real width is used to separate the trail.")]
    [SerializeField, Min(0f)] private float widthSpacingMultiplier = 1.15f;

    [Tooltip("Minimum local width used when calculating dynamic spacing.")]
    [SerializeField, Min(0f)] private float minimumLetterWidth = 32f;

    [Tooltip("Additional per-letter offsets. X adds extra horizontal spacing, Y vertical falloff, Z depth lag.")]
    [FormerlySerializedAs("letterLocalStep")]
    [SerializeField] private Vector3 letterOffsetStep = new Vector3(0.12f, -0.015f, 0.08f);

    [Header("Billboard")]
    [SerializeField] private bool alwaysFaceMainCamera = true;

    private readonly List<Transform> activeLetters = new List<Transform>();
    private readonly List<int> activeLetterIndices = new List<int>();
    private readonly List<float> activeLetterTrailOffsets = new List<float>();

    private Camera targetCamera;
    private Coroutine spawnRoutine;
    private string currentWord = string.Empty;

    public void InitializeWord(string word)
    {
        currentWord = string.IsNullOrWhiteSpace(word) ? string.Empty : word.Trim();

        if (!isActiveAndEnabled)
        {
            return;
        }

        RestartTrail();
    }

    private void Awake()
    {
        targetCamera = Camera.main;
    }

    private void OnEnable()
    {
        if (!string.IsNullOrEmpty(currentWord) && spawnRoutine == null && activeLetters.Count == 0)
        {
            spawnRoutine = StartCoroutine(SpawnLetters());
        }
    }

    private void OnDisable()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }

    private void LateUpdate()
    {
        if (!alwaysFaceMainCamera || activeLetters.Count == 0)
        {
            return;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            return;
        }

        for (int index = 0; index < activeLetters.Count; index++)
        {
            Transform letter = activeLetters[index];
            if (letter == null)
            {
                continue;
            }

            UpdateLetterPosition(
                letter,
                activeLetterIndices[index],
                activeLetterTrailOffsets[index],
                targetCamera.transform);
            FaceCamera(letter, targetCamera.transform);
        }
    }

    private void RestartTrail()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        ClearLetters();

        if (!string.IsNullOrEmpty(currentWord))
        {
            spawnRoutine = StartCoroutine(SpawnLetters());
        }
    }

    private IEnumerator SpawnLetters()
    {
        if (letterPrefab == null)
        {
            Debug.LogWarning($"[{nameof(MorlockProjectileWordTrail)}] Missing letterPrefab on {name}.", this);
            yield break;
        }

        int visibleLetterIndex = 0;
        float accumulatedTrailOffset = 0f;

        for (int index = 0; index < currentWord.Length; index++)
        {
            char currentCharacter = currentWord[index];

            if (!char.IsWhiteSpace(currentCharacter))
            {
                GameObject letterObject = Instantiate(letterPrefab, transform);
                letterObject.name = $"Letter_{visibleLetterIndex}_{currentCharacter}";

                Transform letterTransform = letterObject.transform;
                letterTransform.localScale *= letterScaleMultiplier;

                TMP_Text textComponent = letterObject.GetComponentInChildren<TMP_Text>(true);
                if (textComponent != null)
                {
                    textComponent.text = currentCharacter.ToString();
                }

                activeLetters.Add(letterTransform);
                activeLetterIndices.Add(visibleLetterIndex);
                activeLetterTrailOffsets.Add(accumulatedTrailOffset);

                if (targetCamera == null)
                {
                    targetCamera = Camera.main;
                }

                if (targetCamera != null)
                {
                    UpdateLetterPosition(
                        letterTransform,
                        visibleLetterIndex,
                        accumulatedTrailOffset,
                        targetCamera.transform);
                    FaceCamera(letterTransform, targetCamera.transform);
                }

                accumulatedTrailOffset += CalculateLetterSpacing(letterTransform, textComponent);
                visibleLetterIndex++;

                if (letterRevealDelay > 0f)
                {
                    yield return new WaitForSeconds(letterRevealDelay);
                }
                else
                {
                    yield return null;
                }
            }
        }

        spawnRoutine = null;
    }

    private float CalculateLetterSpacing(Transform letterTransform, TMP_Text textComponent)
    {
        float worldLetterWidth = minimumLetterWidth * letterTransform.lossyScale.x;

        if (textComponent != null)
        {
            Canvas.ForceUpdateCanvases();
            textComponent.ForceMeshUpdate();

            float measuredWidth = Mathf.Max(minimumLetterWidth, textComponent.preferredWidth);
            worldLetterWidth = measuredWidth * letterTransform.lossyScale.x;
        }

        return baseLetterSpacing + (worldLetterWidth * widthSpacingMultiplier);
    }

    private void UpdateLetterPosition(
        Transform letterTransform,
        int index,
        float trailOffset,
        Transform cameraTransform)
    {
        float horizontalOffset = firstLetterOffset.x + trailOffset + (letterOffsetStep.x * index);
        float verticalOffset = firstLetterOffset.y + (letterOffsetStep.y * index);
        float depthOffset = firstLetterOffset.z + (letterOffsetStep.z * index);

        Vector3 projectedTrailDirection = Vector3.ProjectOnPlane(-transform.forward, cameraTransform.forward);
        if (projectedTrailDirection.sqrMagnitude <= 0.0001f)
        {
            projectedTrailDirection = cameraTransform.right;
        }

        projectedTrailDirection.Normalize();

        if (Vector3.Dot(projectedTrailDirection, cameraTransform.right) < 0f)
        {
            projectedTrailDirection = -projectedTrailDirection;
        }

        Vector3 projectedVerticalDirection = Vector3.ProjectOnPlane(cameraTransform.up, cameraTransform.forward);
        if (projectedVerticalDirection.sqrMagnitude <= 0.0001f)
        {
            projectedVerticalDirection = cameraTransform.up;
        }

        projectedVerticalDirection.Normalize();

        Vector3 worldOffset =
            (projectedTrailDirection * horizontalOffset) +
            (projectedVerticalDirection * verticalOffset) -
            (transform.forward * depthOffset);

        letterTransform.position = transform.position + worldOffset;
    }

    private void FaceCamera(Transform letterTransform, Transform cameraTransform)
    {
        letterTransform.rotation = Quaternion.LookRotation(cameraTransform.forward, cameraTransform.up);
    }

    private void ClearLetters()
    {
        for (int index = 0; index < activeLetters.Count; index++)
        {
            Transform letter = activeLetters[index];
            if (letter != null)
            {
                Destroy(letter.gameObject);
            }
        }

        activeLetters.Clear();
        activeLetterIndices.Clear();
        activeLetterTrailOffsets.Clear();
    }

    public string GetWord()
    {
        return currentWord;
    }
}
