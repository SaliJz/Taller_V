using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MorlockProjectileWordTrail : MonoBehaviour
{
    [Header("Letter Visuals")]
    [SerializeField] private GameObject letterPrefab;
    [SerializeField] private float revealDelay = 0.05f;
    [SerializeField] private Vector3 firstLetterLocalOffset = new Vector3(0f, 0.2f, 0f);
    [SerializeField] private Vector3 letterLocalStep = new Vector3(-0.08f, 0.08f, -0.24f);

    [Header("Billboard")]
    [SerializeField] private bool alwaysFaceMainCamera = true;

    private readonly List<Transform> activeLetters = new List<Transform>();
    private readonly List<int> activeLetterIndices = new List<int>();

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

            UpdateLetterPosition(letter, activeLetterIndices[index], targetCamera.transform);
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
            Debug.LogWarning($"[{nameof(MorlockProjectileWordTrail)}] No hay letterPrefab asignado en {name}.", this);
            yield break;
        }

        for (int index = 0; index < currentWord.Length; index++)
        {
            char currentCharacter = currentWord[index];

            if (!char.IsWhiteSpace(currentCharacter))
            {
                GameObject letterObject = Instantiate(letterPrefab, transform);
                letterObject.name = $"Letter_{index}_{currentCharacter}";

                Transform letterTransform = letterObject.transform;

                TMP_Text textComponent = letterObject.GetComponentInChildren<TMP_Text>(true);
                if (textComponent != null)
                {
                    textComponent.text = currentCharacter.ToString();
                }

                activeLetters.Add(letterTransform);

                if (targetCamera == null)
                {
                    targetCamera = Camera.main;
                }

                if (targetCamera != null)
                {
                    UpdateLetterPosition(letterTransform, index, targetCamera.transform);
                    FaceCamera(letterTransform, targetCamera.transform);
                }

                activeLetterIndices.Add(index);
            }

            if (revealDelay > 0f)
            {
                yield return new WaitForSeconds(revealDelay);
            }
            else
            {
                yield return null;
            }
        }

        spawnRoutine = null;
    }

    private void UpdateLetterPosition(Transform letterTransform, int index, Transform cameraTransform)
    {
        float horizontalOffset = firstLetterLocalOffset.x + (letterLocalStep.x * index);
        float verticalOffset = firstLetterLocalOffset.y + (letterLocalStep.y * index);
        float depthOffset = firstLetterLocalOffset.z + (letterLocalStep.z * index);

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
    }
}
