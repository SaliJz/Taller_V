using UnityEngine;

/// <summary>
/// Gestiona la detección y la interacción del jugador con objetos del entorno.
/// </summary>
public class PlayerInteractionController : MonoBehaviour
{
    [Header("Configuración de Interacción")]
    [Tooltip("Rango de detección de objetos interactuables.")]
    [SerializeField] private float interactionRange = 2.5f;

    [Tooltip("Transform que define la posición donde se sostendrá el objeto recogido.")]
    [SerializeField] private Transform holdPoint;

    [Tooltip("LayerMask para filtrar qué objetos son considerados interactuables.")]
    [SerializeField] private LayerMask interactableLayer;

    // Referencia al objeto interactuable actualmente en rango.
    private IInteractable currentInteractable;
    // Referencia al objeto que se está sosteniendo.
    private GameObject heldObject;

    private void Update()
    {
        DetectInteractable();
        HandleInteractionInput();
    }

    /// <summary>
    /// Lanza un rayo para detectar objetos interactuables frente al jugador.
    /// </summary>
    private void DetectInteractable()
    {
        RaycastHit hit;
        if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, interactionRange, interactableLayer))
        {
            IInteractable interactable = hit.collider.GetComponent<IInteractable>();
            PickableObject pickable = hit.collider.GetComponent<PickableObject>();

            // Si el objeto es recogible y ya está siendo sostenido, no se puede interactuar con él.
            if (pickable != null && pickable.GetIsHeld())
            {
                ClearCurrentInteractable();
                return;
            }

            // Si se detecta un nuevo interactuable, actualizar la referencia y mostrar el indicador.
            if (interactable != null)
            {
                if (interactable != currentInteractable)
                {
                    ClearCurrentInteractable();
                    currentInteractable = interactable;
                    currentInteractable.ShowIndicator();
                }
                return;
            }
        }

        ClearCurrentInteractable();
    }

    /// <summary>
    /// Gestiona las entradas del teclado para interactuar o soltar objetos.
    /// </summary>
    private void HandleInteractionInput()
    {
        // Permite interactuar con el objeto actual al presionar la tecla E, si no se sostiene otro objeto.
        if (Input.GetKeyDown(KeyCode.E) && currentInteractable != null && heldObject == null)
        {
            currentInteractable.Interact(this);
        }

        // Permite soltar el objeto sostenido al presionar la tecla Q.
        if (Input.GetKeyDown(KeyCode.Q) && heldObject != null)
        {
            IInteractable heldInteractable = heldObject.GetComponent<IInteractable>();
            if (heldInteractable != null)
            {
                heldInteractable.Release(this);
            }
        }
    }

    /// <summary>
    /// Limpia la referencia al interactuable actual y oculta su indicador.
    /// </summary>
    private void ClearCurrentInteractable()
    {
        if (currentInteractable != null)
        {
            currentInteractable.HideIndicator();
            currentInteractable = null;
        }
    }

    /// <summary>
    /// Dibuja el rayo de interacción.
    /// <summary>
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawRay(Camera.main.transform.position, Camera.main.transform.forward * interactionRange);
    }

    // Métodos públicos para ser accedidos por los objetos interactuables.
    public Transform GetHoldPoint() => holdPoint;
    public void AssignHeldObject(GameObject obj) => heldObject = obj;
}