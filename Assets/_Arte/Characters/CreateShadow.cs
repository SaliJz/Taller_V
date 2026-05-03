using UnityEngine;

public class CreateShadow : MonoBehaviour
{
    [SerializeField] GameObject shadowPrefab;
    [SerializeField] LayerMask GroundLayer;
    public float startYoffset = 5f;
    public float spawnYoffset = 0.1f;
    public float shadowScale;

    void Start()
    {
        SpawnShadow();
    }

    void SpawnShadow()
    {
        RaycastHit hit;

        Vector3 Offset = new Vector3 (transform.position.x, transform.position.y + startYoffset, transform.position.z);

        if (Physics.Raycast(Offset, Vector3.down, out hit, 50f, GroundLayer))
        {
            Vector3 spawnPos = hit.point + new Vector3(0, spawnYoffset, 0);
            Quaternion spawnRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            Vector3 spawnScale = new Vector3(shadowScale, 1, shadowScale);

            GameObject shadowInstance = Instantiate(shadowPrefab, spawnPos, spawnRotation);
            shadowInstance.transform.localScale = Vector3.one * shadowScale;
            shadowInstance.transform.SetParent(this.transform);
            Debug.Log("Sombra spawneada");
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] No se encontró suelo para spawnear la sombra.");
        }
    }
}
