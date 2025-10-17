using UnityEngine;
using System.Collections;

/// <summary>
/// Escala el objeto desde un tama�o inicial a uno final durante un tiempo determinado.
/// Opcionalmente, se destruye al finalizar.
/// </summary>
public class ExplosionScaleOverTime : MonoBehaviour
{
    [Tooltip("Tama�o inicial del objeto.")]
    [SerializeField] private Vector3 startScale = new Vector3(0.5f, 0.5f, 0.5f);

    [Tooltip("Tama�o final que alcanzar� el objeto.")]
    [SerializeField] private Vector3 endScale = new Vector3(5f, 5f, 5f);

    [Tooltip("Duraci�n en segundos de la animaci�n de escalado.")]
    [SerializeField] private float duration = 0.3f;

    [Tooltip("Curva para suavizar la animaci�n de escalado.")]
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("Si es verdadero, el objeto se destruir� al terminar la animaci�n.")]
    [SerializeField] private bool destroyOnComplete = true;

    private float elapsedTime = 0f;

    public Vector3 StartScale
    {
        get => startScale;
        set => startScale = value;
    }

    public Vector3 EndScale
    {
        get => endScale;
        set => endScale = value;
    }

    private void Start()
    {
        transform.localScale = startScale;
    }

    private void Update()
    {
        if (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            float curveT = scaleCurve.Evaluate(t);

            transform.localScale = Vector3.LerpUnclamped(startScale, endScale, curveT);

            if (elapsedTime >= duration && destroyOnComplete)
            {
                Destroy(gameObject);
            }
        }
    }
}