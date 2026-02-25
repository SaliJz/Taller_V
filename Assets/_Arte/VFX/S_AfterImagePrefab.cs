using UnityEngine;

public class S_AfterImagePrefab : MonoBehaviour
{
    [SerializeField] float lifetime;

    SpriteRenderer r;
    Color startColor;
    Color endColor;
    float timer;

    public void Initialize(Color start, Color end, float lifetime)
    {
        r = GetComponent<SpriteRenderer>();
        startColor = start;
        endColor = end;
        r.color = startColor;
        this.lifetime = lifetime;
    }

    void Update()
    {
        timer += Time.deltaTime;

        float t = timer/lifetime;
        int steps = 4;
        t = Mathf.Floor(t * steps) / steps;

        if(r.material == r.sharedMaterial)
        {
            r.material = new Material(r.sharedMaterial);
        }

        Color lerpedColor = Color.Lerp(startColor, endColor, t);
        r.material.SetColor("_Color", lerpedColor);

        if(timer >= lifetime)
        {
            Destroy(gameObject);
        }
    }

}
