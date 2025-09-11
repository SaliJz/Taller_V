using System;
using System.Collections;
using UnityEngine;

public class Bruja_Health : MonoBehaviour
{
    [Header("Vida")]
    public int maxHealth = 20;
    public int currentHealth;

    [Header("Deteccion de golpes rapidos")]
    public int hitThreshold = 3;            // cuantas veces golpeada seguidas para ponerse 'overwhelmed'
    public float hitCountWindow = 1.2f;     // ventana de tiempo para contar golpes rapidos
    public float recoveryNoHitTime = 2.0f;  // tiempo sin golpes para recuperarse del estado overwhelmed

    // callbacks que otros componentes pueden suscribirse
    public Action OnDamaged;       // se invoca cada vez que recibe danio
    public Action OnInterrupted;   // se invoca cuando recibe danio durante ataque (interrumpe)
    public Action OnOverwhelmed;   // se invoca cuando se alcanza hitThreshold
    public Action OnRecovered;     // se invoca cuando sale del estado overwhelmed

    int recentHits = 0;
    float lastHitTime = -999f;
    bool isOverwhelmed = false;
    Coroutine recoveryCoroutine;

    void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int amount)
    {
        if (currentHealth <= 0) return;

        currentHealth -= amount;
        currentHealth = Mathf.Max(0, currentHealth);

        // notificar dano
        OnDamaged?.Invoke();

        // conteo de golpes rapidos
        if (Time.time - lastHitTime <= hitCountWindow)
        {
            recentHits++;
        }
        else
        {
            recentHits = 1;
        }
        lastHitTime = Time.time;

        // si alcanza threshold y no estaba en overwhelmed
        if (!isOverwhelmed && recentHits >= hitThreshold)
        {
            isOverwhelmed = true;
            OnOverwhelmed?.Invoke();
            // iniciar rutina de espera a que no reciba mas golpes
            if (recoveryCoroutine != null) StopCoroutine(recoveryCoroutine);
            recoveryCoroutine = StartCoroutine(WaitForNoHitsThenRecover());
        }

        // si es durante un ataque, podemos notificar interrupcion
        OnInterrupted?.Invoke();

        // si llega a 0 vida -> morir (simple)
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    IEnumerator WaitForNoHitsThenRecover()
    {
        // esperar a que pasen recoveryNoHitTime sin que se actualice lastHitTime
        while (Time.time - lastHitTime < recoveryNoHitTime)
        {
            yield return null;
        }

        // recuperar estado
        isOverwhelmed = false;
        recentHits = 0;
        OnRecovered?.Invoke();
    }

    void Die()
    {
        // placeholder: desactivar enemigo
        gameObject.SetActive(false);
    }

    // util para saber si actualmente esta overwhelmed
    public bool IsOverwhelmed()
    {
        return isOverwhelmed;
    }
}
