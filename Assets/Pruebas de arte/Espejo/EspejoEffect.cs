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

    [Header("Referencias de cámara")]
    public GameObject MainCamera;
    public GameObject CameraEspejo;

    [Header("Seguimiento de cámara espejo")]
    public bool copiarRotacion = true;
    public bool copiarMovimiento = true;

    [Tooltip("Distancia máxima que CameraEspejo puede alejarse de su posición inicial.")]
    public float limiteMovimiento = 10f;

    [Tooltip("Suavizado del movimiento de CameraEspejo.")]
    public float suavizadoMovimiento = 8f;

    [Tooltip("Suavizado de la rotación de CameraEspejo.")]
    public float suavizadoRotacion = 8f;

    private MeshRenderer meshRenderer;

    private Vector3 posicionInicialMainCamera;
    private Vector3 posicionInicialCameraEspejo;

    private EstadoEspejo ultimoEstado;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();

        if (MainCamera == null)
        {
            MainCamera = GameObject.Find("Main Camera");
        }

        if (MainCamera != null)
        {
            posicionInicialMainCamera = MainCamera.transform.position;
        }
        else
        {
            Debug.LogWarning("No se encontró un GameObject llamado 'Main Camera'.", this);
        }

        if (CameraEspejo != null)
        {
            posicionInicialCameraEspejo = CameraEspejo.transform.position;
        }

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

        SincronizarCamaraEspejo();

        testInputs();
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

    private void SincronizarCamaraEspejo()
    {
        if (MainCamera == null || CameraEspejo == null) return;

        if (copiarMovimiento)
        {
            Vector3 desplazamientoMain = MainCamera.transform.position - posicionInicialMainCamera;

            if (desplazamientoMain.magnitude > limiteMovimiento)
            {
                desplazamientoMain = desplazamientoMain.normalized * limiteMovimiento;
            }

            Vector3 posicionObjetivo = posicionInicialCameraEspejo + desplazamientoMain;

            CameraEspejo.transform.position = Vector3.Lerp(
                CameraEspejo.transform.position,
                posicionObjetivo,
                Time.deltaTime * suavizadoMovimiento
            );
        }

        if (copiarRotacion)
        {
            Quaternion rotacionObjetivo = MainCamera.transform.rotation;

            CameraEspejo.transform.rotation = Quaternion.Lerp(
                CameraEspejo.transform.rotation,
                rotacionObjetivo,
                Time.deltaTime * suavizadoRotacion
            );
        }
    }

    void testInputs()
    {
        if(Input.GetKeyDown(KeyCode.Alpha1)) estadoActual = EstadoEspejo.Nivel1;
        if(Input.GetKeyDown(KeyCode.Alpha2)) estadoActual = EstadoEspejo.Nivel2;
        if(Input.GetKeyDown(KeyCode.Alpha3)) estadoActual = EstadoEspejo.Nivel3;
        if(Input.GetKeyDown(KeyCode.Space)) estadoActual = EstadoEspejo.Base;
    }
}