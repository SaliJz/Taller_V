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
        yield return new WaitForSecondsRealtime(primingDuration);

        // 2. Ataque (Active)
        SetState(TrapState.Active);
        TriggerAnimation("Activate");
        PlaySound(activateSound);

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