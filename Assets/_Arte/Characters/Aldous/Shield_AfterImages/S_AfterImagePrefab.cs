using UnityEngine;

public class S_AfterImagePrefab : MonoBehaviour
{
    [Header("General Settings")]
    [SerializeField] float lifetime;
    [SerializeField] bool stepUpdate = false;

    SpriteRenderer r;
    Color startColor;
    Color endColor;
    float timer;

    [Header("Growth Settings")]
    [SerializeField] bool growOverLifetime = false;
    [SerializeField] float growthScale = 2f;
    [SerializeField] AnimationCurve growthCurve;

    Vector3 originalScale;

    public void Initialize(Color start, Color end, float lifetime)
    {
        r = GetComponent<SpriteRenderer>();
        startColor = start;
        endColor = end;
        r.color = startColor;
        this.lifetime = lifetime;

        originalScale =  transform.localScale;
    }

    void Update()
    {
        timer += Time.deltaTime;

        float t = timer/lifetime;

        if(stepUpdate)
        {
            int steps = 4;
            t = Mathf.Floor(t * steps) / steps;
        }

        if(r.material == r.sharedMaterial)
        {
            r.material = new Material(r.sharedMaterial);
        }

        Color lerpedColor = Color.Lerp(startColor, endColor, t);
        r.material.SetColor("_Color", lerpedColor);


        if (growOverLifetime) UpdateGrowth(t);

        if(timer >= lifetime) Destroy(gameObject);
    }

    void UpdateGrowth(float t)
    {
        float curveT = growthCurve.Evaluate(t);

        Vector3 targetScale = originalScale * growthScale;
        transform.localScale = Vector3.Lerp(originalScale, targetScale, curveT);
    }

}
