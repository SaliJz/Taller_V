using UnityEngine;

public class TEST_MOVEMENT : MonoBehaviour
{
    Rigidbody rb;

    float h;
    float v;
    public float speed = 10;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = gameObject.GetComponent<Rigidbody>();   
    }

    // Update is called once per frame
    void Update()
    {
        h = Input.GetAxisRaw("Horizontal");
        v = Input.GetAxisRaw("Vertical");

        Vector3 inputDir = new Vector3(h, 0, v);

        Vector3 rotatedDir = Quaternion.Euler(0, 45, 0) * inputDir;

        rb.linearVelocity = rotatedDir * speed * Time.deltaTime * 10;
    }
}
