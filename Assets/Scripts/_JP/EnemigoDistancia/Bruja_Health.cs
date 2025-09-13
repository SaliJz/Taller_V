// Bruja_Health.cs
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Bruja_Health : MonoBehaviour
{
    [Header("Vidas por capas")]
    public int firstMaxLife = 20;   // primera vida (azul)
    public int secondMaxLife = 20;  // segunda vida (roja)

    // valores internos
    int firstCurrent;
    int secondCurrent;

    [Header("Deteccion de golpes rapidos")]
    public int hitThreshold = 3;
    public float hitCountWindow = 1.2f;
    public float recoveryNoHitTime = 2.0f;

    [Header("Configuración de Escudo")]
    public int escudoDamage = 25;

    [Header("UI - Sliders")]
    public Slider firstLifeSlider;    // asignar slider azul (primera vida)
    public Slider secondLifeSlider;   // asignar slider rojo (segunda vida)
    public Image firstFillImage;      // opcional: imagen del fill para colorear (azul)
    public Image secondFillImage;     // opcional: imagen del fill para colorear (rojo)

    // callbacks
    public Action OnDamaged;
    public Action OnInterrupted;
    public Action OnOverwhelmed;
    public Action OnRecovered;

    int recentHits = 0;
    float lastHitTime = -999f;
    bool isOverwhelmed = false;
    Coroutine recoveryCoroutine;

    // helpers
    public int MaxHealth { get { return firstMaxLife + secondMaxLife; } }
    public int CurrentHealth { get { return Mathf.Max(0, firstCurrent) + Mathf.Max(0, secondCurrent); } }

    void Awake()
    {
        firstCurrent = firstMaxLife;
        secondCurrent = secondMaxLife;

        // configurar sliders de forma segura
        if (firstLifeSlider != null)
        {
            firstLifeSlider.maxValue = Mathf.Max(1, firstMaxLife);
            firstLifeSlider.minValue = 0;
            firstLifeSlider.value = Mathf.Clamp(firstCurrent, 0, firstMaxLife);
            firstLifeSlider.gameObject.SetActive(true);
        }
        if (secondLifeSlider != null)
        {
            secondLifeSlider.maxValue = Mathf.Max(1, secondMaxLife);
            secondLifeSlider.minValue = 0;
            secondLifeSlider.value = Mathf.Clamp(secondCurrent, 0, secondMaxLife);
            secondLifeSlider.gameObject.SetActive(false); // empieza desactivada
        }

        if (firstFillImage != null)
        {
            firstFillImage.color = Color.blue;
            firstFillImage.gameObject.SetActive(true);
        }
        if (secondFillImage != null)
        {
            secondFillImage.color = Color.red;
            secondFillImage.gameObject.SetActive(false);
        }
    }

    // Aplica daño repartido entre primera y segunda vida
    public void TakeDamage(int amount)
    {
        if (amount <= 0) return;                 // proteger contra daño no válido
        if (CurrentHealth <= 0) return;          // ya muerto -> ignorar

        int remaining = amount;

        // aplicar a primera vida
        if (firstCurrent > 0)
        {
            int taken = Mathf.Min(firstCurrent, remaining);
            firstCurrent -= taken;
            remaining -= taken;
        }

        // overflow a segunda vida
        if (remaining > 0 && secondCurrent > 0)
        {
            int taken2 = Mathf.Min(secondCurrent, remaining);
            secondCurrent -= taken2;
            remaining -= taken2;
        }

        // actualizar UI
        UpdateSlidersSafely();

        // notificar daño y lógica previa
        OnDamaged?.Invoke();

        if (Time.time - lastHitTime <= hitCountWindow)
        {
            recentHits++;
        }
        else
        {
            recentHits = 1;
        }
        lastHitTime = Time.time;

        if (!isOverwhelmed && recentHits >= hitThreshold)
        {
            isOverwhelmed = true;
            OnOverwhelmed?.Invoke();
            if (recoveryCoroutine != null) StopCoroutine(recoveryCoroutine);
            recoveryCoroutine = StartCoroutine(WaitForNoHitsThenRecover());
        }

        OnInterrupted?.Invoke();

        // destruir / desactivar sólo si la vida total es 0 o menos
        if (CurrentHealth <= 0)
        {
            Die();
        }

        Debug.Log($"{name} recibió {amount} de daño. Vida1={firstCurrent}/{firstMaxLife} Vida2={secondCurrent}/{secondMaxLife} Total={CurrentHealth}/{MaxHealth}");
    }

    IEnumerator WaitForNoHitsThenRecover()
    {
        while (Time.time - lastHitTime < recoveryNoHitTime)
        {
            yield return null;
        }

        isOverwhelmed = false;
        recentHits = 0;
        OnRecovered?.Invoke();
    }

    void Die()
    {
        // solo desactivar (puedes cambiar a Destroy si quieres)
        gameObject.SetActive(false);
    }

    public bool IsOverwhelmed()
    {
        return isOverwhelmed;
    }

    void UpdateSlidersSafely()
    {
        if (firstLifeSlider != null)
        {
            firstLifeSlider.maxValue = Mathf.Max(1, firstMaxLife);
            firstLifeSlider.value = Mathf.Clamp(firstCurrent, 0, firstMaxLife);
            if (!firstLifeSlider.gameObject.activeSelf) firstLifeSlider.gameObject.SetActive(true);
        }
        if (firstFillImage != null)
        {
            if (!firstFillImage.gameObject.activeSelf) firstFillImage.gameObject.SetActive(true);
            firstFillImage.color = Color.blue;
        }

        bool shouldActivateSecond = (firstCurrent <= 0) && (secondMaxLife > 0);
        if (secondLifeSlider != null)
        {
            if (shouldActivateSecond)
            {
                if (!secondLifeSlider.gameObject.activeSelf) secondLifeSlider.gameObject.SetActive(true);
                secondLifeSlider.maxValue = Mathf.Max(1, secondMaxLife);
                secondLifeSlider.value = Mathf.Clamp(secondCurrent, 0, secondMaxLife);
            }
            else
            {
                if (secondLifeSlider.gameObject.activeSelf) secondLifeSlider.gameObject.SetActive(false);
            }
        }

        if (secondFillImage != null)
        {
            if (shouldActivateSecond)
            {
                if (!secondFillImage.gameObject.activeSelf) secondFillImage.gameObject.SetActive(true);
                secondFillImage.color = Color.red;
            }
            else
            {
                if (secondFillImage.gameObject.activeSelf) secondFillImage.gameObject.SetActive(false);
            }
        }
    }

    // Triggers para detectar objetos con tag "Escudo"
    void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        if (other.CompareTag("Escudo"))
        {
            TakeDamage(escudoDamage);
            Debug.Log($"{name} recibió {escudoDamage} de daño por Escudo (3D). Vida total ahora: {CurrentHealth}/{MaxHealth}");
        }
    }

   
}


//using System;
//using System.Collections;
//using UnityEngine;

//public class Bruja_Health : MonoBehaviour
//{
//    [Header("Vida")]
//    public int maxHealth = 20;
//    public int currentHealth;

//    [Header("Deteccion de golpes rapidos")]
//    public int hitThreshold = 3;            // cuantas veces golpeada seguidas para ponerse 'overwhelmed'
//    public float hitCountWindow = 1.2f;     // ventana de tiempo para contar golpes rapidos
//    public float recoveryNoHitTime = 2.0f;  // tiempo sin golpes para recuperarse del estado overwhelmed

//    // callbacks que otros componentes pueden suscribirse
//    public Action OnDamaged;       // se invoca cada vez que recibe danio
//    public Action OnInterrupted;   // se invoca cuando recibe danio durante ataque (interrumpe)
//    public Action OnOverwhelmed;   // se invoca cuando se alcanza hitThreshold
//    public Action OnRecovered;     // se invoca cuando sale del estado overwhelmed

//    int recentHits = 0;
//    float lastHitTime = -999f;
//    bool isOverwhelmed = false;
//    Coroutine recoveryCoroutine;

//    void Awake()
//    {
//        currentHealth = maxHealth;
//    }

//    public void TakeDamage(int amount)
//    {
//        if (currentHealth <= 0) return;

//        currentHealth -= amount;
//        currentHealth = Mathf.Max(0, currentHealth);

//        // notificar dano
//        OnDamaged?.Invoke();

//        // conteo de golpes rapidos
//        if (Time.time - lastHitTime <= hitCountWindow)
//        {
//            recentHits++;
//        }
//        else
//        {
//            recentHits = 1;
//        }
//        lastHitTime = Time.time;

//        // si alcanza threshold y no estaba en overwhelmed
//        if (!isOverwhelmed && recentHits >= hitThreshold)
//        {
//            isOverwhelmed = true;
//            OnOverwhelmed?.Invoke();
//            // iniciar rutina de espera a que no reciba mas golpes
//            if (recoveryCoroutine != null) StopCoroutine(recoveryCoroutine);
//            recoveryCoroutine = StartCoroutine(WaitForNoHitsThenRecover());
//        }

//        // si es durante un ataque, podemos notificar interrupcion
//        OnInterrupted?.Invoke();

//        // si llega a 0 vida -> morir (simple)
//        if (currentHealth <= 0)
//        {
//            Die();
//        }
//    }

//    IEnumerator WaitForNoHitsThenRecover()
//    {
//        // esperar a que pasen recoveryNoHitTime sin que se actualice lastHitTime
//        while (Time.time - lastHitTime < recoveryNoHitTime)
//        {
//            yield return null;
//        }

//        // recuperar estado
//        isOverwhelmed = false;
//        recentHits = 0;
//        OnRecovered?.Invoke();
//    }

//    void Die()
//    {
//        // placeholder: desactivar enemigo
//        gameObject.SetActive(false);
//    }

//    // util para saber si actualmente esta overwhelmed
//    public bool IsOverwhelmed()
//    {
//        return isOverwhelmed;
//    }
//}
