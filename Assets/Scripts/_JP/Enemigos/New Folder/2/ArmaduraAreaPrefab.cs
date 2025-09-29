// ArmaduraAreaPrefab.cs (actualizado)
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

[DisallowMultipleComponent]
public class ArmaduraAreaPrefab : MonoBehaviour
{
    [Header("Opciones del efecto (ahora en el prefab)")]
    [SerializeField, Range(0f, 1f)] private float reduccionPercent = 0.25f;
    [SerializeField] private float duracion = 10f; // ahora �nico tiempo (duraci�n y m�nimo visible)

    [Header("Comportamiento adicional")]
    [Tooltip("Intervalo en segundos para reaplicar efecto a entidades dentro del �rea.")]
    [SerializeField] private float applyInterval = 1f;

    private Collider col;
    private ArmaduraDemonicaArea owner;
    private float flattenHeightThreshold = 1.2f;
    private LayerMask capasAfectadas = ~0;

    // corrutinas internas
    private Coroutine lifecycleRoutine = null;    // activaciones completas
    private Coroutine momentaryRoutine = null;    // activaciones moment�neas

    // para reaplicar mientras est�n dentro (y para poder notificar limpieza)
    private Dictionary<GameObject, float> lastApplyTime = new Dictionary<GameObject, float>();

    public float ReductionPercent { get => reduccionPercent; set => reduccionPercent = Mathf.Clamp01(value); }
    public float Duration { get => duracion; set => duracion = Mathf.Max(0f, value); }

    private void Awake()
    {
        col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogError($"[{name}] ArmaduraAreaPrefab requiere un Collider (Box/Capsule/Sphere) ya configurado en este GameObject.");
        }
        else
        {
            if (!col.isTrigger)
            {
                Debug.LogWarning($"[{name}] Collider no est� en modo trigger. Forzando isTrigger = true.");
                col.isTrigger = true;
            }
        }
    }

    public void Initialize(ArmaduraDemonicaArea owner, float radius, Color color, float thickness, float flattenHeight, LayerMask capas)
    {
        this.owner = owner;
        this.flattenHeightThreshold = flattenHeight;
        this.capasAfectadas = capas;

        if (col == null)
        {
            col = GetComponent<Collider>();
            if (col == null) Debug.LogWarning($"[{name}] Initialize: Collider ausente. No se puede ajustar tama�o.");
        }

        if (col is SphereCollider sc) sc.radius = Mathf.Max(0.01f, radius);
        else if (col is BoxCollider bc)
        {
            Vector3 size = bc.size;
            float y = size.y;
            bc.size = new Vector3(radius * 2f / transform.lossyScale.x, y, radius * 2f / transform.lossyScale.z);
        }
        else if (col is CapsuleCollider cc)
        {
            cc.radius = Mathf.Max(0.01f, radius / Mathf.Max(1f, transform.lossyScale.x));
            cc.height = Mathf.Max(cc.height, (radius * 2f) / Mathf.Max(0.0001f, transform.lossyScale.y));
        }
    }

    public void SetLayerMask(LayerMask m) { capasAfectadas = m; }
    public void SetFlattenHeight(float h) { flattenHeightThreshold = h; }

    public void SetRadius(float r)
    {
        if (col == null) { Debug.LogWarning($"[{name}] SetRadius llamado pero Collider ausente."); return; }

        if (col is SphereCollider sc) sc.radius = Mathf.Max(0.01f, r);
        else if (col is BoxCollider bc)
        {
            Vector3 size = bc.size;
            bc.size = new Vector3(r * 2f / transform.lossyScale.x, size.y, r * 2f / transform.lossyScale.z);
        }
        else if (col is CapsuleCollider cc)
        {
            cc.radius = Mathf.Max(0.01f, r / Mathf.Max(0.0001f, transform.lossyScale.x));
            cc.height = Mathf.Max(cc.height, (r * 2f) / Mathf.Max(0.0001f, transform.lossyScale.y));
        }
        else
        {
            Debug.LogWarning($"[{name}] SetRadius: tipo de Collider no soportado para ajuste autom�tico.");
        }
    }

    public void ActivateArea() { if (col != null) col.enabled = true; else Debug.LogWarning($"[{name}] ActivateArea: Collider ausente."); }
    public void DeactivateArea() { if (col != null) col.enabled = false; else Debug.LogWarning($"[{name}] DeactivateArea: Collider ausente."); }

    private void OnTriggerEnter(Collider other) => HandleTriggerEnterOrStay(other);
    private void OnTriggerStay(Collider other) => HandleTriggerEnterOrStay(other);

    private void HandleTriggerEnterOrStay(Collider other)
    {
        if (col == null) return;
        if (((1 << other.gameObject.layer) & capasAfectadas) == 0) return;

        var root = other.transform.root != null ? other.transform.root.gameObject : other.gameObject;
        if (root == null) return;
        if (owner != null && root == owner.gameObject) return;
        if (Mathf.Abs(root.transform.position.y - transform.position.y) > flattenHeightThreshold) return;

        float tNow = Time.time;
        if (!lastApplyTime.TryGetValue(root, out float last) || tNow - last >= applyInterval)
        {
            // aplicar reducci�n usando el valor del prefab (prefab-driven)
            ApplyReductionTo(root, reduccionPercent, duracion);
            lastApplyTime[root] = tNow;

            if (owner != null)
            {
                try { owner.gameObject.SendMessage("OnEntityEnterArea", root, SendMessageOptions.DontRequireReceiver); }
                catch { }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var root = other.transform.root != null ? other.transform.root.gameObject : other.gameObject;
        if (root != null)
        {
            if (lastApplyTime.ContainsKey(root)) lastApplyTime.Remove(root);

            // Notificar al target que sali� de ESTA �rea (para que quite reducci�n asociada a este prefab)
            try
            {
                root.SendMessage("RemoveDamageReductionFromArea", this.gameObject, SendMessageOptions.DontRequireReceiver);
                Debug.Log($"[ArmaduraAreaPrefab] RemoveDamageReductionFromArea enviado a {root.name} (salida).");
            }
            catch { }
        }
    }

    public void ActivateFor(float duration, Vector3 center)
    {
        transform.position = center;

        if (momentaryRoutine != null) { StopCoroutine(momentaryRoutine); momentaryRoutine = null; }
        if (lifecycleRoutine != null) StopCoroutine(lifecycleRoutine);
        lifecycleRoutine = StartCoroutine(LifecycleRoutine(duration, center));
    }

    private IEnumerator LifecycleRoutine(float duration, Vector3 center)
    {
        transform.position = center;
        ActivateArea();

        // aplicarlo al owner si existe
        if (owner != null)
        {
            ApplyReductionTo(owner.gameObject, reduccionPercent, duration);
            lastApplyTime[owner.gameObject] = Time.time;
            try { owner.gameObject.SendMessage("OnEntityEnterArea", owner.gameObject, SendMessageOptions.DontRequireReceiver); } catch { }
        }

        Collider[] hits = GetOverlaps(center);
        foreach (var c in hits)
        {
            GameObject root = c.transform.root != null ? c.transform.root.gameObject : c.gameObject;
            if (owner != null && root == owner.gameObject) continue;
            if (Mathf.Abs(root.transform.position.y - (owner != null ? owner.transform.position.y : center.y)) > flattenHeightThreshold) continue;
            ApplyReductionTo(root, reduccionPercent, duration);
            lastApplyTime[root] = Time.time;

            if (owner != null)
            {
                try { owner.gameObject.SendMessage("OnEntityEnterArea", root, SendMessageOptions.DontRequireReceiver); } catch { }
            }
        }

        yield return new WaitForSeconds(Mathf.Max(0.01f, duration));
        DeactivateArea();

        // Antes de notificar al owner, limpiar notificaciones en todos los targets a�n registrados
        var targets = new List<GameObject>(lastApplyTime.Keys);
        foreach (var t in targets)
        {
            try
            {
                t.SendMessage("RemoveDamageReductionFromArea", this.gameObject, SendMessageOptions.DontRequireReceiver);
            }
            catch { }
        }
        lastApplyTime.Clear();

        // Notificar al owner que esta instancia ya termin� su lifecycle (para que el owner pueda destruir/limpiar si corresponde)
        NotifyOwnerAreaDeactivated();

        lifecycleRoutine = null;
    }

    public void ActivateMomentaryAndApply(Vector3 center, float momentDuration)
    {
        transform.position = center;

        if (lifecycleRoutine != null)
        {
            Collider[] hits = GetOverlaps(center);
            foreach (var c in hits)
            {
                GameObject root = c.transform.root != null ? c.transform.root.gameObject : c.gameObject;
                if (owner != null && root == owner.gameObject) continue;
                if (Mathf.Abs(root.transform.position.y - (owner != null ? owner.transform.position.y : center.y)) > flattenHeightThreshold) continue;
                ApplyReductionTo(root, reduccionPercent, duracion);
                lastApplyTime[root] = Time.time;

                if (owner != null)
                {
                    try { owner.gameObject.SendMessage("OnEntityEnterArea", root, SendMessageOptions.DontRequireReceiver); } catch { }
                }
            }
            return;
        }

        if (momentaryRoutine != null) StopCoroutine(momentaryRoutine);
        momentaryRoutine = StartCoroutine(MomentaryRoutine(center, Mathf.Max(0.01f, momentDuration)));
    }

    private IEnumerator MomentaryRoutine(Vector3 center, float momentDuration)
    {
        transform.position = center;
        ActivateArea();

        if (owner != null)
        {
            ApplyReductionTo(owner.gameObject, reduccionPercent, duracion);
            lastApplyTime[owner.gameObject] = Time.time;
            try { owner.gameObject.SendMessage("OnEntityEnterArea", owner.gameObject, SendMessageOptions.DontRequireReceiver); } catch { }
        }

        Collider[] hits = GetOverlaps(center);
        foreach (var c in hits)
        {
            GameObject root = c.transform.root != null ? c.transform.root.gameObject : c.gameObject;
            if (owner != null && root == owner.gameObject) continue;
            if (Mathf.Abs(root.transform.position.y - (owner != null ? owner.transform.position.y : center.y)) > flattenHeightThreshold) continue;
            ApplyReductionTo(root, reduccionPercent, duracion);
            lastApplyTime[root] = Time.time;

            if (owner != null)
            {
                try { owner.gameObject.SendMessage("OnEntityEnterArea", root, SendMessageOptions.DontRequireReceiver); } catch { }
            }
        }

        float waitTime = Mathf.Max(momentDuration, this.duracion);
        yield return new WaitForSeconds(waitTime);

        DeactivateArea();

        // limpiar y notificar a targets
        var targets = new List<GameObject>(lastApplyTime.Keys);
        foreach (var t in targets)
        {
            try { t.SendMessage("RemoveDamageReductionFromArea", this.gameObject, SendMessageOptions.DontRequireReceiver); } catch { }
        }
        lastApplyTime.Clear();

        NotifyOwnerAreaDeactivated();

        momentaryRoutine = null;
    }

    private Collider[] GetOverlaps(Vector3 worldCenter)
    {
        if (col == null)
        {
            return Physics.OverlapSphere(worldCenter, 0.01f, capasAfectadas, QueryTriggerInteraction.Ignore);
        }

        if (col is SphereCollider sc)
        {
            Vector3 center = sc.transform.TransformPoint(sc.center);
            float radiusWorld = sc.radius * MaxScale(sc.transform);
            return Physics.OverlapSphere(center, Mathf.Max(0.001f, radiusWorld), capasAfectadas, QueryTriggerInteraction.Ignore);
        }
        else if (col is BoxCollider bc)
        {
            Vector3 center = bc.transform.TransformPoint(bc.center);
            Vector3 halfExtents = Vector3.Scale(bc.size * 0.5f, bc.transform.lossyScale);
            Quaternion orientation = bc.transform.rotation;
            return Physics.OverlapBox(center, Vector3.Max(halfExtents, Vector3.one * 0.001f), orientation, capasAfectadas, QueryTriggerInteraction.Ignore);
        }
        else if (col is CapsuleCollider cc)
        {
            Transform t = cc.transform;
            Vector3 centerLocal = cc.center;
            int dir = cc.direction;
            Vector3 axis = (dir == 0) ? Vector3.right : (dir == 1) ? Vector3.up : Vector3.forward;
            float radiusWorld = cc.radius * MaxScaleOnAxes(t, dir == 1 ? new int[] { 0, 2 } : new int[] { 0, 1, 2 });
            float halfSeg = Mathf.Max(0f, (cc.height * 0.5f) - cc.radius);
            Vector3 worldA = t.TransformPoint(centerLocal + axis * halfSeg);
            Vector3 worldB = t.TransformPoint(centerLocal - axis * halfSeg);
            return Physics.OverlapCapsule(worldA, worldB, Mathf.Max(0.001f, radiusWorld), capasAfectadas, QueryTriggerInteraction.Ignore);
        }
        else
        {
            Bounds b = col.bounds;
            return Physics.OverlapBox(b.center, b.extents, Quaternion.identity, capasAfectadas, QueryTriggerInteraction.Ignore);
        }
    }

    private float MaxScale(Transform t) => Mathf.Max(t.lossyScale.x, t.lossyScale.y, t.lossyScale.z);

    private float MaxScaleOnAxes(Transform t, int[] axes)
    {
        float max = 0f;
        foreach (var a in axes)
        {
            if (a == 0) max = Mathf.Max(max, t.lossyScale.x);
            else if (a == 1) max = Mathf.Max(max, t.lossyScale.y);
            else if (a == 2) max = Mathf.Max(max, t.lossyScale.z);
        }
        return Mathf.Max(0.0001f, max);
    }

    private void ApplyReductionTo(GameObject target, float percent, float dur)
    {
        if (target == null) return;

        float effectivePercent = Mathf.Clamp01(reduccionPercent);
        float effectiveDur = Mathf.Max(0f, duracion > 0f ? duracion : dur);

        bool invoked = false;

        // 1) intentar m�todo espec�fico ApplyDamageReductionFromArea en componentes
        foreach (var mb in target.GetComponents<MonoBehaviour>())
        {
            if (mb == null) continue;
            var miNew = mb.GetType().GetMethod("ApplyDamageReductionFromArea", BindingFlags.Public | BindingFlags.Instance);
            if (miNew != null)
            {
                try { miNew.Invoke(mb, new object[] { effectivePercent, effectiveDur, this.gameObject }); invoked = true; Debug.Log($"[ArmaduraAreaPrefab] Aplicada reducci�n FROM AREA ({effectivePercent}) a {target.name} via {mb.GetType().Name}.ApplyDamageReductionFromArea()"); break; }
                catch { }
            }
        }

        // 2) compatibilidad: si no exist�a m�todo nuevo, intentar ApplyDamageReduction tradicional
        if (!invoked)
        {
            foreach (var mb in target.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                if (mb is IDamageable)
                {
                    var mi = mb.GetType().GetMethod("ApplyDamageReduction", BindingFlags.Public | BindingFlags.Instance);
                    if (mi != null)
                    {
                        try { mi.Invoke(mb, new object[] { effectivePercent, effectiveDur }); invoked = true; Debug.Log($"[ArmaduraAreaPrefab] Aplicada reducci�n ({effectivePercent}) a {target.name} via {mb.GetType().Name}.ApplyDamageReduction()"); break; }
                        catch { }
                    }
                }
            }
        }

        // 3) intentos por nombre (legacy)
        if (!invoked)
        {
            var vidaEscudo = target.GetComponent("VidaEnemigoEscudo");
            if (vidaEscudo != null)
            {
                var miNew = vidaEscudo.GetType().GetMethod("ApplyDamageReductionFromArea", BindingFlags.Public | BindingFlags.Instance);
                if (miNew != null) { try { miNew.Invoke(vidaEscudo, new object[] { effectivePercent, effectiveDur, this.gameObject }); invoked = true; Debug.Log($"[ArmaduraAreaPrefab] Aplicada reducci�n FROM AREA a {target.name} via VidaEnemigoEscudo.ApplyDamageReductionFromArea()"); } catch { } }
                else
                {
                    var mi = vidaEscudo.GetType().GetMethod("ApplyDamageReduction", BindingFlags.Public | BindingFlags.Instance);
                    if (mi != null) { try { mi.Invoke(vidaEscudo, new object[] { effectivePercent, effectiveDur }); invoked = true; Debug.Log($"[ArmaduraAreaPrefab] Aplicada reducci�n (legacy) a {target.name} via VidaEnemigoEscudo.ApplyDamageReduction()"); } catch { } }
                }
            }
        }

        if (!invoked)
        {
            var enemyH = target.GetComponent("EnemyHealth");
            if (enemyH != null)
            {
                var miNew = enemyH.GetType().GetMethod("ApplyDamageReductionFromArea", BindingFlags.Public | BindingFlags.Instance);
                if (miNew != null) { try { miNew.Invoke(enemyH, new object[] { effectivePercent, effectiveDur, this.gameObject }); invoked = true; Debug.Log($"[ArmaduraAreaPrefab] Aplicada reducci�n FROM AREA a {target.name} via EnemyHealth.ApplyDamageReductionFromArea()"); } catch { } }
                else
                {
                    var mi = enemyH.GetType().GetMethod("ApplyDamageReduction", BindingFlags.Public | BindingFlags.Instance);
                    if (mi != null) { try { mi.Invoke(enemyH, new object[] { effectivePercent, effectiveDur }); invoked = true; Debug.Log($"[ArmaduraAreaPrefab] Aplicada reducci�n (legacy) a {target.name} via EnemyHealth.ApplyDamageReduction()"); } catch { } }
                }
            }
        }

        // 4) �ltimo recurso: intentar SendMessage al nuevo m�todo (si no fue invocado)
        if (!invoked)
        {
            try
            {
                target.SendMessage("ApplyDamageReductionFromArea", new object[] { effectivePercent, effectiveDur, this.gameObject }, SendMessageOptions.DontRequireReceiver);
                Debug.Log($"[ArmaduraAreaPrefab] SendMessage ApplyDamageReductionFromArea ({effectivePercent}) enviado a {target.name} como fallback.");
                invoked = true;
            }
            catch { }
        }

        // 5) si a�n no se invoc� nada, tambi�n intentar el SendMessage legacy para compatibilidad
        if (!invoked)
        {
            try
            {
                target.SendMessage("ApplyDamageReduction", new object[] { effectivePercent, effectiveDur }, SendMessageOptions.DontRequireReceiver);
                Debug.Log($"[ArmaduraAreaPrefab] SendMessage ApplyDamageReduction ({effectivePercent}) enviado a {target.name} como fallback final.");
            }
            catch { }
        }
    }

    private void NotifyOwnerAreaDeactivated()
    {
        if (owner != null)
        {
            try { owner.gameObject.SendMessage("OnAreaDeactivated", this.gameObject, SendMessageOptions.DontRequireReceiver); }
            catch { }
        }
    }

    /// <summary>
    /// Comprueba si la entidad pasada est� actualmente dentro del �rea de este prefab.
    /// M�todo p�blico utilizado por versiones previas (no obligado a usarse si se aplica el nuevo protocolo por SendMessage).
    /// </summary>
    public bool IsEntityInside(GameObject entity)
    {
        if (entity == null || col == null) return false;
        GameObject root = entity.transform.root != null ? entity.transform.root.gameObject : entity;
        if (root == null) return false;
        if (owner != null && root == owner.gameObject) return false;
        if (Mathf.Abs(root.transform.position.y - transform.position.y) > flattenHeightThreshold) return false;

        try { return col.bounds.Contains(root.transform.position); }
        catch { return false; }
    }
}

//// ArmaduraAreaPrefab.cs (a�adida funci�n IsEntityInside)
//using UnityEngine;
//using System.Collections;
//using System.Collections.Generic;
//using System.Reflection;

//[DisallowMultipleComponent]
//public class ArmaduraAreaPrefab : MonoBehaviour
//{
//    [Header("Opciones del efecto (ahora en el prefab)")]
//    [SerializeField, Range(0f, 1f)] private float reduccionPercent = 0.25f;
//    [SerializeField] private float duracion = 10f; // ahora �nico tiempo (duraci�n y m�nimo visible)

//    [Header("Comportamiento adicional")]
//    [Tooltip("Intervalo en segundos para reaplicar efecto a entidades dentro del �rea.")]
//    [SerializeField] private float applyInterval = 1f;

//    private Collider col;
//    private ArmaduraDemonicaArea owner;
//    private float flattenHeightThreshold = 1.2f;
//    private LayerMask capasAfectadas = ~0;

//    // corrutinas internas
//    private Coroutine lifecycleRoutine = null;    // activaciones completas
//    private Coroutine momentaryRoutine = null;    // activaciones moment�neas

//    // para reaplicar mientras est�n dentro
//    private Dictionary<GameObject, float> lastApplyTime = new Dictionary<GameObject, float>();

//    public float ReductionPercent { get => reduccionPercent; set => reduccionPercent = Mathf.Clamp01(value); }
//    public float Duration { get => duracion; set => duracion = Mathf.Max(0f, value); }

//    private void Awake()
//    {
//        col = GetComponent<Collider>();
//        if (col == null)
//        {
//            Debug.LogError($"[{name}] ArmaduraAreaPrefab requiere un Collider (Box/Capsule/Sphere) ya configurado en este GameObject.");
//        }
//        else
//        {
//            if (!col.isTrigger)
//            {
//                Debug.LogWarning($"[{name}] Collider no est� en modo trigger. Forzando isTrigger = true.");
//                col.isTrigger = true;
//            }
//        }
//    }

//    /// <summary>
//    /// Inicializa el prefab. Ajusta tama�o del collider seg�n 'radius' si es posible.
//    /// </summary>
//    public void Initialize(ArmaduraDemonicaArea owner, float radius, Color color, float thickness, float flattenHeight, LayerMask capas)
//    {
//        this.owner = owner;
//        this.flattenHeightThreshold = flattenHeight;
//        this.capasAfectadas = capas;

//        if (col == null)
//        {
//            col = GetComponent<Collider>();
//            if (col == null) Debug.LogWarning($"[{name}] Initialize: Collider ausente. No se puede ajustar tama�o.");
//        }

//        if (col is SphereCollider sc)
//        {
//            sc.radius = Mathf.Max(0.01f, radius);
//        }
//        else if (col is BoxCollider bc)
//        {
//            Vector3 size = bc.size;
//            float y = size.y;
//            bc.size = new Vector3(radius * 2f / transform.lossyScale.x, y, radius * 2f / transform.lossyScale.z);
//        }
//        else if (col is CapsuleCollider cc)
//        {
//            cc.radius = Mathf.Max(0.01f, radius / Mathf.Max(1f, transform.lossyScale.x));
//            cc.height = Mathf.Max(cc.height, (radius * 2f) / Mathf.Max(0.0001f, transform.lossyScale.y));
//        }
//    }

//    public void SetLayerMask(LayerMask m) { capasAfectadas = m; }
//    public void SetFlattenHeight(float h) { flattenHeightThreshold = h; }

//    public void SetRadius(float r)
//    {
//        if (col == null) { Debug.LogWarning($"[{name}] SetRadius llamado pero Collider ausente."); return; }

//        if (col is SphereCollider sc) sc.radius = Mathf.Max(0.01f, r);
//        else if (col is BoxCollider bc)
//        {
//            Vector3 size = bc.size;
//            bc.size = new Vector3(r * 2f / transform.lossyScale.x, size.y, r * 2f / transform.lossyScale.z);
//        }
//        else if (col is CapsuleCollider cc)
//        {
//            cc.radius = Mathf.Max(0.01f, r / Mathf.Max(0.0001f, transform.lossyScale.x));
//            cc.height = Mathf.Max(cc.height, (r * 2f) / Mathf.Max(0.0001f, transform.lossyScale.y));
//        }
//        else
//        {
//            Debug.LogWarning($"[{name}] SetRadius: tipo de Collider no soportado para ajuste autom�tico.");
//        }
//    }

//    public void ActivateArea() { if (col != null) col.enabled = true; else Debug.LogWarning($"[{name}] ActivateArea: Collider ausente."); }
//    public void DeactivateArea() { if (col != null) col.enabled = false; else Debug.LogWarning($"[{name}] DeactivateArea: Collider ausente."); }

//    private void OnTriggerEnter(Collider other) => HandleTriggerEnterOrStay(other);
//    private void OnTriggerStay(Collider other) => HandleTriggerEnterOrStay(other);

//    private void HandleTriggerEnterOrStay(Collider other)
//    {
//        if (col == null) return;
//        if (((1 << other.gameObject.layer) & capasAfectadas) == 0) return;

//        var root = other.transform.root != null ? other.transform.root.gameObject : other.gameObject;
//        if (root == null) return;
//        if (owner != null && root == owner.gameObject) return;
//        if (Mathf.Abs(root.transform.position.y - transform.position.y) > flattenHeightThreshold) return;

//        float tNow = Time.time;
//        if (!lastApplyTime.TryGetValue(root, out float last) || tNow - last >= applyInterval)
//        {
//            ApplyReductionTo(root, reduccionPercent, duracion);
//            lastApplyTime[root] = tNow;

//            if (owner != null)
//            {
//                try
//                {
//                    owner.gameObject.SendMessage("OnEntityEnterArea", root, SendMessageOptions.DontRequireReceiver);
//                }
//                catch { /* no bloquear si el m�todo no existe */ }
//            }
//        }
//    }

//    private void OnTriggerExit(Collider other)
//    {
//        var root = other.transform.root != null ? other.transform.root.gameObject : other.gameObject;
//        if (root != null && lastApplyTime.ContainsKey(root)) lastApplyTime.Remove(root);
//    }

//    public void ActivateFor(float duration, Vector3 center)
//    {
//        transform.position = center;

//        if (momentaryRoutine != null) { StopCoroutine(momentaryRoutine); momentaryRoutine = null; }
//        if (lifecycleRoutine != null) StopCoroutine(lifecycleRoutine);
//        lifecycleRoutine = StartCoroutine(LifecycleRoutine(duration, center));
//    }

//    private IEnumerator LifecycleRoutine(float duration, Vector3 center)
//    {
//        transform.position = center;
//        ActivateArea();

//        // aplicarlo al owner si existe
//        if (owner != null)
//        {
//            ApplyReductionTo(owner.gameObject, reduccionPercent, duration);
//            lastApplyTime[owner.gameObject] = Time.time;
//            try { owner.gameObject.SendMessage("OnEntityEnterArea", owner.gameObject, SendMessageOptions.DontRequireReceiver); } catch { }
//        }

//        Collider[] hits = GetOverlaps(center);
//        foreach (var c in hits)
//        {
//            GameObject root = c.transform.root != null ? c.transform.root.gameObject : c.gameObject;
//            if (owner != null && root == owner.gameObject) continue;
//            if (Mathf.Abs(root.transform.position.y - (owner != null ? owner.transform.position.y : center.y)) > flattenHeightThreshold) continue;
//            ApplyReductionTo(root, reduccionPercent, duration);
//            lastApplyTime[root] = Time.time;

//            if (owner != null)
//            {
//                try { owner.gameObject.SendMessage("OnEntityEnterArea", root, SendMessageOptions.DontRequireReceiver); } catch { }
//            }
//        }

//        yield return new WaitForSeconds(Mathf.Max(0.01f, duration));
//        DeactivateArea();

//        // Notificar al owner que esta instancia ya termin� su lifecycle (para que el owner pueda destruir/limpiar si corresponde)
//        NotifyOwnerAreaDeactivated();

//        lifecycleRoutine = null;
//    }

//    public void ActivateMomentaryAndApply(Vector3 center, float momentDuration)
//    {
//        transform.position = center;

//        if (lifecycleRoutine != null)
//        {
//            Collider[] hits = GetOverlaps(center);
//            foreach (var c in hits)
//            {
//                GameObject root = c.transform.root != null ? c.transform.root.gameObject : c.gameObject;
//                if (owner != null && root == owner.gameObject) continue;
//                if (Mathf.Abs(root.transform.position.y - (owner != null ? owner.transform.position.y : center.y)) > flattenHeightThreshold) continue;
//                ApplyReductionTo(root, reduccionPercent, duracion);
//                lastApplyTime[root] = Time.time;

//                if (owner != null)
//                {
//                    try { owner.gameObject.SendMessage("OnEntityEnterArea", root, SendMessageOptions.DontRequireReceiver); } catch { }
//                }
//            }
//            return;
//        }

//        if (momentaryRoutine != null) StopCoroutine(momentaryRoutine);
//        momentaryRoutine = StartCoroutine(MomentaryRoutine(center, Mathf.Max(0.01f, momentDuration)));
//    }

//    private IEnumerator MomentaryRoutine(Vector3 center, float momentDuration)
//    {
//        transform.position = center;
//        ActivateArea();

//        if (owner != null)
//        {
//            ApplyReductionTo(owner.gameObject, reduccionPercent, duracion);
//            lastApplyTime[owner.gameObject] = Time.time;
//            try { owner.gameObject.SendMessage("OnEntityEnterArea", owner.gameObject, SendMessageOptions.DontRequireReceiver); } catch { }
//        }

//        Collider[] hits = GetOverlaps(center);
//        foreach (var c in hits)
//        {
//            GameObject root = c.transform.root != null ? c.transform.root.gameObject : c.gameObject;
//            if (owner != null && root == owner.gameObject) continue;
//            if (Mathf.Abs(root.transform.position.y - (owner != null ? owner.transform.position.y : center.y)) > flattenHeightThreshold) continue;
//            ApplyReductionTo(root, reduccionPercent, duracion);
//            lastApplyTime[root] = Time.time;

//            if (owner != null)
//            {
//                try { owner.gameObject.SendMessage("OnEntityEnterArea", root, SendMessageOptions.DontRequireReceiver); } catch { }
//            }
//        }

//        // ahora usamos la duraci�n del prefab como m�nimo visible
//        float waitTime = Mathf.Max(momentDuration, this.duracion);
//        yield return new WaitForSeconds(waitTime);

//        DeactivateArea();

//        // Notificar al owner que esta instancia termin� su momento
//        NotifyOwnerAreaDeactivated();

//        momentaryRoutine = null;
//    }

//    private Collider[] GetOverlaps(Vector3 worldCenter)
//    {
//        if (col == null)
//        {
//            return Physics.OverlapSphere(worldCenter, 0.01f, capasAfectadas, QueryTriggerInteraction.Ignore);
//        }

//        if (col is SphereCollider sc)
//        {
//            Vector3 center = sc.transform.TransformPoint(sc.center);
//            float radiusWorld = sc.radius * MaxScale(sc.transform);
//            return Physics.OverlapSphere(center, Mathf.Max(0.001f, radiusWorld), capasAfectadas, QueryTriggerInteraction.Ignore);
//        }
//        else if (col is BoxCollider bc)
//        {
//            Vector3 center = bc.transform.TransformPoint(bc.center);
//            Vector3 halfExtents = Vector3.Scale(bc.size * 0.5f, bc.transform.lossyScale);
//            Quaternion orientation = bc.transform.rotation;
//            return Physics.OverlapBox(center, Vector3.Max(halfExtents, Vector3.one * 0.001f), orientation, capasAfectadas, QueryTriggerInteraction.Ignore);
//        }
//        else if (col is CapsuleCollider cc)
//        {
//            Transform t = cc.transform;
//            Vector3 centerLocal = cc.center;
//            int dir = cc.direction;
//            Vector3 axis = (dir == 0) ? Vector3.right : (dir == 1) ? Vector3.up : Vector3.forward;
//            float radiusWorld = cc.radius * MaxScaleOnAxes(t, dir == 1 ? new int[] { 0, 2 } : new int[] { 0, 1, 2 });
//            float halfSeg = Mathf.Max(0f, (cc.height * 0.5f) - cc.radius);
//            Vector3 worldA = t.TransformPoint(centerLocal + axis * halfSeg);
//            Vector3 worldB = t.TransformPoint(centerLocal - axis * halfSeg);
//            return Physics.OverlapCapsule(worldA, worldB, Mathf.Max(0.001f, radiusWorld), capasAfectadas, QueryTriggerInteraction.Ignore);
//        }
//        else
//        {
//            Bounds b = col.bounds;
//            return Physics.OverlapBox(b.center, b.extents, Quaternion.identity, capasAfectadas, QueryTriggerInteraction.Ignore);
//        }
//    }

//    private float MaxScale(Transform t) => Mathf.Max(t.lossyScale.x, t.lossyScale.y, t.lossyScale.z);

//    private float MaxScaleOnAxes(Transform t, int[] axes)
//    {
//        float max = 0f;
//        foreach (var a in axes)
//        {
//            if (a == 0) max = Mathf.Max(max, t.lossyScale.x);
//            else if (a == 1) max = Mathf.Max(max, t.lossyScale.y);
//            else if (a == 2) max = Mathf.Max(max, t.lossyScale.z);
//        }
//        return Mathf.Max(0.0001f, max);
//    }

//    private void ApplyReductionTo(GameObject target, float percent, float dur)
//    {
//        if (target == null) return;

//        // 1) buscar componentes MonoBehaviour que implementen IDamageable y tengan ApplyDamageReduction
//        foreach (var mb in target.GetComponents<MonoBehaviour>())
//        {
//            if (mb == null) continue;
//            if (mb is IDamageable)
//            {
//                var mi = mb.GetType().GetMethod("ApplyDamageReduction", BindingFlags.Public | BindingFlags.Instance);
//                if (mi != null)
//                {
//                    try
//                    {
//                        mi.Invoke(mb, new object[] { percent, dur });
//                        Debug.Log($"[ArmaduraAreaPrefab] Aplicada reducci�n a {target.name} v�a {mb.GetType().Name}.ApplyDamageReduction()");
//                        return;
//                    }
//                    catch { }
//                }
//            }
//        }

//        // 2) intentar por nombre componentes concretos
//        var vidaEscudo = target.GetComponent("VidaEnemigoEscudo");
//        if (vidaEscudo != null)
//        {
//            var mi = vidaEscudo.GetType().GetMethod("ApplyDamageReduction", BindingFlags.Public | BindingFlags.Instance);
//            if (mi != null)
//            {
//                try { mi.Invoke(vidaEscudo, new object[] { percent, dur }); Debug.Log($"[ArmaduraAreaPrefab] Aplicada reducci�n a {target.name} v�a VidaEnemigoEscudo.ApplyDamageReduction()"); return; }
//                catch { }
//            }
//        }

//        var enemyH = target.GetComponent("EnemyHealth");
//        if (enemyH != null)
//        {
//            var mi = enemyH.GetType().GetMethod("ApplyDamageReduction", BindingFlags.Public | BindingFlags.Instance);
//            if (mi != null)
//            {
//                try { mi.Invoke(enemyH, new object[] { percent, dur }); Debug.Log($"[ArmaduraAreaPrefab] Aplicada reducci�n a {target.name} v�a EnemyHealth.ApplyDamageReduction()"); return; }
//                catch { }
//            }
//        }

//        // 3) fallback: SendMessage
//        try
//        {
//            target.SendMessage("ApplyDamageReduction", new object[] { percent, dur }, SendMessageOptions.DontRequireReceiver);
//            Debug.Log($"[ArmaduraAreaPrefab] SendMessage ApplyDamageReduction enviado a {target.name} como fallback.");
//        }
//        catch { }
//    }

//    /// <summary>
//    /// Env�a un SendMessage al owner para avisar que esta instancia de �rea ha terminado y puede ser destruida/limpiada por el owner.
//    /// Se manda la referencia a este GameObject (prefab instance).
//    /// </summary>
//    private void NotifyOwnerAreaDeactivated()
//    {
//        if (owner != null)
//        {
//            try
//            {
//                owner.gameObject.SendMessage("OnAreaDeactivated", this.gameObject, SendMessageOptions.DontRequireReceiver);
//            }
//            catch { }
//        }
//    }

//    /// <summary>
//    /// Comprueba si la entidad pasada est� actualmente dentro del �rea de este prefab.
//    /// M�todo p�blico utilizado por VidaEnemigoEscudo para validar "presencia en �rea".
//    /// Usamos col.bounds como comprobaci�n general (suficiente como aproximaci�n).
//    /// </summary>
//    public bool IsEntityInside(GameObject entity)
//    {
//        if (entity == null || col == null) return false;
//        GameObject root = entity.transform.root != null ? entity.transform.root.gameObject : entity;
//        if (root == null) return false;
//        if (owner != null && root == owner.gameObject) return false;
//        if (Mathf.Abs(root.transform.position.y - transform.position.y) > flattenHeightThreshold) return false;

//        // Intento razonable: usar bounds del collider para verificar si la posici�n del root est� dentro.
//        try
//        {
//            return col.bounds.Contains(root.transform.position);
//        }
//        catch
//        {
//            return false;
//        }
//    }
//}
