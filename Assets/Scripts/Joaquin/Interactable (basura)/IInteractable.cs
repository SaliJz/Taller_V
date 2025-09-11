using UnityEngine;

/// <summary>
/// Define el contrato para todos los objetos que pueden ser interactuados por el jugador.
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// Muestra un indicador visual para se�alar que el objeto es interactuable.
    /// </summary>
    public void ShowIndicator();

    /// <summary>
    /// Oculta el indicador visual.
    /// </summary>
    public void HideIndicator();

    /// <summary>
    /// Ejecuta la acci�n principal de interacci�n (ej. recoger el objeto).
    /// </summary>
    /// <param name="player">Referencia al controlador del jugador que interact�a.</param>
    public void Interact(PlayerInteractionController player);

    /// <summary>
    /// Ejecuta la acci�n para liberar o soltar el objeto.
    /// </summary>
    /// <param name="player">Referencia al controlador del jugador que libera el objeto.</param>
    public void Release(PlayerInteractionController player);
}