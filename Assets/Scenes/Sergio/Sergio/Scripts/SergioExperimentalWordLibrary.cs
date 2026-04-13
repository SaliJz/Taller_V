using System.Collections.Generic;
using UnityEngine;

public class SergioExperimentalWordLibrary : MonoBehaviour
{
    [Header("Palabras disponibles")]
    [SerializeField] private List<string> words = new List<string>
    {
        "STATIC",
        "MORLOCK",
        "ERROR",
        "GLITCH",
        "EXAMPLE"
    };

    [SerializeField] private string fallbackWord = "STATIC";

    public string GetRandomWord()
    {
        if (TryGetRandomWord(out string selectedWord))
        {
            return selectedWord;
        }

        return "STATIC";
    }

    public bool TryGetRandomWord(out string selectedWord)
    {
        selectedWord = SanitizeWord(fallbackWord);

        if (words == null || words.Count == 0)
        {
            return !string.IsNullOrEmpty(selectedWord);
        }

        int startIndex = Random.Range(0, words.Count);
        for (int i = 0; i < words.Count; i++)
        {
            int currentIndex = (startIndex + i) % words.Count;
            string sanitizedWord = SanitizeWord(words[currentIndex]);
            if (!string.IsNullOrEmpty(sanitizedWord))
            {
                selectedWord = sanitizedWord;
                return true;
            }
        }

        return !string.IsNullOrEmpty(selectedWord);
    }

    private static string SanitizeWord(string rawWord)
    {
        if (string.IsNullOrWhiteSpace(rawWord))
        {
            return string.Empty;
        }

        return rawWord.Trim().ToUpperInvariant();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        fallbackWord = SanitizeWord(fallbackWord);

        if (words == null)
        {
            return;
        }

        for (int i = 0; i < words.Count; i++)
        {
            words[i] = SanitizeWord(words[i]);
        }
    }
#endif
}
