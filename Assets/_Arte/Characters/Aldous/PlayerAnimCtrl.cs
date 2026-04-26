using UnityEngine;

public class PlayerAnimCtrl : BaseAnimCtrl<PlayerAnimCtrl.PlayerState>
{
    #region Custom Classes

    public enum PlayerState
    {
        idle, run, dash, distance, melee, damage,
        block, blockfb, store, inventory, bind
    }

    public enum Age
    {
        young, adult, old
    }

    [Header("Player Custom Status")]
    public Age currentAge =Age.adult;
    public Age nextAge;
    public bool HasShield = true;
    private bool currentShield = true;
    public int meleeStep = 1;

    [Header("State Flags")]
    public bool damageActive;
    public bool isDashing;
    public bool isBlocking;

    [Header("Settings & VFX")]
    private float dashTimer;
    public float dashDuration = 0.5f;
    [SerializeField] GameObject[] VFX_melee;

    private float H, V;
    private bool IsWalking() => H != 0 || V != 0;

    private bool _externalInputThisFrame = false;

    public void SetInputAxes(float h, float v)
    {
        H = h;
        V = v;
        _externalInputThisFrame = true;
    }

    #endregion

    #region Unity Lifecycle

    protected override void Start()
    {
        base.Start();

        nextAge = currentAge;
        currentShield = HasShield;
    }

    private void Update()
    {
        if (!_externalInputThisFrame)
        {
            if (PauseController.Instance != null && PauseController.IsGamePaused) return;
            if (InventoryUIManager.Instance != null && InventoryUIManager.Instance.IsOpen) return;

            H = Input.GetAxisRaw("Horizontal");
            V = Input.GetAxisRaw("Vertical");
        }
        _externalInputThisFrame = false;

        UpdateDirection(H,V);
        //DebugInputs();

        if (UpdateBlockLogic()) return;
        if (UpdateDashLogic()) return;
        if (isForcedAnim) return;

        HandleLocomotion();
    }

    #endregion

    #region Logica de Estados

    private void HandleLocomotion()
    {
        PlayerState nextState = IsWalking()? PlayerState.run : PlayerState.idle;
        
        bool shieldChanged = HasShield != currentShield;
        bool ageChanged = currentAge != nextAge;

        bool forceRefresh = shieldChanged || ageChanged;
        // bool stateChanged = nextState != currentState;
        // bool directionChanged = lastDirection != currentDirection;

        // if(stateChanged || directionChanged || shieldChanged || ageChanged)
        // {
        //     bool shouldReset = stateChanged&& !IsLoopingState(nextState);
        //     if(shieldChanged && !IsLoopingState(nextState)) shouldReset = false;

        //     currentState = nextState;
        //     currentShield = HasShield;
        //     currentAge = nextAge;
        //     currentDirection = lastDirection;

        //     PlayState(currentState, AnimPriority.locomotion, shouldReset);
        // }

        if (forceRefresh)
        {
            currentShield = HasShield;
            currentAge = nextAge;
        }

        PlayState(nextState, AnimPriority.locomotion, forceRefresh);
    }

    private bool UpdateBlockLogic()
    {
        if (!isBlocking) return false;

        // if(lastDirection != currentDirection)

        PlayState(PlayerState.block, AnimPriority.action, false);

        return true;
    }

    private bool UpdateDashLogic()
    {
        if(!isDashing) return false;

        dashTimer -= Time.deltaTime;
        if(dashTimer <= 0) EndDash();
        return true;
    }

    private bool IsLoopingState(PlayerState state)
    {
        return state == PlayerState.idle || state == PlayerState.run;
    }

    #endregion

    #region Public Actions

    public void SetAgeStage(int age)
    {
        switch (age)
        {
            case 3: nextAge = Age.young; break;
            case 2: nextAge = Age.adult; break;
            case 1: nextAge = Age.old; break;
        }
    }

    public void PlayDamage() //DAMAGE-------------------------------
    {
        if (damageActive) return;

        if (isBlocking)
        {
            PlayBlockFeedback();
            return;
        }

        damageActive = true;
        PlayState(PlayerState.damage, AnimPriority.damage);
    }

    public void PlayDistanceAttack()
    {
        PlayState(PlayerState.distance, AnimPriority.attack);
    }

    public void PlayMelee(int index) //MELEE -------------------------------------
    {
        meleeStep = index;

        if (provider.AnimExist(ResolveFullID("melee", lastDirection)))
        {
            PlayState(PlayerState.melee, AnimPriority.attack);
        }
    }

    public void StartDash() //DASH---------------------------------------
    {
        if(isDashing) return;
        isDashing = true;
        dashTimer = dashDuration;
        PlayState(PlayerState.dash, AnimPriority.dash);
    }

    public void EndDash()
    {
        isDashing = false;
        damageActive = false;
        currentPriority = AnimPriority.none;
        PlayState(PlayerState.idle, AnimPriority.locomotion);
    }

    public void PlayBlock() // BLOCK ------------------------------------------------------
    {
        isBlocking = true;
        SA.holdOnLastFrame = true;
        PlayState(PlayerState.block, AnimPriority.action);
    }

    public void EndBlock()
    {
        isBlocking = false;
        SA.holdOnLastFrame = false;
        currentPriority = AnimPriority.none;
        PlayState(PlayerState.idle, AnimPriority.locomotion);
    }

    private void PlayBlockFeedback()
    {
        SA.holdOnLastFrame = false;
        PlayState(PlayerState.blockfb, AnimPriority.action);
    }

    #endregion

    #region Resolver & Callbacks

    protected override string ResolveFullID(string baseID, string direction)
    {
        string id = baseID;

        if(baseID == "idle")
        {
            id = (direction.Contains("down") || direction == "left") ? "idle1" : "idle2";
        }
        if(baseID == "melee")
        {
            id = $"melee{meleeStep}";
        }

        //Contruccion de Prefix de edad
        string prefix = currentAge switch
        {
            Age.young => "begin:",
            Age.old => "late:",
            _ => "mid:"
        };

        string resolved = prefix + id;

        if(!HasShield && provider.AnimExist(resolved + "_ns"))
        {
            resolved += "_ns";
        }

        return resolved;
    }

    protected override void OnAnimationEvent(string ev)
    {
        switch (ev)
        {
            case "MeleeSlash_1": SpawnSlash(0); break;
            case "MeleeSlash_2": SpawnSlash(1); break;
            case "MeleeSlash_3": SpawnSlash(2); break;
        }
    }

    protected override void OnFinishedAnimation()
    {
        if(currentPriority == AnimPriority.dash) return;
        if(currentState == PlayerState.blockfb)
        {
            isBlocking = true;
            SA.holdOnLastFrame = true;
            PlayState(PlayerState.block, AnimPriority.action, false);
            return;
        }

        damageActive = false;
        PlayState(PlayerState.idle, AnimPriority.locomotion);
    }

    #endregion

    private void SpawnSlash(int index)
    {
        GameObject prefab = VFX_melee[index];
        prefab.SetActive(true);

        Debug.LogWarning($"SLASH: {index + 1} INSTATIATED");

        //melee1 = 0
        //melee2 = 1
        //melee3 = 2
    }

    /*
    private void DebugInputs()
    {
        if (Input.GetKeyDown(KeyCode.H)) HasShield = !HasShield;

        if (Input.GetKeyDown(KeyCode.J) && !damageActive) PlayDamage();
        if (Input.GetKeyDown(KeyCode.Space) && !isDashing) StartDash();
        if (Input.GetKeyDown(KeyCode.B)) PlayBlock();
        if (Input.GetKeyUp(KeyCode.B)) EndBlock();
        if (Input.GetKeyDown(KeyCode.Keypad0)) PlayDistanceAttack();

        if (Input.GetKeyDown(KeyCode.Keypad1))
        {
            PlayMelee(1);
            Debug.Log("MELEE1");
        }
        if (Input.GetKeyDown(KeyCode.Keypad2))
        {
            PlayMelee(2);
            Debug.Log("MELEE2");
        }
        if (Input.GetKeyDown(KeyCode.Keypad3))
        {
            PlayMelee(3);
            Debug.Log("MELEE3");
        }

        if (Input.GetKeyDown(KeyCode.Alpha1)) nextAge = Age.young;
        if (Input.GetKeyDown(KeyCode.Alpha2)) nextAge = Age.adult;
        if (Input.GetKeyDown(KeyCode.Alpha3)) nextAge = Age.old;
    }
    */
}