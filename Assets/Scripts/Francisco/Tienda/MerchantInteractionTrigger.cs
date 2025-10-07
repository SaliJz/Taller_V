using UnityEngine;

public class MerchantInteractionTrigger : MonoBehaviour
{
    public GameObject pactInteractionPromptPrefab;
    private GameObject currentInteractionPrompt;

    private MerchantRoomManager manager;

    private void Start()
    {
        manager = FindAnyObjectByType<MerchantRoomManager>();
        if (manager == null)
        {
            Debug.LogError("MerchantRoomManager no encontrado. El trigger del mercader no funcionará.");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (manager != null && other.CompareTag("Player"))
        {
            manager.OnPlayerEnterMerchantTrigger(transform);
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