using UnityEngine;

public class PlayerShieldController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private GameObject shieldPrefab;
    [SerializeField] private Transform shieldSpawnPoint;

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
        if (Input.GetMouseButtonDown(0) && hasShield)
        {
            // if (playerTime.CurrentTime >= timeCostToThrow)
            // {
            //     playerTime.SpendTime(timeCostToThrow);
            ThrowShield();
            // }
        }
    }

    /// <summary>
    /// // Lanza el escudo en la dirección del mouse y lo instancia en el punto y altura del spawn point.
    /// </summary>
    private void ThrowShield()
    {
        hasShield = false;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity))
        {
            // 1. Pide un escudo al pool en lugar de instanciar uno nuevo
            GameObject shieldInstance = ShieldPooler.Instance.GetPooledObject();

            if (shieldInstance != null)
            {
                // 2. Calcula la dirección y posición
                Vector3 targetPoint = hitInfo.point;
                targetPoint.y = shieldSpawnPoint.position.y; // Mantiene la altura del spawn point
                Vector3 direction = (targetPoint - shieldSpawnPoint.position).normalized;

                // 3. Posiciona y activa el escudo
                shieldInstance.transform.position = shieldSpawnPoint.position;
                shieldInstance.transform.rotation = Quaternion.LookRotation(direction);

                // 4. Lanza el escudo
                shieldInstance.GetComponent<Shield>().Throw(this, direction);
            }
            else
            {
                // Si el pool no pudo darnos un escudo, recuperamos el nuestro para no quedarnos sin él
                hasShield = true;
            }
        }
    }

    // El escudo llama a esta función cuando regresa
    public void CatchShield()
    {
        hasShield = true;
    }
}