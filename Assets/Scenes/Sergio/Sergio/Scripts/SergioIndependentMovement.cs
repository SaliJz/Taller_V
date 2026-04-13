using UnityEngine;

public class SergioIndependentMovement : MonoBehaviour
{
    private Vector3 moveDirection;
    private float moveSpeed;
    private bool isInitialized = false;
    private string targetTag = "Player";

    public void Setup(Vector3 direction, float speed, float lifetime)
    {
        moveDirection = direction;
        moveSpeed = speed;
        isInitialized = true;
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        if (!isInitialized) return;
        transform.position += moveDirection * moveSpeed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(targetTag))
        {
            Destroy(gameObject);
        }
        
        if (other.gameObject.layer == 0 && !other.isTrigger)
        {
            Destroy(gameObject);
        }
    }
}