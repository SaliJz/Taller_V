using UnityEngine;

/// <summary>
/// Define el contrato para todos los objetos que pueden ser interactuados por el jugador.
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// Muestra un indicador visual para señalar que el objeto es interactuable.
    /// </summary>
    public void ShowIndicator();

    /// <summary>
    /// Oculta el indicador visual.
    /// </summary>
    public void HideIndicator();

    /// <summary>
    /// Ejecuta la acción principal de interacción (ej. recoger el objeto).
    /// </summary>
    /// <param name="player">Referencia al controlador del jugador que interactúa.</param>
    public void Interact(PlayerInteractionController player);

    /// <summary>
    /// Ejecuta la acción para liberar o soltar el objeto.
    /// </summary>
    /// <param name="player">Referencia al controlador del jugador que libera el objeto.</param>
    public void Release(PlayerInteractionController player);
}