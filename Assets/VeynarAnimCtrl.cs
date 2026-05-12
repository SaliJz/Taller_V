using UnityEngine;
using System.Collections;

public class VeynarAnimCtrl : MonoBehaviour
{
    [SerializeField] private Animator anim;
    [SerializeField] private SkinnedMeshRenderer mesh;

    private const string CamouProperty = "_Camou_Amount";

    private MaterialPropertyBlock propBlock;

    private void Start()
    {
        propBlock = new MaterialPropertyBlock();
    }

    private void Update()
    {
        // testInput();
    }

    public void UpdateCamou(float value)
    {
        mesh.GetPropertyBlock(propBlock);
        propBlock.SetFloat(CamouProperty, value);
        mesh.SetPropertyBlock(propBlock);
    }

    public void PlayDamage()
    {
        anim.SetTrigger("Damage");
    }

    public void PlayMoveOut()
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

    public IEnumerator WaitForMoveOut()
    {
        yield return null;

        AnimatorStateInfo stateInfo;
        do
        {
            stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            yield return null;
        }
        while (stateInfo.IsName("Move Out") && stateInfo.normalizedTime < 1f);
    }

    private void testInput()
    {
        if (Input.GetKeyDown(KeyCode.K)) PlayDamage();
        if (Input.GetKeyDown(KeyCode.L)) PlayMoveOut();
        if (Input.GetKeyDown(KeyCode.P)) PlayMoveIn();

        if (Input.GetKeyDown(KeyCode.Keypad1)) UpdateCamou(0f);
        if (Input.GetKeyDown(KeyCode.Keypad2)) UpdateCamou(0.5f);
        if (Input.GetKeyDown(KeyCode.Keypad3)) UpdateCamou(1f);
    }
}