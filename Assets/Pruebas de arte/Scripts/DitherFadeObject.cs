using UnityEngine;

public class DitherFadeObject : MonoBehaviour
{
    [Range(0,1)]
    public float currentOpacity = 1f;
    public float targetOpacity = 0.3f;
    public float fadeSpeed = 4f;

    Renderer rend;
    MaterialPropertyBlock mbp;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        mbp = new MaterialPropertyBlock();
    }

    void Update()
    {
        SetTargetOpacity();
    }

    void SetTargetOpacity()
    {
        currentOpacity = Mathf.Lerp(currentOpacity, targetOpacity, fadeSpeed * Time.deltaTime * 3);
        mbp.SetFloat("_Opacity", currentOpacity);
        rend.SetPropertyBlock(mbp);
        targetOpacity = 1f;
    }

    public void FadeTo(float target)
    {
        targetOpacity = Mathf.Min(targetOpacity, target);
    }
}
