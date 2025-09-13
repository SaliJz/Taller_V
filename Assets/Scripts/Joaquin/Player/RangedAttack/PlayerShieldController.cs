using UnityEngine;

/// <summary>
/// Clase que maneja el lanzamiento y recuperación del escudo del jugador.
/// </summary>
public class PlayerShieldController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private GameObject shieldPrefab;
    [SerializeField] private Transform shieldSpawnPoint;

    [Header("Stats")]
    [SerializeField] private bool canRebound = true;
    
    public bool CanRebound => canRebound;

    [Header("Costes")]
    //[SerializeField] private float timeCostToThrow = 5f;

    private bool hasShield = true;
    
    // private PlayerTimeResource playerTime;

    private void Awake()
    {
        // playerTime = GetComponent<PlayerTimeResource>();
        shieldPrefab.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(1) && hasShield)
        {
            // if (playerTime.CurrentTime >= timeCostToThrow)
            // {
            //     playerTime.SpendTime(timeCostToThrow);
            ThrowShield();
            // }
        }
    }

    /// <summary>
    /// Función que Lanza el escudo en la dirección del mouse y lo instancia en el punto y altura del spawn point.
    /// </summary>
    private void ThrowShield()
    {
        hasShield = false;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.up, shieldSpawnPoint.position);

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 targetPoint = ray.GetPoint(enter);
            Vector3 direction = (targetPoint - shieldSpawnPoint.position).normalized;

            GameObject shieldInstance = ShieldPooler.Instance.GetPooledObject();

            if (shieldInstance != null)
            {
                shieldInstance.transform.position = shieldSpawnPoint.position;
                shieldInstance.transform.rotation = Quaternion.LookRotation(direction);
                shieldInstance.GetComponent<Shield>().Throw(this, direction, canRebound);
            }
            else
            {
                hasShield = true;
            }
        }
    }

    // El escudo llama a esta función cuando regresa
    public void CatchShield() => hasShield = true;

    // Permite cambiar si el escudo puede rebotar o no
    public void SetRebound(bool value) => canRebound = value;
}