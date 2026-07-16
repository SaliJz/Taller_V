using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerVfxCtrl : MonoBehaviour
{
    [System.Serializable]
    public class SlashMaterialSet
    {
        public string id; //Nombre del set
        public Material[] materials;
    }

    public enum MecanicUpgrades
    {
        larva, intento, vision, entropia
    }

    [SerializeField] private S_VFX_AfterImagesSpawner afterImagesSpawner;
    [SerializeField] private InventoryManager inventoryManager;

    #region Inspector - Slashes

    [Header ("Slashes")]
    [SerializeField] private GameObject[] slashesVFX;
    private ParticleSystemRenderer[] slashesRenderers;

    [SerializeField] private List<SlashMaterialSet> slashMaterialSets = new();
    [SerializeField] private string defaultSetID = "Normal";
    [SerializeField] private string upgradedSetID = "Upgraded";

    private string currentSetID;

    #endregion

    #region Inspector - Dash

    [Header("Dash")]
    [SerializeField] private GameObject dash_normal_AfterImage;
    [SerializeField] private GameObject dash_upgraded_AfterImage;
    [SerializeField] private float dash_SpawnInterval;
    [SerializeField] private float dash_upgraded_spawnInverval;
    [SerializeField] private float dash_lifetime;

    #endregion

    #region Inspector - Melee Displacement

    [Header("Melee Displacemente")]
    [SerializeField] private GameObject meleeDisplacement_AfterImage;
    [SerializeField] private float meleeDisplacement_SpawnInterval;
    [SerializeField] private float meleeDisplacement_lifetime;

    #endregion

    #region Inspector - Distance
    [Header ("Shield Upgrades")]
    [SerializeField] private TrailRenderer shieldTrailRend;
    [SerializeField] private List <SlashMaterialSet> ShieldTrailMaterials = new();
    [SerializeField] private GameObject ShieldUpgradeAfterImage;
    [SerializeField] private float shield_spawninterval;
    [SerializeField] private float shield_lifetime;
    [SerializeField] private GameObject ShieldBounceAfterImage;
    [SerializeField] private float bounceshield_lifeitime;

    private GameObject shield;
    private S_VFX_AfterImagesSpawner shieldSpawner;

    #endregion

    private bool _isMeleeUpgraded = false;
    private bool _isShieldUpgraded = false;
    private bool _isShieldBounceUpgraded = false;
    private bool _isShieldMecanic = false;
    private bool _isDashUpgraded = false;
    private bool hasMeleeDisplacement = false;

    private MecanicUpgrades? activeMeleeMecanic = null;
    private MecanicUpgrades? activeShieldMecanic = null;

    private GameObject currentDashAfterImage;
    private float currentDashSpawninterval;

    private void Awake()
    {
        slashesRenderers = new ParticleSystemRenderer[slashesVFX.Length];
        for (int i = 0; i < slashesVFX.Length; i++)
        {
            slashesRenderers[i] = slashesVFX[i].GetComponent<ParticleSystemRenderer>();
        }

        shield = FindAnyObjectByType<Shield>().gameObject;
        shieldSpawner = shield.GetComponentInChildren<S_VFX_AfterImagesSpawner>();
        shieldTrailRend = shield.GetComponentInChildren<TrailRenderer>();

        inventoryManager = FindAnyObjectByType<InventoryManager>();

        isDashUpgraded = false;
    }

    #if UNITY_EDITOR
    void Update()
    {
        testinput();
    }
#endif

    private void OnEnable()
    {
        InventoryManager.OnInventoryChanged += RefreshAllUpgradesStates;
        Shield.OnShieldBounce += StartBounceShieldEffect;

        RefreshAllUpgradesStates();
    }

    private void OnDisable()
    {
        InventoryManager.OnInventoryChanged -= RefreshAllUpgradesStates;
        Shield.OnShieldBounce -= StartBounceShieldEffect;
    }

    #region Melee Upgraded

    public bool isMeleeUpgraded
    {
        get => _isMeleeUpgraded;
        set
        {
            _isMeleeUpgraded = value;
            RefreshSlashMaterials();
        }
    }

    #endregion

    private void RefreshAllUpgradesStates()
    {
        if (inventoryManager == null) return;
        
        isMeleeUpgraded = inventoryManager.hasMeleeUpgradeItem();
        isDashUpgraded = inventoryManager.hasDashUpgradeItem();
        hasMeleeDisplacement = inventoryManager.hasMeleeDisplacement();
        IsShieldUpgraded = inventoryManager.hasShieldUpgradeItem();
        _isShieldBounceUpgraded = inventoryManager.hasShieldBounceItem();
        

        var meleeMecanic = inventoryManager.getActiveMeleeMecanic();
        if (meleeMecanic.HasValue) SetActiveMeleeMecanic(meleeMecanic);
        else ClearActiveMecanics(); 

        var shieldMecanic = inventoryManager.getActiveShieldMecanic();
        if (shieldMecanic.HasValue) SetActiveMeleeMecanic(shieldMecanic);
        else ClearActiveMecanics();
    }

    #region Melee - Mecanics VFX

    public void SetActiveMeleeMecanic (MecanicUpgrades? mecanic)
    {
        activeMeleeMecanic = mecanic;
        RefreshSlashMaterials();
    }

    public void ClearActiveMecanics()
    {
        activeMeleeMecanic = null;
        activeShieldMecanic = null;
        RefreshSlashMaterials();
    }

    #endregion

    #region Melee - Resolvers

    public void RefreshSlashMaterials()
    {
        string targetID;

        if (activeMeleeMecanic.HasValue)
        {
            targetID = activeMeleeMecanic.Value.ToString();
        }
        else if (_isMeleeUpgraded)
        {
            targetID = upgradedSetID;
        }
        else
        {
            targetID = defaultSetID;
        }

        ApplySetByID(targetID);
    }

    public void ApplySetByID(string ID)
    {
        if (currentSetID == ID) return;

        SlashMaterialSet set = slashMaterialSets.Find(s => s.id == ID);

        if (set == null)
        {
            Debug.LogError($"[{name}] No se encontró set de material para id: {ID}");
            return;
        }

        if (set.materials == null || set.materials.Length != slashesRenderers.Length)
        {
            Debug.LogError($"[{name}] El set {ID} no coincide en tamaño con SlashesRenderers. Esperando {slashesRenderers.Length} | Recibido {set.materials?.Length ?? 0}");
            return;
        }

        currentSetID = ID;

        for (int i = 0; i < slashesRenderers.Length; i++)
        {
            if (slashesRenderers[i] != null) slashesRenderers[i].material = set.materials[i];
        }
    }

    #endregion

    #region Melee - Slash Spawning

    public void SpawnSlash(int index)
    {
        Debug.Log($"index: {index}");

        GameObject prefab = slashesVFX[index];
        prefab.SetActive(false);
        prefab.SetActive(true);

        // if (debug) Debug.LogWarning($"SLASH: {index + 1} INSTATIATED");

        //melee1 = 0
        //melee2 = 1
        //melee3 = 2
    }

    #endregion

    #region Dash Methods

    public bool isDashUpgraded
    {
        get => _isDashUpgraded;
        set
        {
            _isDashUpgraded = value;
            currentDashAfterImage = _isDashUpgraded? dash_upgraded_AfterImage : dash_normal_AfterImage;
            currentDashSpawninterval = _isDashUpgraded? dash_upgraded_spawnInverval : dash_SpawnInterval;
        }
    }

    public void StartDashEffect()
    {
        if (afterImagesSpawner == null || currentDashAfterImage == null) return;

        afterImagesSpawner.afterImagePrefab = currentDashAfterImage;
        afterImagesSpawner.spawnInterval = currentDashSpawninterval;
        afterImagesSpawner.lifetime = dash_lifetime;
        afterImagesSpawner.enabled = true;
    }

    public void StopDashEffect()
    {
        if (afterImagesSpawner == null) return;
        afterImagesSpawner.enabled = false;    
    }

    #endregion

    #region Melee Displacement

    public void StartMeleeDisplacementEffect()
    {
        if (afterImagesSpawner == null) return;
        if (!hasMeleeDisplacement) return;

        afterImagesSpawner.afterImagePrefab = meleeDisplacement_AfterImage;
        afterImagesSpawner.spawnInterval = meleeDisplacement_SpawnInterval;
        afterImagesSpawner.lifetime = meleeDisplacement_lifetime;
        afterImagesSpawner.enabled = true;
    }

    public void StopMeleeDisplacementffect()
    {
        if (afterImagesSpawner == null) return;
        if (!hasMeleeDisplacement) return;
        afterImagesSpawner.enabled = false;    
    }

    #endregion

    #region Shield Methods

    public bool IsShieldUpgraded
    {
        get => _isShieldUpgraded;
        set
        {
            _isShieldUpgraded = value;

            bool active = _isShieldUpgraded? true: false;
            shieldSpawner.enabled = active;
        }
    }

    public void ApplyShieldSetByID(string ID)
    {
        if (currentSetID == ID) return;

        SlashMaterialSet set = ShieldTrailMaterials.Find(s => s.id == ID);

        if (set == null)
        {
            Debug.LogError($"[{name}] No se encontró set de material para id: {ID}");
            return;
        }

        if (set.materials == null || set.materials.Length != shieldTrailRend.materials.Length)
        {
            Debug.LogError($"[{name}] El set {ID} no coincide en tamaño con SlashesRenderers. Esperando {shieldTrailRend.materials.Length} | Recibido {set.materials?.Length ?? 0}");
            return;
        }

        currentSetID = ID;

        for (int i = 0; i < shieldTrailRend.materials.Length; i++)
        {
            if (shieldTrailRend.materials[i] != null) shieldTrailRend.materials[i] = set.materials[i];
        }
    }

    public void RefreshShieldTrailMaterials()
    {
        string targetID;

        if (activeShieldMecanic.HasValue)
        {
            targetID = activeShieldMecanic.Value.ToString();
        }
        else
        {
            targetID = defaultSetID;
        }

        ApplyShieldSetByID(targetID);
    }

    public void StartBounceShieldEffect()
    {
        if (!_isShieldBounceUpgraded) return;

        GameObject afterImage = Instantiate(ShieldBounceAfterImage);
        afterImage.transform.position = shield.transform.position;
        afterImage.transform.rotation = shield.transform.rotation;
        afterImage.transform.localScale = shield.transform.localScale;
    }

    

    #endregion

    #if UNITY_EDITOR
    void testinput()
    {
        if (Input.GetKeyDown(KeyCode.L)) isDashUpgraded = !isDashUpgraded;
        if (Input.GetKeyDown(KeyCode.O)) isMeleeUpgraded = !isMeleeUpgraded;
        if (Input.GetKeyDown(KeyCode.Keypad1)) SetActiveMeleeMecanic(MecanicUpgrades.larva);
        if (Input.GetKeyDown(KeyCode.Keypad2)) SetActiveMeleeMecanic(MecanicUpgrades.intento);
        if (Input.GetKeyDown(KeyCode.Keypad3)) SetActiveMeleeMecanic(MecanicUpgrades.entropia);
        if (Input.GetKeyDown(KeyCode.Keypad4)) SetActiveMeleeMecanic(MecanicUpgrades.vision);
        if (Input.GetKeyDown(KeyCode.RightShift)) ClearActiveMecanics();
    }

    #endif
}
