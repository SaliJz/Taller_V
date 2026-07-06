using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

public class DeathSequenceCtrl : FullScreenEffectsBase
{
    [SerializeField] PlayableDirector deathDirector;
    [SerializeField] GameObject playerGFX;

    private static readonly string intensidadProp = "_Intensidad";

    public void StartSequence()
    {
        deathDirector.Play();
        StartCoroutine(BlackFade());

        PlayerAnimCtrl anim = playerGFX.GetComponent<PlayerAnimCtrl>();
        anim.PlayDeath();
    }

    private IEnumerator BlackFade(float duration = 0.2f)
    {
        float elapsed = 0f;
        Time.timeScale = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed/duration;
            SetFloat(intensidadProp, Mathf.Lerp(0, 1, t));

            yield return null;
        }

        SetFloat(intensidadProp, 1);
    }

    #if UNITY_EDITOR
    void Update()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "AndreiNew")
        {
            testInputs();
        }
    }

    private void testInputs()
    {
        if (Input.GetKeyDown(KeyCode.F2)) 
        {
            playerGFX.SetActive(true);
            StartSequence();
        }
    }
    #endif
}
