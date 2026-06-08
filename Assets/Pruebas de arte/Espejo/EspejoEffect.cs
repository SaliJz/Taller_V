using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class EspejoController : MonoBehaviour
{
    [Header("Estados")]
    public bool Base = true;
    public bool Nivel1;
    public bool Nivel2;
    public bool Nivel3;

    [Header("Materiales")]
    public Material textBase;
    public Material EspejoMaterial;

    [Header("Posiciones de Cámara")]
    public Transform nivel1;
    public Transform nivel2;
    public Transform nivel3;

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

    private bool lastBase;
    private bool lastNivel1;
    private bool lastNivel2;
    private bool lastNivel3;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();

        if (Player != null)
            ultimaPosicionPlayer = Player.transform.position;

        GuardarEstados();
        AplicarEstado();
    }

    private void Update()
    {
        ControlarBoolUnico();
        AplicarEstado();
        RotarCamaraSegunPlayer();
    }

    private void ControlarBoolUnico()
    {
        if (Base != lastBase && Base)
        {
            Nivel1 = false;
            Nivel2 = false;
            Nivel3 = false;
        }
        else if (Nivel1 != lastNivel1 && Nivel1)
        {
            Base = false;
            Nivel2 = false;
            Nivel3 = false;
        }
        else if (Nivel2 != lastNivel2 && Nivel2)
        {
            Base = false;
            Nivel1 = false;
            Nivel3 = false;
        }
        else if (Nivel3 != lastNivel3 && Nivel3)
        {
            Base = false;
            Nivel1 = false;
            Nivel2 = false;
        }

        if (!Base && !Nivel1 && !Nivel2 && !Nivel3)
        {
            Base = true;
        }

        GuardarEstados();
    }

    private void GuardarEstados()
    {
        lastBase = Base;
        lastNivel1 = Nivel1;
        lastNivel2 = Nivel2;
        lastNivel3 = Nivel3;
    }

    private void AplicarEstado()
    {
        AplicarMaterial();
        MoverCamaraANivel();
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

        if (Base)
        {
            materiales[2] = textBase;
        }
        else
        {
            materiales[2] = EspejoMaterial;
        }

        meshRenderer.materials = materiales;
    }

    private void MoverCamaraANivel()
    {
        if (CameraEspejo == null) return;

        if (Nivel1 && nivel1 != null)
        {
            CameraEspejo.transform.position = nivel1.position;
        }
        else if (Nivel2 && nivel2 != null)
        {
            CameraEspejo.transform.position = nivel2.position;
        }
        else if (Nivel3 && nivel3 != null)
        {
            CameraEspejo.transform.position = nivel3.position;
        }
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