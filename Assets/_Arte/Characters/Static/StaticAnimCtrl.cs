using JetBrains.Annotations;
using UnityEngine;

public class StaticAnimCtrl : MonoBehaviour
{
    Animator anim;
    [SerializeField] Material[] originalMaterials;
    [SerializeField] Material tpMaterial;
    [SerializeField] SkinnedMeshRenderer mesh;

    void Awake()
    {
        anim = gameObject.GetComponent<Animator>();

        if(mesh != null)
        {
            originalMaterials = mesh.sharedMaterials;
        }
        else
        {
            Debug.LogError("No hay SkinnedMeshRenderer asignado en: " + gameObject.name + "!!!");
        }
    }

    void Update()
    {
        // TESTinputs();
    }

    public void PlayShoot()
    {
        anim.SetTrigger("Shoot");
    }

    public void PlayDeath()
    {
        anim.SetTrigger("Death");
    }

    public void PlayTPout()
    {
        Material[] tpArray = new Material[originalMaterials.Length];
        for(int i = 0; i <tpArray.Length; i++)
        {
            tpArray[i] = tpMaterial;
        }

        mesh.materials = tpArray;

        anim.Play("TP out");
    }


    public void PlayTPin()
    {
        anim.Play("TP in");
    }
    public void restoreOriginalMaterials() //EVENTO AL FINAL DE PLAY TP IN
    {
        mesh.sharedMaterials = originalMaterials;
    }

    public void PlayDamage()
    {
        anim.SetTrigger("Damage");
    }

    void TESTinputs()
    {
        if(Input.GetKeyDown(KeyCode.Mouse0)) PlayShoot();
        if(Input.GetKeyDown(KeyCode.Space)) PlayTPout();
        if(Input.GetKeyDown(KeyCode.M)) PlayTPin();
        if(Input.GetKeyDown(KeyCode.B)) PlayDeath();
        if(Input.GetKeyDown(KeyCode.L)) PlayDamage();
     }
}