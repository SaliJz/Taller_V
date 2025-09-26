using UnityEngine;
using UnityEngine.Events;

public class InteractionEvents : MonoBehaviour
{
    [Header("Eventos")]
    [SerializeField] private UnityEvent OnObjectEntered;
    [SerializeField] private UnityEvent OnObjectExited;

    [SerializeField] private string tagName;

    private void OnTriggerEnter(Collider other)
    {
        if (tagName == "")
        {
            OnObjectEntered?.Invoke();
        }
        else
        {
            if (other.CompareTag(tagName)) OnObjectEntered?.Invoke();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (tagName == "")
        {
            OnObjectExited?.Invoke();
        }
        else
        {
            if (other.CompareTag(tagName)) OnObjectExited?.Invoke();
        }
    }
}