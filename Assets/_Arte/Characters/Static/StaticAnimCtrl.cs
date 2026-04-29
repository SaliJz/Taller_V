using JetBrains.Annotations;
using UnityEngine;

public class StaticAnimCtrl : MonoBehaviour
{
    private Animator anim;
    [SerializeField] private Material[] originalMaterials;
    [SerializeField] private Material tpMaterial;
    [SerializeField] private SkinnedMeshRenderer mesh;

   private void Awake()
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

    private void Update()
    {
        // TESTinputs();
    }

    public void PlayShoot()
    {
        if (anim != null) anim.SetTrigger("Shoot");
    }

    public void PlayDeath()
    {
        if (anim != null) anim.SetTrigger("Death");
    }

    public void PlayTPout()
    {
        Material[] tpArray = new Material[originalMaterials.Length];
        for(int i = 0; i <tpArray.Length; i++)
        {
            tpArray[i] = tpMaterial;
        }

        mesh.materials = tpArray;

        if (anim != null) anim.Play("TP out");
    }

    public void PlayTPin()
    {
        if (anim != null) anim.Play("TP in");
    }
    public void restoreOriginalMaterials() //EVENTO AL FINAL DE PLAY TP IN
    {
        mesh.sharedMaterials = originalMaterials;
    }

    public void PlayDamage()
    {
        if (anim != null) anim.SetTrigger("Damage");
    }

    private void TESTinputs()
    {
        if(Input.GetKeyDown(KeyCode.Mouse0)) PlayShoot();
        if(Input.GetKeyDown(KeyCode.Space)) PlayTPout();
        if(Input.GetKeyDown(KeyCode.M)) PlayTPin();
        if(Input.GetKeyDown(KeyCode.B)) PlayDeath();
        if(Input.GetKeyDown(KeyCode.L)) PlayDamage();
     }
}