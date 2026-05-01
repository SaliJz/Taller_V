using UnityEngine;
using System.Collections;

public class TorreMovimiento : MonoBehaviour
{
    public float altura = 2f;
    public float duracion = 0.3f;
    public float tiempoDeVida = 2f;

    private Vector3 posicionInicial;
    private Vector3 posicionFinal;

    void Start()
    {
        posicionInicial = transform.position;

        posicionFinal = new Vector3(
            transform.position.x,
            transform.position.y + altura,
            transform.position.z
        );

        StartCoroutine(MovimientoCompleto());
    }

    IEnumerator MovimientoCompleto()
    {
        float tiempo = 0f;

        while (tiempo < duracion)
        {
            tiempo += Time.deltaTime;
            float t = tiempo / duracion;

            transform.position = Vector3.Lerp(posicionInicial, posicionFinal, t);
            yield return null;
        }

        yield return new WaitForSeconds(tiempoDeVida);

        tiempo = 0f;

        while (tiempo < duracion)
        {
            tiempo += Time.deltaTime;
            float t = tiempo / duracion;

            transform.position = Vector3.Lerp(posicionFinal, posicionInicial, t);
            yield return null;
        }

        Destroy(transform.parent.gameObject);
    }
}