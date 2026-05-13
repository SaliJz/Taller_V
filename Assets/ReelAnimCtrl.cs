using UnityEngine;

public class ReelAnimCtrl : MonoBehaviour
{
    [SerializeField] private Animator anim;
    [SerializeField] private SkinnedMeshRenderer mesh;

    [Header("Cameras Aim")]
    [SerializeField] private LookAt leftLookAt;
    [SerializeField] private LookAt rightLookAt;

    public enum Cameras{ right, left }

    public bool walking; //Conectar con el bool 

    private void Update()
    {
        anim.SetBool("isWalking", walking);

        if(leftLookAt.Target != null || rightLookAt.Target != null)
        {
            mesh.SetBlendShapeWeight(0, 100);
        }
        else
        {
            mesh.SetBlendShapeWeight(0, 0);
        }

        // testInputs();
    }

    public void PlayAttack()
    {
        anim.SetTrigger("Attack");
    }

    public void PlayDeath()
    {
        anim.SetTrigger("Death");
    }

    public void SetTarget(Cameras cam, Transform target)
    {
        switch (cam)
        {
            case Cameras.right:
                {
                    rightLookAt.Target = target;
                    break;
                }
            case Cameras.left:
                {
                    leftLookAt.Target = target;
                    break;
                }
        }
    }

    public Transform GetCameraTransform(Cameras cam)
    {
        return cam == Cameras.right ? rightLookAt.transform : leftLookAt.transform;
    }

    public void ClearTarget(Cameras cam)
    {
        switch (cam)
        {
            case Cameras.right:
                {
                    rightLookAt.Target = null;
                    break;
                }
            case Cameras.left:
                {
                    leftLookAt.Target = null;
                    break;
                }
        }
    }

    private void testInputs()
    {
        if(Input.GetKeyDown(KeyCode.J)) PlayAttack();
        if(Input.GetKeyDown(KeyCode.H)) PlayDeath();
        if (Input.GetKeyDown(KeyCode.O)) anim.Play("Idle"); //Para testing
    }

}
