using UnityEngine;

public class VeynarAnimCtrl : MonoBehaviour
{
    [SerializeField] Animator anim;
    [SerializeField] SkinnedMeshRenderer mesh;
    [SerializeField] Material[] materials;
    

    string CamouProperty = "_Camou_Amount";

    void Start()
    {
        materials = mesh.sharedMaterials;
    }

    void Update()
    {
        // testInput();
    }

    public void UpdateCamou(float value)
    {
        for(int i = 0; i < materials.Length; i++)
        {
            materials[i].SetFloat(CamouProperty, value);
        }

    }

    public void PlayDamage()
    {
        anim.SetTrigger("Damage");
    }

    public void PlayMoveOut() //Inicio TP
    {
        anim.Play("Move Out");
        anim.SetBool("IsTraveling", true);
    }

    public void PlayMoveIn()
    {
        anim.Play("Move In");
    }

    public void TravelingFalseEvent()
    {
        anim.SetBool("IsTraveling", false);
    }

    void testInput()
    {
        if (Input.GetKeyDown(KeyCode.K)) PlayDamage();
        if (Input.GetKeyDown(KeyCode.L)) PlayMoveOut();
        if (Input.GetKeyDown(KeyCode.P)) PlayMoveIn();
         
        if(Input.GetKeyDown(KeyCode.Keypad1)) UpdateCamou(0f);
        if(Input.GetKeyDown(KeyCode.Keypad2)) UpdateCamou(0.5f);
        if(Input.GetKeyDown(KeyCode.Keypad3)) UpdateCamou(1f);
    }
}
