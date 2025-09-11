using UnityEngine;
using System.Collections;

/// <summary>
/// Clase que maneja el ataque cuerpo a cuerpo del jugador.
/// </summary>
public class PlayerMeleeAttack : MonoBehaviour
{
    [Header("Configuraci�n de Ataque")]
    [SerializeField] private Transform hitPoint;
    [SerializeField] private float hitRadius = 0.8f;
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private LayerMask enemyLayer;

    [SerializeField] private bool showGizmo = false;
    [SerializeField] private float gizmoDuration = 0.2f;
    //private Animator animator;

    private void Start()
    {
        //animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Attack();
        }
    }

    private void Attack()
    {
        //animator.SetTrigger("Attack");
        PerformHitDetection();
    }

    // FUNCI�N LLAMADA POR UN ANIMATION EVENT
    /// <summary>
    /// Funci�n que realiza la detecci�n de golpes en un �rea definida alrededor del punto de impacto.
    /// </summary>
    public void PerformHitDetection()
    {
        Collider[] hitEnemies = Physics.OverlapSphere(hitPoint.position, hitRadius, enemyLayer);

        foreach (Collider enemy in hitEnemies)
        {
            /*
            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(attackDamage);
            }
            */

            Debug.Log("Golpeaste a " + enemy.name + " por " + attackDamage + " de da�o.");
        }

        StartCoroutine(ShowGizmoCoroutine());
    }

    private IEnumerator ShowGizmoCoroutine()
    {
        showGizmo = true;
        yield return new WaitForSeconds(gizmoDuration);
        showGizmo = false;
    }

    private void OnDrawGizmos()
    {
        if (hitPoint == null || !showGizmo) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(hitPoint.position, hitRadius);
    }
}