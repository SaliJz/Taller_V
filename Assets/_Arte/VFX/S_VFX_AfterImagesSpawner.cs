using UnityEngine;
public class S_VFX_AfterImagesSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] SpriteRenderer sourceRenderer;

    [Header("Spawn Settings")]   
    [SerializeField] GameObject afterImagePrefab;
    [SerializeField] float spawnInterval = 0.05f;
    [SerializeField] float lifetime = 0.2f;

    [Header("Color")]
    [SerializeField] Color startColor = new Color(1f, 0.8f, 0.2f, 0.7f);
    [SerializeField] Color endColor = new Color(1f, 0.2f, 0.8f, 0.3f);

    float timer;

    void Update()
    {
        timer += Time.deltaTime;

        if(timer >= spawnInterval)
        {
            SpawnAfterImage();
            timer = 0;
        }
    }

    void SpawnAfterImage()
    {
        
        // GameObject obj = Instantiate(afterImagePrefab, transform.position, transform.rotation);
        GameObject obj = Instantiate(afterImagePrefab);
        obj.name = "AfterImage";
        obj.transform.position = transform.position;
        obj.transform.rotation = transform.rotation;
        obj.transform.localScale = transform.localScale;

        SpriteRenderer r = obj.GetComponent<SpriteRenderer>();

        r.sprite = sourceRenderer.sprite;
        r.flipX = sourceRenderer.flipX;
        r.flipY = sourceRenderer.flipY;
        r.sortingLayerID = sourceRenderer.sortingLayerID;
        r.sortingOrder = sourceRenderer.sortingOrder - 1;

        S_AfterImagePrefab afterImage = obj.GetComponent<S_AfterImagePrefab>();
        afterImage.Initialize(startColor, endColor, lifetime);
    }
}