using System.Linq;
using UnityEngine;

public class RoomDistortionHandler : MonoBehaviour
{
    [SerializeField] private PhysicsMaterial slipperyMaterial;

    private Renderer[] roomFloorRenderers;

    private void Start()
    {
        roomFloorRenderers = GetComponentsInChildren<Renderer>().Where(r => r.tag == "Floor").ToArray();
    }

    public void ApplyFloorOfTheDamned()
    {
        if (slipperyMaterial == null)
        {
            Debug.LogError("[Distorsi�n] slipperyMaterial no asignado para FloorOfTheDamned.");
            return;
        }

        foreach (var renderer in roomFloorRenderers)
        {
            if (renderer.TryGetComponent<Collider>(out var collider))
            {
                collider.sharedMaterial = slipperyMaterial;
            }
        }
        Debug.Log("[Distorsi�n] Suelo maldito aplicado: Fricci�n baja.");
    }

    public void ClearFloorOfTheDamned()
    {
        // restaurar el material de f�sica original
    }
}