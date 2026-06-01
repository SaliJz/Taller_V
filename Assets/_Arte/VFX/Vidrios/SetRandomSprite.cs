using UnityEngine;

public class SetRandomSprite : MonoBehaviour
{
    [SerializeField] Sprite[] sprites;
    SpriteRenderer targetRenderer;

    void Start()
    {
        targetRenderer = GetComponent<SpriteRenderer>();

        int randomNum = Random.Range(0, sprites.Length);
        targetRenderer.sprite = sprites[randomNum];
    }
}
