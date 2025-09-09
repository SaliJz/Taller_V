using System.Collections.Generic;
using UnityEngine;

public class ShieldPooler : MonoBehaviour
{
    public static ShieldPooler Instance;

    [Header("Pool Configuration")]
    [SerializeField] private GameObject shieldToPool;
    [SerializeField] private int amountToPool;

    private List<GameObject> pooledObjects;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        pooledObjects = new List<GameObject>();
        for (int i = 0; i < amountToPool; i++)
        {
            GameObject obj = Instantiate(shieldToPool);
            obj.SetActive(false);
            pooledObjects.Add(obj);
        }
    }

    public GameObject GetPooledObject()
    {
        foreach (GameObject obj in pooledObjects)
        {
            if (!obj.activeInHierarchy)
            {
                return obj;
            }
        }
        
        Debug.LogWarning("Pool de escudos agotado. Considera aumentar 'amountToPool'.");
        return null;
    }
}
