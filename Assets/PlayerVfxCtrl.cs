using System.Collections.Generic;
using UnityEngine;

public class PlayerVfxCtrl : MonoBehaviour
{
    [System.Serializable]
    public class SlashMaterialSet
    {
        public string id; //Nombre del set
        public Material[] materials;
        public int priority;
    }

    public enum MecanicUpgrades
    {
        larva, intento, vision, entropia
    }

    [SerializeField] private S_VFX_AfterImagesSpawner afterImagesSpawner;

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

    [Header("Melee Displacemente")]
    [SerializeField] private float meleeDisplacement_SpawnInterval;
    [SerializeField] private float meleeDisplacement_lifetime;


    private bool _isMeleeUpgraded = false;
    private bool _isDistanceUpgraded = false;
    private bool _isDashUpgraded = false;
    private bool _hasMeleeDisplacement = false;

    private MecanicUpgrades? activeMecanic = null;

    private GameObject currentDashAfterImage;
    private float currentDashSpawninterval;
    private bool isDashing = false;

    private void Awake()
    {
        slashesRenderers = new ParticleSystemRenderer[slashesVFX.Length];
        for (int i = 0; i < slashesVFX.Length; i++)
        {
            slashesRenderers[i] = slashesVFX[i].GetComponent<ParticleSystemRenderer>();
        }

        isDashUpgraded = false;
    }

    void Update()
    {
        testinput();
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

    #region Melee - Mecanics VFX

    public void SetActiveMecanic (MecanicUpgrades? mecanic)
    {
        activeMecanic = mecanic;
        RefreshSlashMaterials();
    }

    public void ClearActiveMecanic()
    {
        activeMecanic = null;
        RefreshSlashMaterials();
    }

    #endregion

    #region Melee - Resolvers

    public void RefreshSlashMaterials()
    {
        string targetID;

        if (activeMecanic.HasValue)
        {
            targetID = activeMecanic.Value.ToString();
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

    void testinput()
    {
        if (Input.GetKeyDown(KeyCode.L)) isDashUpgraded = !isDashUpgraded;
        if (Input.GetKeyDown(KeyCode.O)) isMeleeUpgraded = !isMeleeUpgraded;
        if (Input.GetKeyDown(KeyCode.I)) SetActiveMecanic(MecanicUpgrades.larva);
        if (Input.GetKeyDown(KeyCode.RightShift)) ClearActiveMecanic();
    }
}
