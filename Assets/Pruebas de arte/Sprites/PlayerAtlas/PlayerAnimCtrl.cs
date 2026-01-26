using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem.Users;

public class PlayerAnimCtrl : MonoBehaviour
{
    [Header("Scripts")]
    PlayerAnimData DataBase;
    //AtlasLoader atlasLoader;
    //AnimDataLoader animLoader;
    SpriteAnimator SA;

    [Header("Variables")]
    public float H;
    public float V;

    [Header("Estados")]
    public PlayerAnimData.Age currentAge = PlayerAnimData.Age.adult;
    PlayerAnimData.Age nextAge;
    public AnimPriority currentPriority = AnimPriority.none;
    public PlayerState currentState;
    public string currentDirection;
    string LastDirection = "down";
    public bool IsWalking;
    public bool HasShield = true;
    bool currentShield = true;
    public bool isForcedAnim;
    public bool damageActive;
    public bool isDashing;
    float dashTimer;
    public float dashDuration = 0.5f;
    public int meleeStep = 0;
    public bool isBlocking;

    [Header("VFX")]
    [SerializeField] GameObject[] VFX_melee;

    public enum PlayerState
    {
        idle,
        run,
        dash,
        distance,
        melee,
        damage,
        block,
        blockfb,
        store,
        inventory,
        bind
    }
    public enum AnimPriority
    {
        none = 0,
        damage = 10,
        action = 20, //distance, melee, inventory
        dash = 30,
        bind = 40
    }

    private void Awake()
    {
        SA = GetComponent<SpriteAnimator>();
        DataBase = GetComponent<PlayerAnimData>();
    }

    private void Start()
    {
        LoadAnimations();

        SA.onAnimFinished += onForceAnimFinished;
        SA.onAnimEvent += onAnimEvent;

        nextAge = currentAge;
    }

    private void Update()
    {
        H = Input.GetAxisRaw("Horizontal");
        V = Input.GetAxisRaw("Vertical");
        IsWalking = H != 0 || V != 0;
        TEST_IMPUTS();
        GetDirection(H, V);


        PlayerState nextState = IsWalking? PlayerState.run: PlayerState.idle;
        string nextDirection = LastDirection;
        bool nextHasShield = HasShield;

        bool StateChanged = nextState != currentState;
        bool DirectionChanged = nextDirection != currentDirection;
        bool ShiledChanged = nextHasShield != currentShield;
        bool AgeChanged = currentAge != nextAge;


        // bool ShouldReset = StateChanged || (ShiledChanged && currentState != PlayerState.run && currentState != PlayerState.idle);
        bool ShouldReset = false;

        if (StateChanged) ShouldReset = !isLoopingState(nextState);
        if (ShiledChanged && !isLoopingState(nextState)) ShouldReset = true;

        if (isBlocking)
        {
            if (DirectionChanged && currentState != PlayerState.blockfb)
            {
                currentDirection = LastDirection;
                // currentState = PlayerState.block;
                playResolvedState (PlayerState.block, false);
                
            }

            return;
        }

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0) endDash();
            return;
        }

        if (isForcedAnim) return;


        if(StateChanged || DirectionChanged || ShiledChanged || AgeChanged)
        {
            currentState = nextState;
            currentDirection = nextDirection;
            currentShield = nextHasShield;
            currentAge = nextAge;

            playResolvedState(currentState, ShouldReset);
        }
    }

    void TEST_IMPUTS()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            HasShield = !HasShield;
        }

        if (Input.GetKeyDown(KeyCode.J) && !damageActive)
        {
            startDamage();
        }

        if (Input.GetKeyDown(KeyCode.Space) && !isDashing)
        {
            startDash();
        }

        if (Input.GetKeyDown(KeyCode.Keypad0))
        {
            startDistance();
        }

        if (Input.GetKeyDown(KeyCode.Keypad1))
        {
            playMelee(1);
            Debug.Log("MELEE1");
        }
        if (Input.GetKeyDown(KeyCode.Keypad2))
        {
            playMelee(2);
            Debug.Log("MELEE2");
        }
        if (Input.GetKeyDown(KeyCode.Keypad3))
        {
            playMelee(3);
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
            StartBlock();
        }
        if (Input.GetKeyUp(KeyCode.B))
        {
            EndBlock();
        }
    }
    void LoadAnimations()
    {
        foreach (var anim in DataBase.Assets)
        {
            SA.LoadAnim(anim.id);
        }
    }

    void GetDirection(float h, float v)
    {
        if (h == 0 && v > 0) LastDirection = "up";
        else if (h == 0 && v < 0) LastDirection = "down";
        else if (h > 0 && v == 0) LastDirection = "right";
        else if (h < 0 && v == 0) LastDirection = "left";
        else if (h > 0 && v > 0) LastDirection = "upright";
        else if (h < 0 && v > 0) LastDirection = "upleft";
        else if (h > 0 && v < 0) LastDirection = "downright";
        else if (h < 0 && v < 0) LastDirection = "downleft"; ;
    }

    
    bool isIdleGroup1(string dir)
    {
        if (dir == "down") return true;
        else if (dir == "downleft") return true;
        else if (dir == "downright") return true;
        else if (dir == "left") return true;
        else { return false; }
    }

    string agePrefix()
    {
        return currentAge switch
        {
            PlayerAnimData.Age.young => "begin:",
            PlayerAnimData.Age.adult => "mid:",
            PlayerAnimData.Age.old => "late:",
            _ => "mid:"
        };
    }

    string ResolveAnim(PlayerState state, string direction)
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
            resolved = isIdleGroup1(direction) ? "idle1" : "idle2";
        }

        resolved = agePrefix() + resolved; //Fijarse que PlayMelee() tenga el mismo orden//

        //CONFIRMA SI TIENE EL ESCUDO        
        if (!HasShield)
        {
            string nsVariant = resolved + "_ns";
            if (DataBase.AnimExist(nsVariant))
            {
                resolved = nsVariant;
            }
        }

        Debug.Log($"Current ANim: {resolved}");
        return resolved;
    }

    void forceAnimation(PlayerState newstate, AnimPriority priority, bool reset = true)
    {
        if (priority < currentPriority) return;

        currentPriority = priority;
        currentState = newstate;
        isForcedAnim = true;

        playResolvedState(currentState, reset);
    }

     void onForceAnimFinished()
    {
        //if (!damageActive) return;
        if (currentPriority == AnimPriority.dash) return;

        if(currentState == PlayerState.blockfb)
        {
            currentState = PlayerState.block;
            isForcedAnim = true;
            currentPriority = AnimPriority.action;

            SA.holdOnLastFrame = true;

            playResolvedState(PlayerState.block, false);
            return;
        }

        damageActive = false;
        isForcedAnim = false;

        currentPriority = AnimPriority.none;
        currentState = PlayerState.idle;
        currentDirection = LastDirection;
        
        playResolvedState(currentState);
    }

    void playResolvedState(PlayerState state, bool reset = true)
    {
        string resolved = ResolveAnim(state, currentDirection);
        SA.Play(resolved, currentDirection, reset);
    }

    bool isLoopingState(PlayerState state)
    {
        return state == PlayerState.idle || state == PlayerState.run;
    }

    public void startDamage() //DAMAGE-------------------------------
    {
        if (damageActive) return;

        if (isBlocking)
        {
            triggerBlockFB();
            return;
        }

        damageActive = true;
        forceAnimation(PlayerState.damage, AnimPriority.damage);
    }
    public void startDistance()
    {
        forceAnimation(PlayerState.distance, AnimPriority.action);
    }


    public void startDash() //DASH---------------------------------------
    {
        isDashing = true;
        isForcedAnim = true;
        dashTimer = dashDuration;

        currentPriority = AnimPriority.dash;
        currentState = PlayerState.dash;
        currentDirection = LastDirection;

        playResolvedState(currentState);
    }
    void endDash()
    {
        isDashing = false;
        damageActive = false;
        isForcedAnim = false;
        currentPriority = AnimPriority.none;
        currentState = PlayerState.idle;

        playResolvedState(currentState);
    }

    public void playMelee(int index) //MELEE -------------------------------------
    {
        if (!DataBase.AnimExist($"{agePrefix()}melee{index}")) return;

        meleeStep = index;
        currentDirection = LastDirection;
        forceAnimation(PlayerState.melee, AnimPriority.action, true);
    }

    public void StartBlock() // BLOCK ------------------------------------------------------
    {
        isBlocking = true;
        currentPriority = AnimPriority.action;
        SA.Play(ResolveAnim(PlayerState.block,currentDirection),currentDirection, false);
        SA.holdOnLastFrame = true;
    }

    public void EndBlock()
    {
        isBlocking = false;
        SA.holdOnLastFrame = false;
        isForcedAnim = false;
        currentPriority = AnimPriority.none;
        currentState = PlayerState.idle;

        playResolvedState(currentState, true);
    }

    void triggerBlockFB()
    {
        SA.holdOnLastFrame = false;
        isForcedAnim = true;
        currentState = PlayerState.blockfb;
        forceAnimation(currentState, AnimPriority.action);
    }

    void onAnimEvent(string ev)
    {
        switch (ev)
        {
            case "MeleeSlash_1": SpawnSlash(0); break;
            case "MeleeSlash_2": SpawnSlash(1); break;
            case "MeleeSlash_3": SpawnSlash(2); break;
        }
    }

    void SpawnSlash(int index)
    {
        GameObject prefab = VFX_melee[index];
        prefab.SetActive(true);

        Debug.LogWarning($"SLASH: {index + 1} INSTATIATED");

        //melee1 = 0
        //melee2 = 1
        //melee3 = 2
    }
   
}
