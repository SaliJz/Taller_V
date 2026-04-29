using UnityEngine;

public class TorreMovimiento : MonoBehaviour
{
    public float altura = 2f;
    public float duracion = 0.3f;

    private Vector3 posicionInicial;
    private Vector3 maxHeight;
    private float tiempo = 0f;

    void Start()
    {
        posicionInicial = transform.position;

        // Posición objetivo (sube en Y)
        maxHeight = new Vector3(
            transform.position.x,
            transform.position.y + altura,
            transform.position.z
        );
    }

    void Update()
    {
        if (tiempo < duracion)
        {
            tiempo += Time.deltaTime;

            float t = tiempo / duracion;

            transform.position = Vector3.Lerp(
                posicionInicial,
                maxHeight,
                t
            );
        }
    }
}