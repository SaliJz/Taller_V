using UnityEngine;

[RequireComponent(typeof(Transform))]
public class LinternaArea : MonoBehaviour
{
    [Header("Area")]
    [Tooltip("Radio del area de la linterna (en unidades)")]
    public float radio = 5f;

    [Tooltip("Multiplicador que se aplica a los enemigos cuando el player entra")]
    public float multiplicadorAlEntrar = 1.6f;

    [Tooltip("Multiplicador al salir (normalmente 1)")]
    public float multiplicadorAlSalir = 1f;

    [Header("Visual")]
    [Tooltip("Cantidad de segmentos para dibujar el circulo de la linterna")]
    public int segmentos = 48;
    [Tooltip("Grosor del LineRenderer")]
    public float grosorLinea = 0.05f;

    GameObject triggerGO;
    GameObject visualGO;
    LineRenderer line;

    void Start()
    {
        CrearTrigger();
        CrearVisual();
        ActualizarVisual();
    }

    void OnValidate()
    {
        // si editas en inspector actualiza en editor
        if (Application.isPlaying)
        {
            if (triggerGO != null)
            {
                var sc = triggerGO.GetComponent<SphereCollider>();
                if (sc != null) sc.radius = Mathf.Max(0.01f, radio);
            }
            if (line != null) ActualizarVisual();
        }
    }

    // Crea el child que contiene el SphereCollider trigger y un Rigidbody kinematico
    void CrearTrigger()
    {
        triggerGO = new GameObject("AreaLinterna_Trigger");
        triggerGO.transform.SetParent(transform, false);
        triggerGO.transform.localPosition = Vector3.zero;
        var sc = triggerGO.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = Mathf.Max(0.01f, radio);

        // Para asegurarnos que los OnTrigger llamen aunque el player no tenga rigidbody
        var rb = triggerGO.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // Añadimos este componente para recibir eventos de trigger en este objeto padre
        var relay = triggerGO.AddComponent<TriggerRelay>();
        relay.padre = this;
    }

    // Crea visual con LineRenderer que dibuja un circulo segun 'radio'
    void CrearVisual()
    {
        visualGO = new GameObject("AreaLinterna_Visual");
        visualGO.transform.SetParent(transform, false);
        visualGO.transform.localPosition = Vector3.zero;

        line = visualGO.AddComponent<LineRenderer>();
        line.loop = true;
        line.positionCount = Mathf.Max(3, segmentos);
        line.useWorldSpace = false;
        line.startWidth = grosorLinea;
        line.endWidth = grosorLinea;
        line.material = new Material(Shader.Find("Sprites/Default")); // shader simple
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.alignment = LineAlignment.View;
        // color simple con algo de transparencia
        line.startColor = new Color(1f, 1f, 0.6f, 0.45f);
        line.endColor = new Color(1f, 1f, 0.6f, 0.15f);

        ActualizarVisual();
    }

    void ActualizarVisual()
    {
        if (line == null) return;
        int pts = Mathf.Max(3, segmentos);
        line.positionCount = pts;
        float angStep = (Mathf.PI * 2f) / pts;
        for (int i = 0; i < pts; i++)
        {
            float ang = angStep * i;
            float x = Mathf.Cos(ang) * radio;
            float z = Mathf.Sin(ang) * radio;
            line.SetPosition(i, new Vector3(x, 0f, z));
        }
    }

    // Llamado por el TriggerRelay cuando algo entra en el trigger
    public void OnPlayerEnterTrigger()
    {
        // aumenta la velocidad de todos los enemigos que tengan SeguidorNavMeshSuave
        var enemigos = FindObjectsOfType<SeguidorNavMeshSuave>();
        foreach (var e in enemigos)
        {
            e.SetMultiplicadorVelocidad(multiplicadorAlEntrar);
        }
    }

    // Llamado por el TriggerRelay cuando algo sale del trigger
    public void OnPlayerExitTrigger()
    {
        var enemigos = FindObjectsOfType<SeguidorNavMeshSuave>();
        foreach (var e in enemigos)
        {
            e.SetMultiplicadorVelocidad(multiplicadorAlSalir);
        }
    }
}

// Clase auxiliar que reenvia eventos de trigger desde el child trigger al componente LinternaArea
public class TriggerRelay : MonoBehaviour
{
    public LinternaArea padre;

    void OnTriggerEnter(Collider other)
    {
        if (padre == null) return;
        if (other.CompareTag("Player"))
        {
            padre.OnPlayerEnterTrigger();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (padre == null) return;
        if (other.CompareTag("Player"))
        {
            padre.OnPlayerExitTrigger();
        }
    }
}
