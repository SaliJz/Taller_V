using UnityEngine;
using System.Collections.Generic;

public class InstantiateInBoxController : MonoBehaviour
{
    [SerializeField] private List<GameObject> itemPrefabs = new List<GameObject>(); 
    [SerializeField] private KeyCode spawnKey = KeyCode.R;

    private BoxCollider spawnArea;
    private int currentItemIndex = 0; 

    private void Awake()
    {
        spawnArea = GetComponent<BoxCollider>();

        if (spawnArea == null)
        {
            Debug.LogError("Se requiere un BoxCollider en este GameObject para definir el área de spawn.");
            enabled = false;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(spawnKey))
        {
            InstantiateOneItem();
        }
    }

    private void InstantiateOneItem()
    {
        if (itemPrefabs == null || itemPrefabs.Count == 0)
        {
            Debug.LogError("La lista de Prefabs está vacía. No se puede instanciar el ítem.");
            return;
        }

        if (spawnArea == null)
        {
            Debug.LogError("BoxCollider no encontrado. No se puede instanciar el ítem.");
            return;
        }

        GameObject prefabToInstantiate = itemPrefabs[currentItemIndex];

        currentItemIndex = (currentItemIndex + 1) % itemPrefabs.Count;

        Bounds bounds = spawnArea.bounds;

        float randomX = Random.Range(bounds.min.x, bounds.max.x);
        float randomY = Random.Range(bounds.min.y, bounds.max.y);
        float randomZ = Random.Range(bounds.min.z, bounds.max.z);

        Vector3 randomPosition = new Vector3(randomX, randomY, randomZ);

        GameObject newItem = Instantiate(prefabToInstantiate, randomPosition, Quaternion.identity);

        newItem.transform.SetParent(transform);

        Debug.Log($"Ítem '{prefabToInstantiate.name}' instanciado en: {randomPosition}");
    }
}