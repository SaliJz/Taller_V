using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class EspejoController : MonoBehaviour
{
    public enum EstadoEspejo
    {
        Base,
        Nivel1,
        Nivel2,
        Nivel3
    }

    [Header("Estado actual")]
    public EstadoEspejo estadoActual = EstadoEspejo.Base;

    [Header("Materiales")]
    public Material textBase;
    public Material EspejoMaterial;

    [Header("Objetos por nivel")]
    public GameObject nivel1;
    public GameObject nivel2;
    public GameObject nivel3;

    [Header("Referencias")]
    public GameObject Player;
    public GameObject CameraEspejo;

    [Header("Rotación del efecto espejo")]
    public float rotacionMaxima = 45f;
    public float sensibilidadRotacion = 80f;
    public float suavizadoRotacion = 8f;

    private MeshRenderer meshRenderer;
    private Vector3 ultimaPosicionPlayer;
    private float rotacionActualY;

    private EstadoEspejo ultimoEstado;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();

        if (Player != null)
            ultimaPosicionPlayer = Player.transform.position;

        ultimoEstado = estadoActual;
        AplicarEstado();
    }

    private void Update()
    {
        if (estadoActual != ultimoEstado)
        {
            AplicarEstado();
            ultimoEstado = estadoActual;
        }

        RotarCamaraSegunPlayer();
    }

    private void AplicarEstado()
    {
        AplicarMaterial();
        ActivarNivel();
    }

    private void AplicarMaterial()
    {
        if (meshRenderer == null) return;

        Material[] materiales = meshRenderer.materials;

        if (materiales.Length <= 2)
        {
            Debug.LogWarning("El MeshRenderer no tiene Element 2 en Materials.", this);
            return;
        }

        if (estadoActual == EstadoEspejo.Base)
        {
            materiales[2] = textBase;
        }
        else
        {
            materiales[2] = EspejoMaterial;
        }

        meshRenderer.materials = materiales;
    }

    private void ActivarNivel()
    {
        if (nivel1 != null)
            nivel1.SetActive(estadoActual == EstadoEspejo.Nivel1);

        if (nivel2 != null)
            nivel2.SetActive(estadoActual == EstadoEspejo.Nivel2);

        if (nivel3 != null)
            nivel3.SetActive(estadoActual == EstadoEspejo.Nivel3);
    }

    private void RotarCamaraSegunPlayer()
    {
        if (Player == null || CameraEspejo == null) return;

        Vector3 posicionActual = Player.transform.position;
        float movimientoX = posicionActual.x - ultimaPosicionPlayer.x;

        float rotacionObjetivo = rotacionActualY;

        if (Mathf.Abs(movimientoX) > 0.001f)
        {
            rotacionObjetivo += -movimientoX * sensibilidadRotacion;
            rotacionObjetivo = Mathf.Clamp(rotacionObjetivo, -rotacionMaxima, rotacionMaxima);
        }

        rotacionActualY = Mathf.Lerp(
            rotacionActualY,
            rotacionObjetivo,
            Time.deltaTime * suavizadoRotacion
        );

        CameraEspejo.transform.localRotation = Quaternion.Euler(0f, rotacionActualY, 0f);

        ultimaPosicionPlayer = posicionActual;
    }
}