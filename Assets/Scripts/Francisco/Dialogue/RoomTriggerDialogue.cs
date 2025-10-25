using UnityEngine;
using System.Collections;

public class RoomTriggerDialogue : MonoBehaviour
{
    public DialogLine[] Sala1_IntroDialog;

    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered || !other.CompareTag("Player"))
        {
            return;
        }

        hasTriggered = true;

        StartCoroutine(ExecuteIntroFlow());
    }

    private IEnumerator ExecuteIntroFlow()
    {
        if (DialogManager.Instance == null)
        {
            Debug.LogError("DialogManager no est� en la escena.");
            yield break;
        }

        if (FadeController.Instance != null)
        {
            yield return new WaitUntil(() => !FadeController.Instance.IsFading);
        }

        DialogManager.Instance.StartDialog(Sala1_IntroDialog);

        while (DialogManager.Instance.IsActive)
        {
            yield return null;
        }

        Debug.Log("Di�logo de introducci�n terminado. El jugador puede moverse y el juego contin�a.");
    }
}