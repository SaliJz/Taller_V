using UnityEngine;

public class PlayerAnimCtrl : MonoBehaviour
{
    //------------------------------------------------------------------------
    #region Referencias
    PlayerAnimData DataBase;
    SpriteAnimator SA;
    #endregion

    //------------------------------------------------------------------------
    #region Runtime Input
    float H;
    float V;
    #endregion

    //------------------------------------------------------------------------
    #region State
    public PlayerAnimData.Age currentAge = PlayerAnimData.Age.adult;
    PlayerAnimData.Age nextAge;

    public AnimPriority currentPriority = AnimPriority.none;
    public PlayerState currentState;
    public string currentDirection;
    string lastDirection = "down";

    public bool IsWalking;
    public bool HasShield = true;
    bool currentShield = true;

    public bool isForcedAnim;
    public bool damageActive;
    public bool isDashing;
    public bool isBlocking;

    float dashTimer;
    public float dashDuration = 0.5f;
    public int meleeStep = 0;
    #endregion

    //------------------------------------------------------------------------
    #region VFX Variables
    [SerializeField] GameObject[] VFX_melee;
    #endregion

    #region Enums
    public enum PlayerState
    {
        idle, run, dash, distance, melee, damage,
        block, blockfb, store, inventory, bind
    }
    public enum AnimPriority
    {
        none = 0, locomotion = 5,
        damage = 10,
        action = 20, //inventory, interact, transitions
        attack = 25, //melee, distance
        dash = 30, //movement override
        bind = 40
    }
    #endregion

    //------------------------------------------------------------------------
    #region Unity Lifecycle
    private void Awake()
    {
        SA = GetComponent<SpriteAnimator>();
        DataBase = GetComponent<PlayerAnimData>();
    }

    private void Start()
    {
        LoadAnimations();

        SA.onAnimFinished += OnForceAnimationFinished;
        SA.onAnimEvent += OnAnimationEvent;

        nextAge = currentAge;
    }

    private void Update()
    {
        ReadMovementInput();
        UpdateDirectionFromInput(H, V);
        DebugInputs();

        if (UpdateBlockState()) return;
        if(UpdateDash()) return;
        if (isForcedAnim) return;

        UpdateBaseMovementState();
    }
    #endregion

    //------------------------------------------------------------------------
    #region Debug
    void DebugInputs()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            HasShield = !HasShield;
        }

        if (Input.GetKeyDown(KeyCode.J) && !damageActive)
        {
            PlayDamage();
        }

        if (Input.GetKeyDown(KeyCode.Space) && !isDashing)
        {
            StartDash();
        }

        if (Input.GetKeyDown(KeyCode.Keypad0))
        {
            PlayDistanceAttack();
        }

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

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            nextAge = PlayerAnimData.Age.young;
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            nextAge = PlayerAnimData.Age.adult;
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            nextAge = PlayerAnimData.Age.old;
        }

        if (Input.GetKeyDown(KeyCode.B))
        {
            PlayBlock();
        }
        if (Input.GetKeyUp(KeyCode.B))
        {
            EndBlock();
        }
    }
    #endregion

    //------------------------------------------------------------------------
    #region Input y movimiento
    void ReadMovementInput()
    {
        H = Input.GetAxisRaw("Horizontal");
        V = Input.GetAxisRaw("Vertical");
        IsWalking = H != 0 || V != 0;
    }
    void UpdateDirectionFromInput(float h, float v)
    {
        if (h == 0 && v > 0) lastDirection = "up";
        else if (h == 0 && v < 0) lastDirection = "down";
        else if (h > 0 && v == 0) lastDirection = "right";
        else if (h < 0 && v == 0) lastDirection = "left";
        else if (h > 0 && v > 0) lastDirection = "upright";
        else if (h < 0 && v > 0) lastDirection = "upleft";
        else if (h > 0 && v < 0) lastDirection = "downright";
        else if (h < 0 && v < 0) lastDirection = "downleft"; ;
    }

    bool UsesIdle1(string dir)
    {
        if (dir == "down") return true;
        else if (dir == "downleft") return true;
        else if (dir == "downright") return true;
        else if (dir == "left") return true;
        else { return false; }
    }

    void UpdateBaseMovementState()
    {
        PlayerState nextState = IsWalking? PlayerState.run: PlayerState.idle;
        string nextDirection = lastDirection;
        bool nextHasShield = HasShield;

        bool StateChanged = nextState != currentState;
        bool DirectionChanged = nextDirection != currentDirection;
        bool ShiledChanged = nextHasShield != currentShield;
        bool AgeChanged = currentAge != nextAge;

        bool ShouldReset = false;

        if (StateChanged)
        ShouldReset = !IsLoopingState(nextState);

        if (ShiledChanged && !IsLoopingState(nextState)) 
        ShouldReset = true;

        if(StateChanged || DirectionChanged || ShiledChanged || AgeChanged)
        {
            currentState = nextState;
            currentDirection = nextDirection;
            currentShield = nextHasShield;
            currentAge = nextAge;

            PlayResolvedState(currentState, ShouldReset);
        }
    }

    bool UpdateBlockState()
    {
        if (!isBlocking) return false;
        
        bool directionChanged = lastDirection != currentDirection;

        if (directionChanged && currentState != PlayerState.blockfb)
        {
            currentDirection = lastDirection;
            PlayResolvedState (PlayerState.block, false);
        }

        return true;
    }

    bool UpdateDash()
    {
        if(!isDashing) return false;

        dashTimer -= Time.deltaTime;
        if(dashTimer <= 0) EndDash();
        return true;
    }

    #endregion

    //------------------------------------------------------------------------
    #region Anim Resolution
    string GetAgePrefix()
    {
        return currentAge switch
        {
            PlayerAnimData.Age.young => "begin:",
            PlayerAnimData.Age.adult => "mid:",
            PlayerAnimData.Age.old => "late:",
            _ => "mid:"
        };
    }

    string ResolveAnimationID(PlayerState state, string direction)
    {
        string baseID = state switch
        {
            PlayerState.idle => "idle",
            PlayerState.run => "run",
            PlayerState.dash => "dash",
            PlayerState.distance => "distance",
            PlayerState.melee => $"melee{meleeStep}",
            PlayerState.damage => "damage",
            PlayerState.block => "block",
            PlayerState.blockfb => "blockfb",
            _ => "idle"

        };

        string resolved = baseID;

        if (baseID == "idle")
        {
            resolved = UsesIdle1(direction) ? "idle1" : "idle2";
        }

        resolved = GetAgePrefix() + resolved; //Fijarse que PlayMelee() tenga el mismo orden//

        //CONFIRMA SI TIENE EL ESCUDO        
        if (!HasShield)
        {
            string nsVariant = resolved + "_ns";
            if (DataBase.AnimExist(nsVariant))
            {
                resolved = nsVariant;
            }
        }

        // Debug.Log($"Current ANim: {resolved}");
        return resolved;
    }
    #endregion

    //------------------------------------------------------------------------
    #region Anim Playback
    void LoadAnimations()
    {
        foreach (var anim in DataBase.Assets)
        {
            SA.LoadAnim(anim.id);
        }
    }

    void PlayResolvedState(PlayerState state, bool reset = true)
    {
        string resolved = ResolveAnimationID(state, currentDirection);
        SA.Play(resolved, currentDirection, reset);
    }

    bool IsLoopingState(PlayerState state)
    {
        return state == PlayerState.idle || state == PlayerState.run;
    }
    #endregion

    //------------------------------------------------------------------------
    #region Force Animations
    void PlayForcedAnimation(PlayerState newstate, AnimPriority priority, bool reset = true)
    {
        if (priority < currentPriority) return;

        currentPriority = priority;
        currentState = newstate;
        isForcedAnim = true;

        PlayResolvedState(currentState, reset);
    }
    #endregion

    //------------------------------------------------------------------------
    #region Public Actions
    public void PlayDamage() //DAMAGE-------------------------------
    {
        if (damageActive) return;

        if (isBlocking)
        {
            PlayBlockFeedback();
            return;
        }

        damageActive = true;
        PlayForcedAnimation(PlayerState.damage, AnimPriority.damage);
    }
    public void PlayDistanceAttack()
    {
        PlayForcedAnimation(PlayerState.distance, AnimPriority.attack);
    }


    public void StartDash() //DASH---------------------------------------
    {
        isDashing = true;
        isForcedAnim = true;
        dashTimer = dashDuration;

        currentPriority = AnimPriority.dash;
        currentState = PlayerState.dash;
        currentDirection = lastDirection;

        PlayResolvedState(currentState);
    }
    void EndDash()
    {
        isDashing = false;
        damageActive = false;
        isForcedAnim = false;
        currentPriority = AnimPriority.none;
        currentState = PlayerState.idle;

        PlayResolvedState(currentState);
    }

    public void PlayMelee(int index) //MELEE -------------------------------------
    {
        if (!DataBase.AnimExist($"{GetAgePrefix()}melee{index}")) return;

        meleeStep = index;
        currentDirection = lastDirection;
        PlayForcedAnimation(PlayerState.melee, AnimPriority.attack, true);
    }

    public void PlayBlock() // BLOCK ------------------------------------------------------
    {
        isBlocking = true;
        currentPriority = AnimPriority.action;
        SA.Play(ResolveAnimationID(PlayerState.block,currentDirection),currentDirection, false);
        SA.holdOnLastFrame = true;
    }

    public void EndBlock()
    {
        isBlocking = false;
        SA.holdOnLastFrame = false;
        isForcedAnim = false;
        currentPriority = AnimPriority.none;
        currentState = PlayerState.idle;

        PlayResolvedState(currentState, true);
    }

    void PlayBlockFeedback()
    {
        SA.holdOnLastFrame = false;
        isForcedAnim = true;
        currentState = PlayerState.blockfb;
        PlayForcedAnimation(currentState, AnimPriority.action);
    }
    #endregion

    //------------------------------------------------------------------------
    #region Animator Callbacks
    void OnForceAnimationFinished()
    {
        if (currentPriority == AnimPriority.dash) return; //No se interrumpe el dash

        if(currentState == PlayerState.blockfb)
        {
            currentState = PlayerState.block;
            isForcedAnim = true;
            currentPriority = AnimPriority.action;

            SA.holdOnLastFrame = true;

            PlayResolvedState(PlayerState.block, false);
            return;
        }

        damageActive = false;
        isForcedAnim = false;

        currentPriority = AnimPriority.none;
        currentState = PlayerState.idle;
        currentDirection = lastDirection;
        
        PlayResolvedState(currentState);
    }

    void OnAnimationEvent(string ev)
    {
        switch (ev)
        {
            case "MeleeSlash_1": SpawnSlash(0); break;
            case "MeleeSlash_2": SpawnSlash(1); break;
            case "MeleeSlash_3": SpawnSlash(2); break;
        }
    }
    #endregion

    //------------------------------------------------------------------------
    #region VFX
    void SpawnSlash(int index)
    {
        GameObject prefab = VFX_melee[index];
        prefab.SetActive(true);

        Debug.LogWarning($"SLASH: {index + 1} INSTATIATED");

        //melee1 = 0
        //melee2 = 1
        //melee3 = 2
    }
    #endregion


    

}