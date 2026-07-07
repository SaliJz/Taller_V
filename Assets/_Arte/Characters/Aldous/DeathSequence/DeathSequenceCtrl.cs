using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Playables;

public class DeathSequenceCtrl : FullScreenEffectsBase
{
    [Header("Director Settings")]
    [SerializeField] public PlayableDirector deathDirector;
    [SerializeField] private GameObject playerGFX;

    [Header("Occlusion Boxcast")]
    [SerializeField] private Camera mainCam;
    [SerializeField] private LayerMask occlusionMask;
    [SerializeField] private Vector3 boxHalfExtents = new Vector3(5f, 3f, 0.5f);
    [SerializeField] private Vector3 nearbyHalfExtents = new Vector3(3f, 2f, 3f);
    [SerializeField] private List<GameObject> hiddenObjects = new();


    private Action onSequenceFinished;

    private static readonly string intensidadProp = "_Intensidad";

    void Awake()
    {
        mainCam = Camera.main;
    }

    public void StartSequence(Action onFinished = null)
    {
        HideOccludingObjects();
        HideNearbyObjects();

        onSequenceFinished = onFinished;
        deathDirector.stopped += onDeathSequenceFinished;

        PlayerAnimCtrl anim = playerGFX.GetComponent<PlayerAnimCtrl>();
        anim.PlayDeath();
        anim.enabled = false;
        
        deathDirector.Play();
        StartCoroutine(BlackFade());

        PlayerShaderCtrl shader = playerGFX.GetComponent<PlayerShaderCtrl>();
        shader.ResetAllEffects();
    }

    private void onDeathSequenceFinished(PlayableDirector director)
    {
        deathDirector.stopped -= onDeathSequenceFinished;
        Time.timeScale = 1;
        onSequenceFinished?.Invoke();
        onSequenceFinished = null;
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

    private void HideOccludingObjects()
    {
        Vector3 camPos = mainCam.transform.position;
        Vector3 playerPos = playerGFX.transform.position;

        Vector3 direction = (playerPos - camPos).normalized;
        float distance = Vector3.Distance(camPos, playerPos);

        RaycastHit[] hits = Physics.BoxCastAll(
            camPos,
            boxHalfExtents,
            direction,
            mainCam.transform.rotation,
            distance,
            occlusionMask
        );

        foreach(var hit in hits)
        {
            GameObject obj = hit.collider.gameObject;
            if (obj == playerGFX) continue;

            obj.SetActive(false);
            hiddenObjects.Add(obj);
        }
    }

    private void HideNearbyObjects()
    {
        Collider[] hits = Physics.OverlapBox(
            playerGFX.transform.position,
            nearbyHalfExtents,
            playerGFX.transform.rotation,
            occlusionMask
        );

        foreach(Collider hit in hits)
        {
            GameObject obj = hit.gameObject;
            if (obj == playerGFX) continue;
            if (hiddenObjects.Contains(obj)) continue;

            obj.SetActive(false);
            hiddenObjects.Add(obj);
        }
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

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawCube(playerGFX.transform.position, nearbyHalfExtents);
    }

    #endif
}
