using Unity.VisualScripting;
using UnityEngine;

public class PlayerAnimCtrl : MonoBehaviour
{
    [Header("Scripts")]
    AnimDataBase DataBase;
    //AtlasLoader atlasLoader;
    //AnimDataLoader animLoader;
    SpriteAnimator SA;
    [Header("Variables")]
    public float H;
    public float V;

    [Header("Estados")]
    public AnimPriority currentPriority = AnimPriority.none;
    public PlayerState currentState;
    public string currentDirection;
    public string LastDirection = "down";
    public bool IsWalking;
    public bool HasShield = true;
    public bool currentShield = true;
    public bool isForcedAnim;
    public string forcedState;
    public bool damageActive;
    public bool isDashing;
    public float dashTimer;
    public float dashDuration = 0.5f;

    public enum PlayerState
    {
        idle,
        run,
        dash,
        attack,
        distance,
        damage
    }
    public enum AnimPriority
    {
        none = 0,
        damage = 10,
        action = 20, //distance, melee, inventory
        dash = 30
    }

    private void Awake()
    {
        SA = GetComponent<SpriteAnimator>();
        DataBase = GetComponent<AnimDataBase>();
    }

    private void Start()
    {
        LoadAnimations();

        SA.onAnimFinished += onForceAnimFinished;
    }

    private void Update()
    {
        H = Input.GetAxisRaw("Horizontal");
        V = Input.GetAxisRaw("Vertical");
        IsWalking = (H != 0 || V != 0);
        TEST_IMPUTS();
        GetDirection(H, V);


        PlayerState nextState = IsWalking? PlayerState.run: PlayerState.idle;
        string nextDirection = LastDirection;
        bool nextHasShield = HasShield;

        bool StateChanged = nextState != currentState;
        bool DirectionChanged = nextDirection != currentDirection;
        bool ShiledChanged = nextHasShield != currentShield;

        bool ShouldReset = StateChanged || (ShiledChanged && currentState != PlayerState.run && currentState != PlayerState.idle);

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0) endDash();
            return;
        }

        if (isForcedAnim) return;

        if(StateChanged || DirectionChanged || ShiledChanged)
        {
            currentState = nextState;
            currentDirection = nextDirection;
            currentShield = nextHasShield;

            string IDresolved = ResolveAnim(currentState, currentDirection);
            SA.Play(IDresolved, currentDirection, ShouldReset);
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
            takeDamage();
        }

        if (Input.GetKeyDown(KeyCode.Space) && !isDashing)
        {
            startDash();
        }

        if (Input.GetKeyDown(KeyCode.Mouse1))
        {
            makeDistance();
        }
    }
    void LoadAnimations()
    {
        //IDLE
        SA.LoadAnim("idle1");
        SA.LoadAnim("idle1_ns");
        SA.LoadAnim("idle2");
        SA.LoadAnim("idle2_ns");
        //RUN
        SA.LoadAnim("run");
        SA.LoadAnim("run_ns");
        //DAMAGE
        SA.LoadAnim("damage");
        SA.LoadAnim("damage_ns");
        //DASH
        SA.LoadAnim("dash");
        SA.LoadAnim("dash_ns");
        //ATTACKS
        SA.LoadAnim("distance");
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

    string ResolveAnim(PlayerState state, string direction)
    {
        string baseID = state switch
        {
            PlayerState.idle => "idle",
            PlayerState.run => "run",
            PlayerState.dash => "dash",
            PlayerState.attack => "attack",
            PlayerState.distance => "distance",
            PlayerState.damage => "damage",
            _ => "idle"

        };

        string resolved = baseID;

        if (baseID == "idle")
        {
            resolved = isIdleGroup1(direction) ? "idle1" : "idle2";
        }

        //if(baseID == "run")
        //{
        //    resolved = "run";
        //}

        //if (baseID == "damage")
        //{
        //    resolved = "damage";
        //}

        //CONFIRMA SI TIENE EL ESCUDO        
        if (!HasShield)
        {
            string nsVariant = resolved + "_ns";
            if (DataBase.AnimExist(nsVariant))
            {
                resolved = nsVariant;
            }
        }

        return resolved;
    }

    void forceAnimation(PlayerState newstate, AnimPriority priority, bool reset = true)
    {
        if (priority < currentPriority) return;

        currentPriority = priority;
        currentState = newstate;
        isForcedAnim = true;

        string resolved = ResolveAnim(currentState, currentDirection);
        SA.Play(resolved, currentDirection, reset);
    }

    public void takeDamage()
    {
        if (damageActive) return;

        damageActive = true;
        isForcedAnim = true;

        forceAnimation(PlayerState.damage, AnimPriority.damage);
    }
    public void makeDistance()
    {
        isForcedAnim = true;
        forceAnimation(PlayerState.distance, AnimPriority.action);
    }


    void startDash()
    {
        isDashing = true;
        isForcedAnim = true;
        dashTimer = dashDuration;

        currentPriority = AnimPriority.dash;
        currentState = PlayerState.dash;
        currentDirection = LastDirection;

        string resolved = ResolveAnim(currentState, currentDirection);
        SA.Play(resolved, currentDirection, true);
    }
    void endDash()
    {
        isDashing = false;
        damageActive = false;
        isForcedAnim = false;
        currentPriority = AnimPriority.none;
        currentState = PlayerState.idle;

        string resolved = ResolveAnim(currentState, currentDirection);
        SA.Play(resolved, currentDirection, true);
    }

    void onForceAnimFinished()
    {
        Debug.Log("Anim Forced FINISH");

        //if (!damageActive) return;
        if (currentPriority == AnimPriority.dash) return;

        Debug.Log("damage y force deactivate");
        damageActive = false;
        isForcedAnim = false;

        currentPriority = AnimPriority.none;
        currentState = PlayerState.idle;
        currentDirection = LastDirection;

        string resolved = ResolveAnim(currentState, currentDirection);
        SA.Play(resolved, currentDirection, true);
    }
}
