using UnityEngine;

public class MerchantInteractionTrigger : MonoBehaviour
{
    public GameObject pactInteractionPromptPrefab;
    private GameObject currentInteractionPrompt;

    private MerchantRoomManager manager;
    private Sala6MerchantFlow flowManager;

    private void Start()
    {
        manager = FindAnyObjectByType<MerchantRoomManager>();
        if (manager == null)
        {
            Debug.LogError("MerchantRoomManager no encontrado. El trigger del mercader no funcionará.");
        }
        flowManager = FindAnyObjectByType<Sala6MerchantFlow>();
        if (flowManager == null)
        {
            Debug.LogWarning("Sala6MerchantFlow no encontrado. Usando lógica de mercader de rutina.");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (manager != null && other.CompareTag("Player"))
        {
            manager.OnPlayerEnterMerchantTrigger(transform);
            if (flowManager != null)
            {
                flowManager.HandleTriggerEnter();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (manager != null && other.CompareTag("Player"))
        {
            manager.OnPlayerExitMerchantTrigger();
        }
    }

    public void ShowPactPrompt()
    {
        if (manager != null && manager.dialoguePanel.activeSelf && pactInteractionPromptPrefab != null && currentInteractionPrompt == null)
        {
            Vector3 promptPosition = transform.position + Vector3.up * 2f;
            currentInteractionPrompt = Instantiate(pactInteractionPromptPrefab, promptPosition, Quaternion.identity, transform);
        }
    }

    public void HidePactPrompt()
    {
        if (currentInteractionPrompt != null)
        {
            Destroy(currentInteractionPrompt);
            currentInteractionPrompt = null;
        }
    }
}