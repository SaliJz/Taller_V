using UnityEngine;
using System.Collections;

public class AsyncMusicController : MonoBehaviour
{
    [Header("Fuentes de Audio")]
    public AudioSource calmSource;
    public AudioSource battleSource;

    [Header("Configuración")]
    [Range(0.1f, 3f)]
    public float fadeDuration = 1.5f;

    private Coroutine currentTransition;
    private bool isInBattle = false;

    private void Start()
    {
        calmSource.loop = true;
        battleSource.loop = true;

        calmSource.volume = 1f;
        battleSource.volume = 0f;

        calmSource.Play();
    }

    public void SetBattleState(bool battleState)
    {
        if (calmSource == null || battleSource == null) return;

        if (isInBattle == battleState) return;

        isInBattle = battleState;

        if (currentTransition != null) StopCoroutine(currentTransition);
        currentTransition = StartCoroutine(CrossfadeMusic(battleState));
    }

    private IEnumerator CrossfadeMusic(bool toBattle)
    {
        float timer = 0f;

        AudioSource sourceIn = toBattle ? battleSource : calmSource;
        AudioSource sourceOut = toBattle ? calmSource : battleSource;

        if (!sourceIn.isPlaying) sourceIn.Play();

        float startVolIn = sourceIn.volume;
        float startVolOut = sourceOut.volume;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float t = timer / fadeDuration;

            sourceIn.volume = Mathf.Lerp(startVolIn, 1f, t);
            sourceOut.volume = Mathf.Lerp(startVolOut, 0f, t);

            yield return null;
        }

        sourceIn.volume = 1f;
        sourceOut.volume = 0f;

        sourceOut.Stop();
    }
}