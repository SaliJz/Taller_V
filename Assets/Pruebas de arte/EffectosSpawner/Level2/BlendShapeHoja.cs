using System.Collections;
using UnityEngine;

public class BlendShapeCrecerAlActivarse : MonoBehaviour
{
    [Header("Skinned Mesh")]
    [SerializeField] private SkinnedMeshRenderer skinnedMeshRenderer;

    [Header("Blend Shape")]
    [SerializeField] private string nombreBlendShape = "blendShape.Crecer";

    [Header("Valores")]
    [SerializeField] private float valorMinimo = 0f;
    [SerializeField] private float valorMaximo = 100f;

    [Header("Tiempo")]
    [SerializeField, Min(1)] private int tiempoEnSegundos = 1;

    private int blendShapeIndex = -1;
    private Coroutine rutina;

    private void Reset()
    {
        skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
    }

    private void Awake()
    {
        BuscarBlendShape();
    }

    private void OnEnable()
    {
        BuscarBlendShape();

        if (blendShapeIndex == -1) return;

        if (rutina != null)
            StopCoroutine(rutina);

        rutina = StartCoroutine(AnimarBlendShape());
    }

    private void OnDisable()
    {
        if (rutina != null)
        {
            StopCoroutine(rutina);
            rutina = null;
        }
    }

    private void BuscarBlendShape()
    {
        if (skinnedMeshRenderer == null)
            skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();

        if (skinnedMeshRenderer == null)
        {
            Debug.LogError("No se encontró SkinnedMeshRenderer.", this);
            return;
        }

        Mesh mesh = skinnedMeshRenderer.sharedMesh;

        if (mesh == null)
        {
            Debug.LogError("El SkinnedMeshRenderer no tiene mesh.", this);
            return;
        }

        string nombreReal = nombreBlendShape;

        blendShapeIndex = mesh.GetBlendShapeIndex(nombreReal);

        if (blendShapeIndex == -1)
        {
            Debug.LogError($"No se encontró el blend shape: {nombreReal}", this);

            for (int i = 0; i < mesh.blendShapeCount; i++)
                Debug.Log($"BlendShape disponible {i}: {mesh.GetBlendShapeName(i)}", this);
        }
    }

    private IEnumerator AnimarBlendShape()
    {
        float tiempoActual = 0f;

        skinnedMeshRenderer.SetBlendShapeWeight(blendShapeIndex, valorMinimo);

        while (tiempoActual < tiempoEnSegundos)
        {
            tiempoActual += Time.deltaTime;

            float t = tiempoActual / tiempoEnSegundos;
            float valor = Mathf.Lerp(valorMinimo, valorMaximo, t);

            skinnedMeshRenderer.SetBlendShapeWeight(blendShapeIndex, valor);

            yield return null;
        }

        skinnedMeshRenderer.SetBlendShapeWeight(blendShapeIndex, valorMaximo);
        rutina = null;
    }
}
