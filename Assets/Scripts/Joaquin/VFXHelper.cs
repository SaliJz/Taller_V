using System.Collections;
using UnityEngine;

/// <summary>
/// Utilidad centralizada para detener y destruir ParticleSystems.
/// </summary>
public static class VFXHelper
{
    // Runner persistente

    private static VFXHelperRunner runner;

    private static VFXHelperRunner Runner
    {
        get
        {
            if (runner != null) return runner;

            var go = new GameObject("[VFXHelperRunner]")
            {
                hideFlags = HideFlags.HideAndDontSave   // invisible en Hierarchy
            };
            Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<VFXHelperRunner>();
            return runner;
        }
    }

    // API pública

    /// <summary>
    /// Reproduce un ParticleSystem de forma segura si el GameObject está activo.
    /// </summary>
    public static void SafePlay(ParticleSystem ps)
    {
        if (ps == null || !ps.gameObject.activeInHierarchy) return;
        ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.Clear(false);
        ps.Play();
    }

    /// <summary>
    /// Detiene un ParticleSystem de forma segura.
    /// </summary>
    /// <param name="clear">Si true, limpia las partículas existentes.</param>
    public static void SafeStop(ParticleSystem ps, bool clear = false)
    {
        if (ps == null) return;
        var stopBehavior = clear
            ? ParticleSystemStopBehavior.StopEmittingAndClear
            : ParticleSystemStopBehavior.StopEmitting;
        ps.Stop(false, stopBehavior);
        if (clear) ps.Clear(false);
    }

    /// <summary>
    /// Activa o desactiva la emisión de un TrailRenderer de forma segura.
    /// </summary>
    public static void SafeSetEmitting(TrailRenderer trail, bool emitting)
    {
        if (trail == null) return;
        trail.emitting = emitting;
        if (!emitting) trail.Clear();
    }

    /// <summary>
    /// Para el ParticleSystem de forma segura y destruye su GameObject.
    /// La corrutina corre en el runner persistente: nunca se invalida aunque
    /// el objeto padre del PS sea destruido entre frames.
    /// </summary>
    /// <param name="ps">ParticleSystem a detener y destruir.</param>
    /// <param name="extraDelay">Segundos adicionales antes de Destroy (0 por defecto).</param>
    public static void StopAndDestroy(ParticleSystem ps, float extraDelay = 0f)
    {
        if (ps == null) return;
        Runner.Run(StopAndDestroyRoutine(ps, extraDelay));
    }

    /// <summary>
    /// Desvincula el PS de su padre, lo reproduce, lo para de forma segura
    /// y destruye su GameObject.  Usar cuando el objeto padre se va a destruir
    /// inmediatamente después.
    /// </summary>
    /// <param name="ps">ParticleSystem a desvincular, reproducir y destruir.</param>
    /// <param name="extraDelay">Segundos adicionales antes de Destroy.</param>
    public static void DetachStopAndDestroy(ParticleSystem ps, float extraDelay = 0f)
    {
        if (ps == null) return;
        ps.transform.SetParent(null);   // desvincula antes de ceder el control
        ps.Play();                      // reproducir el efecto one-shot
        Runner.Run(StopAndDestroyRoutine(ps, extraDelay));
    }

    // Corrutina interna

    private static IEnumerator StopAndDestroyRoutine(ParticleSystem ps, float extraDelay)
    {
        if (ps == null) yield break;

        // 1. Detener emisión y limpiar partículas existentes en este frame
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // 2. Esperar 2 frames para que el Job Scheduler libere los NativeArrays
        //    internos del ParticleSystem antes del Destroy
        yield return null;
        yield return null;

        // 3. Delay extra opcional
        if (extraDelay > 0f)
            yield return new WaitForSeconds(extraDelay);

        // 4. Destruir — el PS puede haber sido destruido externamente; la
        //    comprobación evita un MissingReferenceException inocuo
        if (ps != null) Object.Destroy(ps.gameObject);
    }
}

/// <summary>
/// MonoBehaviour auxiliar del VFXHelper.
/// Creado y gestionado automáticamente; no añadir manualmente a la escena.
/// </summary>
internal class VFXHelperRunner : MonoBehaviour
{
    public void Run(IEnumerator routine) => StartCoroutine(routine);
}