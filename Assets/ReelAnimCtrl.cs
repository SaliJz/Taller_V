using UnityEngine;

public class ReelAnimCtrl : MonoBehaviour
{
    [SerializeField] Animator anim;
    [SerializeField] SkinnedMeshRenderer mesh;

    [Header("Cameras Aim")]
    [SerializeField] LookAt leftLookAt;
    [SerializeField] LookAt rightLookAt;

    public enum Cameras{ right, left }

    public bool walking; //Conectar con el bool 

    void Update()
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

    void testInputs()
    {
        if(Input.GetKeyDown(KeyCode.J)) PlayAttack();
        if(Input.GetKeyDown(KeyCode.H)) PlayDeath();
        if (Input.GetKeyDown(KeyCode.O)) anim.Play("Idle"); //Para testing
    }

}
