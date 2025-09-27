using UnityEngine;

/// <summary>
/// Componente que debe ir en el prefab del área.
/// Gestiona SphereCollider trigger + visual, y notifica al owner con OnEntityEnterArea.
/// Ahora: no sobrescribe material existente, expone visualMaterial en inspector y evita DestroyImmediate.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class ArmaduraAreaPrefab : MonoBehaviour
{
    [Header("Opciones visuales")]
    [Tooltip("Si quieres forzar un material para el visual, asígnalo aquí en el prefab.")]
    [SerializeField] private Material visualMaterial = null;

    private SphereCollider sphere;
    private GameObject visual;
    private ArmaduraDemonicaArea owner;
    private float flattenHeightThreshold = 1.2f;
    private LayerMask capasAfectadas = ~0;

    private void Awake()
    {
        sphere = GetComponent<SphereCollider>();
        if (sphere == null) sphere = gameObject.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
    }

    /// <summary>
    /// Inicializa el prefab (llamado por el controlador).
    /// </summary>
    public void Initialize(ArmaduraDemonicaArea owner, float radius, Color color, float thickness, float flattenHeight, LayerMask capas)
    {
        this.owner = owner;
        this.flattenHeightThreshold = flattenHeight;
        this.capasAfectadas = capas;

        if (sphere == null) sphere = gameObject.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = Mathf.Max(0.01f, radius);

        CreateOrFindVisual(radius, color, thickness);
        HideVisual();
    }

    private void CreateOrFindVisual(float radius, Color color, float thickness)
    {
        // 1) Buscar child existente "AreaVisual" (si el prefab ya lo tiene, lo preservamos)
        Transform t = transform.Find("AreaVisual");
        if (t != null)
        {
            visual = t.gameObject;
        }

        // 2) Si no existe, crear uno (Cylinder por defecto)
        if (visual == null)
        {
            visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "AreaVisual";
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            // remover collider del primitive: usar Destroy (no Immediate)
            var col = visual.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        // ajustar escala: X/Z = diameter, Y = thickness
        float diameter = radius * 2f;
        visual.transform.localScale = new Vector3(diameter, thickness, diameter);

        // obtener renderer
        var rend = visual.GetComponent<Renderer>();
        if (rend == null) return;

        // Si el visual ya tiene un material (por ejemplo el prefab lo traía), NO lo sobreescribimos
        if (rend.sharedMaterial != null)
        {
            // preservar material del prefab / asset
            return;
        }

        // Si el usuario asignó un material en el prefab inspector, usarlo
        if (visualMaterial != null)
        {
            // asignar sharedMaterial para que el prefab lo mantenga como referencia
            rend.sharedMaterial = visualMaterial;
            return;
        }

        // Fallback: crear un material transparente con shader apropiado.
        // Intentar URP Lit (si el proyecto usa URP), si no, usar Standard.
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material mat = null;
        if (shader != null)
        {
            mat = new Material(shader);
        }
        else
        {
            // último recurso: material default
            mat = new Material(Shader.Find("Standard"));
        }

        // configurar transparencia (compatible con Standard y URP lit)
        // Para URP Lit, las propiedades difieren, pero color.a funcionará en la mayoría de casos.
        Color c = color;
        if (c.a >= 0.99f) c.a = 0.28f;
        mat.color = c;

        // No forzamos sharedMaterial en el asset si no existía; asignamos material instanciado al renderer.
        // Esto evita modificar assets del proyecto en tiempo de ejecución.
        rend.material = mat;
    }

    public void ActivateArea()
    {
        if (sphere != null) sphere.enabled = true;
        if (visual != null) visual.SetActive(true);
    }

    public void DeactivateArea()
    {
        if (sphere != null) sphere.enabled = false;
        if (visual != null) visual.SetActive(false);
    }

    private void HideVisual()
    {
        if (visual != null) visual.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (owner == null) return;

        // respetar capas del owner
        if (((1 << other.gameObject.layer) & owner.CapasAfectadas) == 0) return;

        var root = other.transform.root != null ? other.transform.root.gameObject : other.gameObject;
        if (root == owner.gameObject) return;

        if (Mathf.Abs(root.transform.position.y - owner.transform.position.y) > flattenHeightThreshold) return;

        owner.OnEntityEnterArea(root);
    }

    public void SetRadius(float r)
    {
        if (sphere != null) sphere.radius = Mathf.Max(0.01f, r);
        if (visual != null) visual.transform.localScale = new Vector3(r * 2f, visual.transform.localScale.y, r * 2f);
    }
}
