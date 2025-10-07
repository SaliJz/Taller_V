using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// RalentizadorArea:
/// - Aplica una ralentizaci�n al PlayerMovement cuando entra en el trigger.
/// - Soporta:
///   * ralentizaci�n mientras est� dentro (si useDuration == false)
///   * ralentizaci�n por duraci�n fija (si useDuration == true)
///   * m�ltiples jugadores y solapes (stack multiplicativo)
/// - Requiere que el jugador tenga un componente PlayerMovement con la propiedad MoveSpeed p�blica.
/// </summary>
[DisallowMultipleComponent]
public class RalentizadorArea : MonoBehaviour
{
    [Header("Ajustes de ralentizaci�n")]
    [Tooltip("Multiplicador aplicado al MoveSpeed (0.5 = 50% velocidad).")]
    [Range(0f, 1f)]
    public float slowMultiplier = 0.5f;

    [Tooltip("Si true, la ralentizaci�n durar� 'duration' segundos y no ser� removida por salir.")]
    public bool useDuration = false;
    [Tooltip("Duraci�n en segundos de la ralentizaci�n cuando useDuration == true.")]
    public float duration = 3f;

    [Tooltip("Si true, registra s�lo objetos con tag 'Player' (m�s seguro). Si false, aplica a cualquier objeto que tenga PlayerMovement.")]
    public bool requirePlayerTag = true;

    // Estado por jugador
    private class SlowState
    {
        public float baseSpeed;
        public List<float> multipliers = new List<float>(); // lista de multiplicadores activos
    }

    private Dictionary<GameObject, SlowState> states = new Dictionary<GameObject, SlowState>();

    private void Awake()
    {
        if (duration < 0f) duration = 0f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (requirePlayerTag && !other.gameObject.CompareTag("Player")) return;

        GameObject go = other.gameObject;
        PlayerMovement pm = go.GetComponent<PlayerMovement>();
        if (pm == null) return;

        if (!states.TryGetValue(go, out SlowState st))
        {
            st = new SlowState();
            st.baseSpeed = Mathf.Max(0.01f, pm.MoveSpeed); // capturar velocidad base actual
            states.Add(go, st);
        }

        // A�adir multiplicador y recalcular velocidad
        st.multipliers.Add(slowMultiplier);
        ApplyEffectiveSpeed(pm, st);

        // Si es por duraci�n, arrancamos coroutine que eliminar� un multiplicador al terminar
        if (useDuration && duration > 0f)
        {
            StartCoroutine(TimedRemove(go, slowMultiplier, duration));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (requirePlayerTag && !other.gameObject.CompareTag("Player")) return;

        // Si estamos usando duraci�n, salir no quita la ralentizaci�n (dependiendo de tu intenci�n).
        if (useDuration) return;

        GameObject go = other.gameObject;
        PlayerMovement pm = go.GetComponent<PlayerMovement>();
        if (pm == null) return;

        if (states.TryGetValue(go, out SlowState st))
        {
            // remover una instancia del multiplicador (si hay varias, s�lo una)
            bool removed = st.multipliers.Remove(slowMultiplier);
            if (removed)
            {
                ApplyEffectiveSpeed(pm, st);
            }

            // si ya no quedan multiplicadores, restaurar y quitar estado
            if (st.multipliers.Count == 0)
            {
                pm.MoveSpeed = st.baseSpeed;
                states.Remove(go);
            }
        }
    }

    private IEnumerator TimedRemove(GameObject go, float multiplier, float wait)
    {
        yield return new WaitForSeconds(wait);

        // Si el objeto ya fue destruido / desconectado, terminar
        if (go == null) yield break;

        PlayerMovement pm = go.GetComponent<PlayerMovement>();
        if (pm == null) yield break;

        if (states.TryGetValue(go, out SlowState st))
        {
            // Remueve una instancia del multiplicador (si hay varias, s�lo una)
            bool removed = st.multipliers.Remove(multiplier);
            if (removed)
            {
                ApplyEffectiveSpeed(pm, st);
            }

            if (st.multipliers.Count == 0)
            {
                pm.MoveSpeed = st.baseSpeed;
                states.Remove(go);
            }
        }
    }

    // Calcula el multiplicador efectivo (productoria) y aplica MoveSpeed
    private void ApplyEffectiveSpeed(PlayerMovement pm, SlowState st)
    {
        float effective = 1f;
        for (int i = 0; i < st.multipliers.Count; i++)
        {
            effective *= Mathf.Clamp01(st.multipliers[i]);
        }
        float newSpeed = st.baseSpeed * Mathf.Max(0.01f, effective);
        pm.MoveSpeed = newSpeed;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        slowMultiplier = Mathf.Clamp(slowMultiplier, 0f, 1f);
        if (duration < 0f) duration = 0f;
    }
#endif
}
