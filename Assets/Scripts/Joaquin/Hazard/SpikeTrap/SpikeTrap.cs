using System.Collections.Generic;
using UnityEngine;
using System.Collections;

/// <summary> 
/// Trampa de pinchos que avisa, ataca y entra en enfriamiento antes de rearmarse.
/// </summary>
public class SpikeTrap : MonoBehaviour
{
    private enum TrapState { Armed, Priming, Active, Cooldown }
    [SerializeField, Tooltip("Estado actual de la trampa (solo lectura en inspector).")]
    private TrapState currentState = TrapState.Armed;

    [Header("Parámetros")]
    [SerializeField] private float damage = 20f;
    [SerializeField, Tooltip("Tiempo entre el aviso y el ataque.")]
    private float primingDuration = 0.7f;
    [SerializeField, Tooltip("Tiempo que los pinchos permanecen arriba.")]
    private float activeDuration = 1.5f;
    [SerializeField, Tooltip("Tiempo de recarga de la trampa.")]
    private float cooldownDuration = 3f;

    [Header("Placa de presión")]
    [SerializeField, Tooltip("Transform de la base/plataforma que baja hacia el jugador.")]
    private Transform plateTransform;
    [SerializeField, Tooltip("posición local Y cuando la placa está levantada.")]
    private float plateRaisedLocalY = 1f;
    [SerializeField, Tooltip("posición local Y cuando la placa queda presionada.")]
    private float platePressedLocalY = 0.25f;
    [SerializeField, Tooltip("duración del movimiento de la placa al bajar o subir.")]
    private float plateMoveDuration = 0.25f;

    [Header("VFX")]
    [SerializeField, Tooltip("particle system que se reproduce durante el priming. Debe ser hijo de la placa.")]
    private ParticleSystem primingDustVFX;
    [SerializeField, Tooltip("particle system tipo burst que se reproduce en la activación.")]
    private ParticleSystem activateDustVFX;

    [Header("Referencias")]
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip triggerSound;   
    [SerializeField] private AudioClip activateSound;
    [SerializeField] private AudioClip desactivateSound;
    [SerializeField] private AudioClip armedSound;

    [Header("Debug HUD")]
    [SerializeField] private bool showDebugHUD = true;

    private readonly List<PlayerHealth> playerInRange = new List<PlayerHealth>();

    private Coroutine trapRoutine;
    private Coroutine plateMoveRoutine;

    private void OnDisable()
    {
        StopPlateRoutineIfAny();
        StopPrimingVFX();
        StopActivateVFX();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        PlayerHealth player = other.GetComponent<PlayerHealth>();
        if (player != null && !playerInRange.Contains(player))
        {
            playerInRange.Add(player);
        }

        if (currentState == TrapState.Armed)
        {
            if (trapRoutine == null) trapRoutine = StartCoroutine(ActivateTrapSequence());
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        PlayerHealth player = other.GetComponent<PlayerHealth>();
        if (player != null)
        {
            playerInRange.Remove(player);
        }
    }

    private IEnumerator ActivateTrapSequence()
    {
        //1. Aviso (Priming)
        SetState(TrapState.Priming);
        PlaySound(triggerSound);

        // iniciar movimiento de placa y VFX de priming
        StartPlateLower();
        StartPrimingVFX();

        yield return new WaitForSecondsRealtime(primingDuration);

        // 2. Ataque (Active)
        StopPrimingVFX();
        SetState(TrapState.Active);
        TriggerAnimation("Activate");
        PlaySound(activateSound);
        PlayActivateVFX();

        for (int i = playerInRange.Count - 1; i >= 0; i--)
        {
            PlayerHealth player = playerInRange[i];
            if (player != null && player.isActiveAndEnabled)
            {
                player.TakeDamage(damage);
            }
            else
            {
                playerInRange.RemoveAt(i); // Limpieza segura
            }
        }

        yield return new WaitForSecondsRealtime(activeDuration);

        // 3. Enfriamiento (Cooldown)
        SetState(TrapState.Cooldown);
        TriggerAnimation("Deactivate");
        PlaySound(desactivateSound);

        yield return new WaitForSecondsRealtime(cooldownDuration);

        // 4. Rearmado (Armed)
        SetState(TrapState.Armed);
        PlaySound(armedSound);
        trapRoutine = null;
        StartPlateRaise();
        StopPrimingVFX();
        StopActivateVFX();

        if (playerInRange.Count > 0)
        {
            trapRoutine = StartCoroutine(ActivateTrapSequence());
        }
    }

    private void SetState(TrapState newState)
    {
        currentState = newState;
        Debug.Log($"[SpikeTrap] Estado -> {currentState}", this);
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    private void TriggerAnimation(string trigger)
    {
        if (animator != null)
        {
            animator.SetTrigger(trigger);
        }
    }

    private void StartPlateLower()
    {
        StopPlateRoutineIfAny();
        if (plateTransform != null)
        {
            plateMoveRoutine = StartCoroutine(MovePlateLocalY(plateTransform, plateRaisedLocalY, platePressedLocalY, plateMoveDuration));
        }
    }

    private void StartPlateRaise()
    {
        StopPlateRoutineIfAny();
        if (plateTransform != null)
        {
            float raiseDuration = Mathf.Max(0.08f, plateMoveDuration * 0.45f);
            plateMoveRoutine = StartCoroutine(MovePlateLocalY(plateTransform, platePressedLocalY, plateRaisedLocalY, raiseDuration));
        }
    }

    private void StopPlateRoutineIfAny()
    {
        if (plateMoveRoutine != null)
        {
            StopCoroutine(plateMoveRoutine);
            plateMoveRoutine = null;
        }
    }

    private IEnumerator MovePlateLocalY(Transform t, float fromY, float toY, float duration)
    {
        if (t == null)
            yield break;

        Vector3 start = t.localPosition;
        float startTime = Time.realtimeSinceStartup;
        float endTime = startTime + Mathf.Max(0.0001f, duration);

        while (Time.realtimeSinceStartup < endTime)
        {
            float elapsed = Time.realtimeSinceStartup - startTime;
            float tNorm = Mathf.Clamp01(elapsed / duration);
            // optional easing
            float eased = Mathf.SmoothStep(0f, 1f, tNorm);
            float y = Mathf.Lerp(fromY, toY, eased);
            t.localPosition = new Vector3(start.x, y, start.z);
            yield return null;
        }

        t.localPosition = new Vector3(start.x, toY, start.z);
        plateMoveRoutine = null;
    }

    private void StartPrimingVFX()
    {
        if (primingDustVFX != null)
        {
            primingDustVFX.transform.position = plateTransform != null ? plateTransform.position : transform.position;
            if (!primingDustVFX.isPlaying) primingDustVFX.Play();
        }
    }

    private void StopPrimingVFX()
    {
        if (primingDustVFX != null && primingDustVFX.isPlaying)
        {
            primingDustVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void PlayActivateVFX()
    {
        if (activateDustVFX != null)
        {
            activateDustVFX.transform.position = plateTransform != null ? plateTransform.position : transform.position;
            activateDustVFX.Play();
        }
    }

    private void StopActivateVFX()
    {
        if (activateDustVFX != null && activateDustVFX.isPlaying)
        {
            activateDustVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void OnDrawGizmos()
    {
        Color color = Color.gray;
        switch (currentState)
        {
            case TrapState.Armed: color = Color.green; break;
            case TrapState.Priming: color = Color.yellow; break;
            case TrapState.Active: color = Color.red; break;
            case TrapState.Cooldown: color = Color.cyan; break;
        }
        Gizmos.color = color;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, new Vector3(1, 0.5f, 1));
    }

    private void OnGUI()
    {
        if (!showDebugHUD) return;

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 12,
            alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(10, 10, 10, 10)
        };

        Rect rect = new Rect(10, 10, 250, 150);
        GUILayout.BeginArea(rect, boxStyle);
        GUILayout.Label("<b>SpikeTrap Debug</b>", new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold
        });
        GUILayout.Space(5);
        GUILayout.Label($"Estado: {currentState}");
        GUILayout.Label($"Jugador en rango: {playerInRange.Count}");
        GUILayout.Label($"Rutina activa: {(trapRoutine != null ? "Sí" : "No")}");
        GUILayout.EndArea();
    }
}