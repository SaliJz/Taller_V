using UnityEngine;

public class LookAt : MonoBehaviour
{
    [SerializeField] Transform Target;
    [SerializeField] float MaxAngle;
    [SerializeField] float MinAngle;

    private void LateUpdate()
    {
        if (Target == null & GameObject.FindGameObjectWithTag("Player"))
        {
            Target = GameObject.FindGameObjectWithTag("Player").transform;
        }

        if (Target != null)
        {
            transform.LookAt(Target);

            Vector3 e = transform.eulerAngles;

            float x = e.x;

            if(x > 180f)
            {
                x -= 360f;
            }

            x = Mathf.Clamp(x, MinAngle, MaxAngle);

            transform.rotation = Quaternion.Euler(x, e.y, e.z);
        }
    }
}
