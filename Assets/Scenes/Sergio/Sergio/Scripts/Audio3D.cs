using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class Audio3D : MonoBehaviour
{
    public Transform jugador;
    public float distanciaMaxima = 20f;

    private AudioSource source;

    void Awake()
    {
        jugador = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    private void Start()
    {
        source = GetComponent<AudioSource>();
    }

    private void Update()
    {
        float distancia = Vector3.Distance(transform.position, jugador.position);

        source.volume = Mathf.Clamp01(1f - (distancia / distanciaMaxima));

        Debug.Log($"Distancia: {distancia} | Volumen: {source.volume}");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, distanciaMaxima);
    }
}