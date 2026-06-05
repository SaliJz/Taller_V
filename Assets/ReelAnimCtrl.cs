using UnityEngine;
using UnityEngine.SceneManagement;

public class ReelAnimCtrl : MonoBehaviour
{
    [SerializeField] private Animator anim;
    [SerializeField] private SkinnedMeshRenderer mesh;
    [SerializeField] private EscudoController shieldCtrl;

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

        #if UNITY_EDITOR
        if(SceneManager.GetActiveScene().name == "AndreiNew") testInputs();
        #endif
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

    public void PlayInvulnerabilityVFX()
    {
        if(shieldCtrl != null) shieldCtrl.Escudo = true;
    }

    //public void PlayDamage()
    //{
    //    if (anim != null) anim.Play("Damage", 0, 0);
    //}

    public void PauseAnimation()
    {
        if (anim != null) anim.speed = 0f;
    }

    public void ResumeAnimation()
    {
        if (anim != null) anim.speed = 1f;
    }

    private void testInputs()
    {
        if(Input.GetKeyDown(KeyCode.J)) PlayAttack();
        if(Input.GetKeyDown(KeyCode.H)) PlayDeath();
        if (Input.GetKeyDown(KeyCode.O)) anim.Play("Idle");
        if(Input.GetKeyDown(KeyCode.P)) shieldCtrl.Escudo = true;
    }

}
