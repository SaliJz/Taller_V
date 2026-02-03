using UnityEngine;

public class CameraOcclusionFade : MonoBehaviour
{
    public Transform target;
    public LayerMask occlusionMask;
    public float fadedOppacity = 0.3f;

    Camera cam;

    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        Vector3 dir = target.position - cam.transform.position;
        float dist = dir.magnitude;

        if(Physics.Raycast(cam.transform.position, dir.normalized, out RaycastHit hit, dist, occlusionMask))
        {
            var fade = hit.collider.GetComponent<DitherFadeObject>();
            if (fade != null)
            {
                fade.FadeTo(fadedOppacity);
            }
        }
    }
}
