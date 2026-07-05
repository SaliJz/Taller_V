using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CharacterController), typeof(PlayerMovement))]
public class PlayerKnockbackReceiver : MonoBehaviour
{
    private CharacterController cc;
    private PlayerMovement playerMovement;
    private Coroutine activeKnockback;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        playerMovement = GetComponent<PlayerMovement>();
    }

    public void ApplyKnockback(Vector3 direction, float force, float duration = 0.25f)
    {
        if (playerMovement != null && playerMovement.IsDashing) return;

        if (activeKnockback != null) StopCoroutine(activeKnockback);

        activeKnockback = StartCoroutine(KnockbackRoutine(direction.normalized * force, duration));
    }

    private IEnumerator KnockbackRoutine(Vector3 velocity, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            // Valida por si el jugador tira un dash a mitad del empuje
            if (playerMovement != null && playerMovement.IsDashing) break;

            if (cc != null && cc.enabled)
            {
                cc.Move(velocity * Time.deltaTime);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }
}