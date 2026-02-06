using System.Collections.Generic;
using UnityEngine;

public class CameraOcclusionFade : MonoBehaviour
{
    public Camera cam;
    public LayerMask obstructionMask;
    public List<Transform> targets;

    [Range(0,1)]
    public float fadeOpacity = 0.2f;
    public float fadeSpeed = 1f;

    Dictionary<Renderer, float> renderers = new Dictionary<Renderer, float>();
    // Renderer currentRender;
    float currentOpacity = 1f;

    MaterialPropertyBlock mpb;

    void Awake()
    {
        if (!cam) cam = Camera.main;
        mpb = new MaterialPropertyBlock();
    }

    void Update()
    {
        HashSet<Renderer> shouldFade = new HashSet<Renderer>();

        foreach(Transform target in targets)
        {
            Vector3 dir = target.position - cam.transform.position;
            float dist = dir.magnitude;

            Ray ray = new Ray(cam.transform.position, dir.normalized);

            if(Physics.Raycast(ray, out RaycastHit hit, dist, obstructionMask))
            {
                Renderer r = hit.collider.GetComponent<Renderer>();
                if(r) shouldFade.Add(r);
            }
        }

        UpdateRenderers(shouldFade);
    }

    void UpdateRenderers(HashSet<Renderer> shouldFade)
    {
        foreach(Renderer r in shouldFade)
        {
            if(!renderers.ContainsKey(r)) renderers[r] = 1f;

            renderers[r] = Mathf.MoveTowards(renderers[r], fadeOpacity, fadeSpeed * Time.deltaTime);
            ApplyOpacity(r, renderers[r]);
        }

        List<Renderer> keys = new List<Renderer>(renderers.Keys);

        foreach(Renderer r in keys)
        {
            if(shouldFade.Contains(r)) continue;

            renderers[r] = Mathf.MoveTowards(renderers[r], 1f, fadeSpeed * Time.deltaTime);
            ApplyOpacity(r, renderers[r]);

            if(Mathf.Approximately(renderers[r], 1f)) renderers.Remove(r);
        }
    }
    void ApplyOpacity(Renderer renderer, float value)
    {
        renderer.GetPropertyBlock(mpb);
        mpb.SetFloat("_Opacity", value);
        renderer.SetPropertyBlock(mpb);
    }
}
