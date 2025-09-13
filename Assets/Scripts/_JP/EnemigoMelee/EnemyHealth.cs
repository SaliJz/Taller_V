// EnemyHealth.cs
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Componente de vida que delega en IDamageable del mismo GameObject.
/// Ahora con 2 capas de vida + sliders (primera = azul, segunda = roja).
/// FIX: evita destrucción prematura y evita doble-aplicación de daño al controller
/// </summary>
public class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("Vidas por capas")]
    public int firstMaxLife = 10;   // primera vida (azul)
    public int secondMaxLife = 0;   // segunda vida (roja)

    int firstCurrent;
    int secondCurrent;

    [Header("Referencias")]
    public MeleeEnemyController controller;

    [Header("Configuración Escudo")]
    public int escudoDamage = 25;

    [Header("UI - Sliders")]
    public Slider firstLifeSlider;    // slider azul
    public Slider secondLifeSlider;   // slider rojo
    public Image firstFillImage;      // opcional: fill image (azul)
    public Image secondFillImage;     // opcional: fill image (rojo)

    [Header("Opciones")]
    [Tooltip("Si está activado, llamará controller.TakeDamage(amount). Desactiva si tu controller aplica vida también (evita doble daño).")]
    public bool notifyControllerOnDamage = false;

    void Awake()
    {
        firstCurrent = firstMaxLife;
        secondCurrent = secondMaxLife;

        if (controller == null) controller = GetComponent<MeleeEnemyController>();

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

    // implementa IDamageable
    public void TakeDamage(int amount)
    {
        if (amount <= 0) return;
        if (GetTotalLife() <= 0) return; // ya muerto -> ignorar

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

        UpdateSlidersSafely();

        Debug.Log($"{name} recibió {amount} de daño. Vida1={firstCurrent}/{firstMaxLife} Vida2={secondCurrent}/{secondMaxLife}");

        // opcional: notificar al controller (DESACTIVADO por defecto para evitar doble daño)
        if (notifyControllerOnDamage && controller != null)
        {
            // llama solo si realmente quieres que el controller aplique lógica extra
            controller.TakeDamage(amount);
        }
        else
        {
            // si el controller necesita saber del golpe sin aplicar vida, envía un mensaje no obligatorio
            // (no aplicará daño adicional si no implementas OnEnemyDamaged en el controller)
            if (controller != null)
            {
                controller.SendMessage("OnEnemyDamaged", amount, SendMessageOptions.DontRequireReceiver);
            }
        }

        // destruir sólo si la vida total es 0 o menos
        if (GetTotalLife() <= 0)
        {
            Destroy(gameObject, 0.1f);
        }
    }

    int GetTotalLife()
    {
        return Mathf.Max(0, firstCurrent) + Mathf.Max(0, secondCurrent);
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

    // Triggers para "Escudo"
    void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        if (other.CompareTag("Escudo"))
        {
            TakeDamage(escudoDamage);
            Debug.Log($"{name} recibió {escudoDamage} de daño por Escudo (3D). Vida total ahora: {GetTotalLife()}");
        }
    }

   
}


//// EnemyHealth.cs
//using UnityEngine;

///// <summary>
///// Ejemplo de componente de vida que delega en IDamageable del mismo GameObject.
///// Útil si quieres separar lógica de salud de controlador.
///// Añadido: detección de objetos con tag "Escudo" en OnTrigger (3D y 2D).
///// </summary>
//public class EnemyHealth : MonoBehaviour, IDamageable
//{
//    [Header("Vida")]
//    public int maxHealth = 10;
//    int current;

//    [Header("Referencias")]
//    public MeleeEnemyController controller;

//    [Header("Configuración Escudo")]
//    public int escudoDamage = 25; // daño que aplica un objeto con tag "Escudo"

//    void Awake()
//    {
//        current = maxHealth;
//        if (controller == null) controller = GetComponent<MeleeEnemyController>();
//    }

//    public void TakeDamage(int amount)
//    {
//        current -= amount;
//        Debug.Log($"{name} recibió {amount} de daño. HP={current}/{maxHealth}");

//        if (controller != null) controller.TakeDamage(amount);

//        if (current <= 0)
//        {
//            Destroy(gameObject, 0.1f);
//        }
//    }

//    // --- Triggers para detectar objetos con tag "Escudo" ---
//    // Física 3D
//    void OnTriggerEnter(Collider other)
//    {
//        if (other == null) return;
//        if (other.CompareTag("Escudo"))
//        {
//            TakeDamage(escudoDamage);
//            Debug.Log($"{name} recibió {escudoDamage} de daño por Escudo (3D). HP={current}/{maxHealth}");
//        }
//    }


//}


