using UnityEngine;

/// <summary>
/// Implementa la l�gica para un objeto que puede ser recogido y soltado por el jugador.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PickableObject : MonoBehaviour, IInteractable
{
    [Tooltip("Referencia al objeto visual que se muestra para indicar la interacci�n.")]
    [SerializeField] private GameObject interactionIndicator;

    private Rigidbody rb;
    private bool isHeld = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (interactionIndicator != null)
        {
            interactionIndicator.SetActive(false);
        }
    }

    public void ShowIndicator()
    {
        if (interactionIndicator != null)
        {
            interactionIndicator.SetActive(true);
        }
    }

    public void HideIndicator()
    {
        if (interactionIndicator != null)
        {
            interactionIndicator.SetActive(false);
        }
    }

    /// <summary>
    /// Maneja la l�gica para que el jugador recoja el objeto.
    /// </summary>
    public void Interact(PlayerInteractionController player)
    {
        if (isHeld) return;

        // Se asigna el punto de sujeci�n definido en el jugador.
        Transform holdPoint = player.GetHoldPoint();
        transform.SetParent(holdPoint);
        transform.position = holdPoint.position;
        transform.rotation = holdPoint.rotation;

        if (rb != null)
        {
            rb.isKinematic = true;
        }

        isHeld = true;
        player.AssignHeldObject(this.gameObject);
        HideIndicator();
    }

    /// <summary>
    /// Maneja la l�gica para que el jugador suelte el objeto.
    /// </summary>
    public void Release(PlayerInteractionController player)
    {
        if (!isHeld) return;

        transform.SetParent(null); // Se desvincula del jugador.

        if (rb != null)
        {
            rb.isKinematic = false;
            // Aplica una peque�a fuerza hacia adelante al soltar.
            rb.AddForce(player.transform.forward * 2f, ForceMode.Impulse);
        }

        isHeld = false;
        player.AssignHeldObject(null);
    }

    // M�todos p�blicos para ser accedidos por los objetos interactuables.
    public bool GetIsHeld() => isHeld;
}