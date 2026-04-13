using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class SergioProjectileWordTrail : MonoBehaviour
{
    [SerializeField] private GameObject letterPrefab;
    [SerializeField] private float letterSpawnDelay = 0.15f; 
    [SerializeField] private bool faceMainCamera = true;

    private readonly List<Transform> spawnedLetters = new List<Transform>();
    private Camera cachedCamera;

    public void SetupWord(string word, Vector3 direction, float speed, float lifetime)
    {
        if (string.IsNullOrWhiteSpace(word)) return;
        StartCoroutine(FireLettersOneByOne(word, direction, speed, lifetime));
    }

    private IEnumerator FireLettersOneByOne(string word, Vector3 direction, float speed, float lifetime)
    {
        if (letterPrefab == null) yield break;

        for (int i = 0; i < word.Length; i++)
        {
            if (this == null) yield break;

            GameObject letterObj = Instantiate(letterPrefab, transform.position, Quaternion.identity);
            
            TMP_Text textComponent = letterObj.GetComponentInChildren<TMP_Text>(true);
            if (textComponent != null) textComponent.text = word[i].ToString();

            spawnedLetters.Add(letterObj.transform);

            SergioIndependentMovement moveScript = letterObj.GetComponent<SergioIndependentMovement>();
            if(moveScript == null) moveScript = letterObj.AddComponent<SergioIndependentMovement>();
            
            moveScript.Setup(direction, speed, lifetime);

            yield return new WaitForSeconds(letterSpawnDelay);
        }
    }

    private void LateUpdate()
    {
        if (!faceMainCamera) return;
        if (cachedCamera == null) cachedCamera = Camera.main;
        if (cachedCamera == null) return;

        for (int i = spawnedLetters.Count - 1; i >= 0; i--)
        {
            if (spawnedLetters[i] == null)
            {
                spawnedLetters.RemoveAt(i);
                continue;
            }
            spawnedLetters[i].LookAt(spawnedLetters[i].position + cachedCamera.transform.forward);
        }
    }
}