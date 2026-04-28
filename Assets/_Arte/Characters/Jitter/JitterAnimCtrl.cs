using JetBrains.Annotations;
using UnityEngine;

public class JitterAnimCtrl : MonoBehaviour
{
    [Header("References")]
    Animator anim;
    [SerializeField] SkinnedMeshRenderer mesh;
    MaterialPropertyBlock propBlock;

    [Header("State Bools")]
    public bool isWalking; //CONECTAR AL BOOL EQUIVALENTE DEL ENEMIGO
    static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    bool isOnFuryMode = false;

    [Header("HeartBeat Settings")]
    [SerializeField] float normalFrecuency = 1.5f;
    [SerializeField] float furyFrecuency = 4f;
    [SerializeField] float pulseValue = 8f;

    void Awake()
    {
        anim = GetComponent<Animator>();
        propBlock = new MaterialPropertyBlock();
    }

    void Update()
    {
        HandleMovement();
        BeatRythim();

        if(isOnFuryMode) ElectricPulse();
        else ElectricityReset();

        // testImputs();
    }

    void HandleMovement()
    {
        anim.SetBool(IsWalkingHash, isWalking);
    }
    void BeatRythim()
    {
        float frecuency = isOnFuryMode? furyFrecuency: normalFrecuency;
        float baseWave = Mathf.Sin(Time.time * frecuency * Mathf.PI);
        float pulse = Mathf.Pow(baseWave, pulseValue);

        mesh.SetBlendShapeWeight(0, pulse * 100);
    }
    void ElectricPulse()
    {
        float pulse = Mathf.PingPong(Time.time * pulseValue, 1f);

        mesh.GetPropertyBlock(propBlock, 1);
        propBlock.SetFloat("_Amount", pulse);
        mesh.SetPropertyBlock(propBlock, 1);
    }
    void ElectricityReset()
    {
        mesh.GetPropertyBlock(propBlock, 1);
        if (propBlock.GetFloat("_Amount") > 0f)
        {
            propBlock.SetFloat("_Amount", 0);
            mesh.SetPropertyBlock(propBlock, 1);
        }

    }

#region Public actions
    public void SetFuryMode (bool active)
    {
        if (isOnFuryMode == active) return;
        
        isOnFuryMode = active;
        anim.SetBool("RageMode", isOnFuryMode);

        Debug.Log(isOnFuryMode);

        if (isOnFuryMode)
        {
            anim.Play("Rage Transform");
        }
    }

    public void PlayAttack()
    {
        anim.SetTrigger("Attack");
    }

    public void PlayDamage()
    {
        anim.Play("Damage", 0, 0);
    }

    public void PlayDeath()
    {
        anim.SetTrigger("Death");
    }

#endregion    

    void testImputs()
    {
        if (Input.GetKeyDown(KeyCode.W)) isWalking = true;
        if (Input.GetKeyUp(KeyCode.W)) isWalking = false;

        if (Input.GetKeyDown(KeyCode.Q) && isOnFuryMode == false) SetFuryMode(true);
        if (Input.GetKeyDown(KeyCode.E) && isOnFuryMode == true) SetFuryMode(false);

        if (Input.GetKeyDown(KeyCode.M)) PlayAttack();
        if (Input.GetKeyDown(KeyCode.J)) PlayDamage();

        if (Input.GetKeyDown(KeyCode.K)) PlayDeath();
    }
}
