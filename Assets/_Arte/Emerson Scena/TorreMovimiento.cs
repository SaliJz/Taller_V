using UnityEngine;

public class TorreMovimiento : MonoBehaviour
{
    public float tiempoDeVida = 2f;

    private Vector3 posicionInicial;
    private Material mat;
    private float tiempoSubida = 0.3f;
    private float tiempoActual = 0f;

    void Start()
    {
        posicionInicial = transform.position;

        // Rotación aleatoria
        float rotZ = Random.Range(-45f, 45f);
        transform.rotation = Quaternion.Euler(0, 0, rotZ);

        mat = GetComponent<Renderer>().material;

        // Empieza en dorado (Tint = 1)
        mat.SetFloat("_Tint", 1f);
    }

    void Update()
    {
        // Subida en Y + cambio de tint
        if (tiempoActual < tiempoSubida)
        {
            float t = tiempoActual / tiempoSubida;

            transform.position = Vector3.Lerp(
                posicionInicial,
                posicionInicial + Vector3.up,
                t
            );

            // Tint de 1 → 0
            float tintValue = Mathf.Lerp(1f, 0f, t);
            mat.SetFloat("_Tint", tintValue);

            tiempoActual += Time.deltaTime;
        }

        // Tiempo de vida
        tiempoDeVida -= Time.deltaTime;

        if (tiempoDeVida <= 0)
        {
            transform.position = posicionInicial;
            Destroy(gameObject);
        }
    }
}