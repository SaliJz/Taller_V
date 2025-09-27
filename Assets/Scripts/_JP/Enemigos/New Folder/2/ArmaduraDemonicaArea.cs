using System.Collections;
using System.Reflection;
using UnityEngine;

[RequireComponent(typeof(EnemyHealth))]
public class ArmaduraDemonicaArea : MonoBehaviour
{
    [Header("Armadura")]
    [SerializeField, Range(0f, 1f)] private float reduccionPercent = 0.25f;
    [SerializeField] private float duracion = 10f;
    [SerializeField] private float cooldown = 4.5f;
    [SerializeField] private float radio = 8f;
    [SerializeField] private LayerMask capasAfectadas = ~0;

    [Header("Prefab del área (preferido)")]
    [Tooltip("Prefab que contiene ArmaduraAreaPrefab. Si está vacío, se creará runtime como fallback.")]
    [SerializeField] private GameObject areaPrefab;

    [Header("Visual / Opciones")]
    [SerializeField] private Color visualColor = new Color(0f, 1f, 0f, 0.28f);
    [SerializeField] private float visualThickness = 0.08f;
    [SerializeField] private float flattenHeightThreshold = 1.2f;

    [Header("Debug / Test")]
    [SerializeField] private float checkInterval = 0.25f;

    // runtime
    private EnemyHealth salud;
    private GameObject areaInstance;
    private ArmaduraAreaPrefab areaPrefabComp;
    private bool activa = false;
    private bool enCooldown = false;
    private Coroutine rutina = null;

    private void Awake()
    {
        salud = GetComponent<EnemyHealth>();
        if (salud == null)
            Debug.LogError($"[{name}] ArmaduraDemonicaArea requiere EnemyHealth en el mismo GameObject.");

        CreateOrInstantiateArea();
        if (areaPrefabComp != null)
            areaPrefabComp.DeactivateArea(); // inicio desactivado
    }

    private void Start()
    {
        StartCoroutine(HealthMonitor());
    }

    private void CreateOrInstantiateArea()
    {
        if (areaPrefab != null)
        {
            areaInstance = Instantiate(areaPrefab, transform);
            areaInstance.name = "ArmaduraArea_Instance";
            areaInstance.transform.SetParent(transform, false);
            areaInstance.transform.localPosition = Vector3.zero;
            areaInstance.transform.localRotation = Quaternion.identity;

            areaPrefabComp = areaInstance.GetComponent<ArmaduraAreaPrefab>();
            if (areaPrefabComp == null)
            {
                Debug.LogWarning($"[{name}] areaPrefab no contiene ArmaduraAreaPrefab. Añadiendo componente automáticamente.");
                areaPrefabComp = areaInstance.AddComponent<ArmaduraAreaPrefab>();
            }
            areaPrefabComp.Initialize(this, radio, visualColor, visualThickness, flattenHeightThreshold, capasAfectadas);
        }
        else
        {
            // Fallback: crear simple runtime prefab-like object
            areaInstance = new GameObject("ArmaduraArea_Runtime");
            areaInstance.transform.SetParent(transform, false);
            areaInstance.transform.localPosition = Vector3.zero;
            areaPrefabComp = areaInstance.AddComponent<ArmaduraAreaPrefab>();
            areaPrefabComp.Initialize(this, radio, visualColor, visualThickness, flattenHeightThreshold, capasAfectadas);
        }
    }

    private IEnumerator HealthMonitor()
    {
        while (salud == null || salud.MaxHealth <= 0f) yield return null;

        while (true)
        {
            if (!activa && !enCooldown)
            {
                float percent = salud.CurrentHealth / Mathf.Max(1f, salud.MaxHealth);
                if (percent <= 0.25f)
                {
                    ActivateArmadura();
                }
            }
            yield return new WaitForSeconds(checkInterval);
        }
    }

    public void ForceActivate()
    {
        if (rutina != null) StopCoroutine(rutina);
        enCooldown = false;
        ActivateArmadura();
    }

    private void ActivateArmadura()
    {
        if (activa || enCooldown) return;
        activa = true;

        // habilitar area (collider + visual)
        if (areaPrefabComp != null) areaPrefabComp.ActivateArea();

        // aplicarse a sí mismo
        TryApplyReductionTo(gameObject, reduccionPercent, duracion);

        // aplicar inmediatamente a objetos en rango
        Collider[] hits = Physics.OverlapSphere(transform.position, radio, capasAfectadas, QueryTriggerInteraction.Ignore);
        foreach (var c in hits)
        {
            GameObject root = c.transform.root != null ? c.transform.root.gameObject : c.gameObject;
            if (root == this.gameObject) continue;
            if (Mathf.Abs(root.transform.position.y - transform.position.y) > flattenHeightThreshold) continue;
            TryApplyReductionTo(root, reduccionPercent, duracion);
        }

        rutina = StartCoroutine(ArmaduraDurationAndCooldown());
        Debug.Log($"[{name}] Armadura ACTIVADA. Radio={radio} Reducción={(reduccionPercent * 100f)}%");
    }

    private IEnumerator ArmaduraDurationAndCooldown()
    {
        yield return new WaitForSeconds(duracion);
        DeactivateArmadura();
        enCooldown = true;
        yield return new WaitForSeconds(cooldown);
        enCooldown = false;
        Debug.Log($"[{name}] Armadura COOL DOWN finalizado.");
    }

    private void DeactivateArmadura()
    {
        activa = false;
        if (areaPrefabComp != null) areaPrefabComp.DeactivateArea();
        Debug.Log($"[{name}] Armadura DESACTIVADA.");
    }

    internal void OnEntityEnterArea(GameObject other)
    {
        // llamado desde el prefab cuando hay trigger enter
        if (!activa) return;
        if (other == null) return;
        if (Mathf.Abs(other.transform.position.y - transform.position.y) > flattenHeightThreshold) return;
        TryApplyReductionTo(other, reduccionPercent, duracion);
    }

    private void TryApplyReductionTo(GameObject target, float percent, float dur)
    {
        if (target == null) return;

        string[] posibles = {
            "ApplyDamageReduction", "ApplyArmor", "AddDamageModifier", "AddDamageReduction",
            "AplicarReduccionDanio", "AplicarArmadura", "AgregarModificadorDanio", "AgregarReduccionDanio"
        };

        foreach (var mb in target.GetComponents<MonoBehaviour>())
        {
            if (mb == null) continue;
            var t = mb.GetType();
            foreach (var name in posibles)
            {
                var mi = t.GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
                if (mi != null)
                {
                    try
                    {
                        mi.Invoke(mb, new object[] { percent, dur });
                        Debug.Log($"[{name}] Aplicada reducción a {target.name} vía {t.Name}.{mi.Name}()");
                        return;
                    }
                    catch { /* ignore */ }
                }
            }
        }

        // fallback SendMessage
        try
        {
            target.SendMessage("ApplyDamageReduction", new object[] { percent, dur }, SendMessageOptions.DontRequireReceiver);
            target.SendMessage("AplicarReduccionDanio", new object[] { percent, dur }, SendMessageOptions.DontRequireReceiver);
            Debug.Log($"[ArmaduraDemonicaArea] SendMessage enviado a {target.name} como fallback.");
        }
        catch { }
    }

    // Exponer algunas propiedades para que el prefab pueda consultarlas si quisiera
    public float Radio => radio;
    public LayerMask CapasAfectadas => capasAfectadas;
}
