using UnityEngine;

public class AporiaAnimCtrl : BaseAnimCtrl<AporiaAnimCtrl.ActionState>
{
    public enum ActionState
    {
        run, dash, attack, death, damage
    }

    [Header("State Flags")]
    public bool damageActive;
    public bool isDashing;

    [Header("Settings & VFX")]
    float dashTimer;
    public float dashDuration = 0.5f;
    [SerializeField] GameObject AttackVFX;
    public float h, v; //CONECTAR A VELOCIDAD DEL ENEMIGO
    Vector2 nextdirecion;

    protected override void Start()
    {
        base.Start();

        PlayState(ActionState.run, AnimPriority.locomotion);
    }

    void Update()
    {
        string directionBeforeUpdate = currentDirection;

        // handleTESTimputs();
        UpdateDirection(h,v);

        UpdateDashLogic();

        if(currentPriority <= AnimPriority.locomotion)
        {
            bool hasInput = (h != 0 || v != 0);
            bool directionChanged = currentDirection != directionBeforeUpdate;

            if (hasInput)
            {
                if (directionChanged || currentState != ActionState.run)
                {
                    PlayState(ActionState.run, AnimPriority.locomotion, false);
                }
            }
        }
        
    }

#region Public Actions
    void PlayAttack()
    {
        PlayState(ActionState.attack, AnimPriority.attack);
    }

    public void PlayDash()
    {
        if(isDashing) return;
        isDashing = true;
        dashTimer = dashDuration;
        PlayState(ActionState.dash, AnimPriority.dash);
    }

    void EndDash()
    {
        isDashing = false;
        damageActive = false;
        currentPriority = AnimPriority.none;
        PlayState(ActionState.run, AnimPriority.locomotion);
    }

    public void PlayDeath()
    {
        PlayState(ActionState.death, AnimPriority.bind);
    }

    public void PlayDamage()
    {
        PlayState(ActionState.damage, AnimPriority.damage);
    }
#endregion

    protected override string ResolveFullID(string baseID, string direction)
    {
        return baseID;
    }

#region Event Void
    protected override void OnAnimationEvent(string ev)
    {
        //AQUI PONES LA LOGICA QUE NECESITES PARA LLAMAR EVENTOS POR NOMBRE
    }
#endregion

    protected override void OnFinishedAnimation()
    {
        if(currentPriority == AnimPriority.dash) return;
        PlayState(ActionState.run, AnimPriority.locomotion);
    }

    bool UpdateDashLogic()
    {
        if(!isDashing) return false;

        dashTimer -= Time.deltaTime;
        if(dashTimer <= 0) EndDash();
        return true;
    }

    void handleTESTimputs() //COMENTAR AL CONECTAR AL ENEMIGO
    {
        h = Input.GetAxisRaw("Horizontal");
        v = Input.GetAxisRaw("Vertical");

        if(Input.GetKeyDown(KeyCode.Space)) PlayDash();
        if(Input.GetKeyDown(KeyCode.G)) PlayAttack();
        if(Input.GetKeyDown(KeyCode.L)) PlayDamage();
    }   


}
