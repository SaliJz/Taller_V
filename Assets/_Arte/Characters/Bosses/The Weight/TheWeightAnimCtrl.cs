using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TheWeightAnimCtrl : MonoBehaviour
{
    public enum SwapForms
    {
        formaBase, apisonador, pulpo, canon,
    }

    SwapForms currentForm;
    SkinnedMeshRenderer[] currentMesh;

    [Header("Modelos")]
    [SerializeField] GameObject formaBaseModel;
    [SerializeField] SkinnedMeshRenderer[] formaBaseMesh;
    [SerializeField] GameObject apisonadorModel;
    [SerializeField] SkinnedMeshRenderer[] apisonadorMesh;
    [SerializeField] GameObject canonModel;
    [SerializeField] SkinnedMeshRenderer[] canonMesh;
    [SerializeField] GameObject pulpoModel;
    [SerializeField] SkinnedMeshRenderer[] pulpoMesh;

    [Header("Swap Model VFX")]
    [SerializeField] Material swapMaterial;
    [SerializeField] float ghostVanishTime = 0.2f;
    [SerializeField] GameObject FleshPrefab;
    [SerializeField] int fleshCount; 
    [SerializeField] float fleshLifetime;

    Animator formaBase_anim;
    Animator apisonador_anim;
    Animator canon_anim;
    Animator pulpo_anim;

    private bool internalWalking;
    public bool isWalking ///////ESTE BOOL SE DEBE IGUALAR AL VALOR EQUIVALENTE DE WALKING DEL BOSS
    {
        get => internalWalking;
        set
        {
            if (internalWalking == value) return;
            internalWalking = value;
            formaBase_anim.SetBool("Walk", isWalking);
        }
    }

    #if UNITY_EDITOR
    void Update()
    {
        if(SceneManager.GetActiveScene().name == "AndreiNew")
        TestImputs();
    }
    #endif

    private const string ANIM_BASE_ATTACK = "A_Base_Attack";
    private const string ANIM_CANON_SHOT = "A_Canon_Disparo";
    private void Awake()
    {
        formaBase_anim = formaBaseModel.GetComponent<Animator>();
        apisonador_anim = apisonadorModel.GetComponent<Animator>();
        canon_anim = canonModel.GetComponent<Animator>();
        pulpo_anim = pulpoModel.GetComponent<Animator>();

        ReturnToIdle();
    }

    private void SwapTo(SwapForms newForm)
    {
        QuickSwapVFX();

        formaBaseModel.SetActive(false);
        apisonadorModel.SetActive(false);
        canonModel.SetActive(false);
        pulpoModel.SetActive(false);

        currentForm = newForm;

        switch (newForm)
        {
            case SwapForms.formaBase: 
            {
                formaBaseModel.SetActive(true); 
                currentMesh = formaBaseMesh;
                break;
            }
            case SwapForms.apisonador: 
            {
                apisonadorModel.SetActive(true);
                currentMesh = apisonadorMesh;
                break;
            }
            case SwapForms.canon: 
            {
                canonModel.SetActive(true);
                currentMesh = canonMesh;
                break;
            }
            case SwapForms.pulpo: 
            {
                pulpoModel.SetActive(true);
                currentMesh = pulpoMesh;
                break;
            }
        }
    }

    private void QuickSwapVFX()
    {
        if (currentMesh == null) return;

        foreach (var mesh in currentMesh)
        {
            Mesh bakedMesh = new Mesh();
            mesh.BakeMesh(bakedMesh);

            GameObject ghost = new GameObject("SwapGhost");
            ghost.transform.position = mesh.transform.position;
            ghost.transform.rotation = mesh.transform.rotation;
            ghost.transform.localScale = mesh.transform.localScale;

            MeshFilter mf = ghost.AddComponent<MeshFilter>();
            MeshRenderer mr = ghost.AddComponent<MeshRenderer>();
            EscudoController e = ghost.AddComponent<EscudoController>();
            mf.mesh = bakedMesh;
            mr.material = swapMaterial;
            e.Escudo = true;
            e.tiempoEscudo = ghostVanishTime;

            SpawnFleshVFX(bakedMesh);

            Destroy(ghost, ghostVanishTime);
        }
    }

    void SpawnFleshVFX(Mesh bakedMesh)
    {
        Vector3[] vertices = bakedMesh.vertices;

        for(int i = 0; i < fleshCount; i++)
        {
            Vector3 localPos = vertices[Random.Range(0, vertices.Length)];
            Vector3 worldPos = currentMesh[0]. transform.TransformPoint(localPos);

            GameObject currentFlesh = Instantiate(FleshPrefab, worldPos, Random.rotation);

            // currentFlesh.GetComponent<TheWeight_FleshVFX>().lifetime = fleshLifetime;
        }
    }


    #region Funcionas publicas
    public void PlayApisonador() => SwapTo(SwapForms.apisonador);
    public void PlayCanon() => SwapTo(SwapForms.canon);
    public void PlayPulpo() => SwapTo(SwapForms.pulpo);
    public void ReturnToIdle() => SwapTo(SwapForms.formaBase);
    public void PlayPrepareBaseAttack() => formaBase_anim.SetTrigger("PrepareAtk");
    public void PlayAttack() => formaBase_anim.Play(ANIM_BASE_ATTACK);
    public void PlayCanonShot() => canon_anim.SetTrigger("Shot");

    #endregion

    void TestImputs()
    {
        if(Input.GetKeyDown(KeyCode.J)) PlayApisonador();
        if(Input.GetKeyDown(KeyCode.K)) PlayCanon();
        if(Input.GetKeyDown(KeyCode.L)) PlayPulpo();
        if(Input.GetKeyDown(KeyCode.O)) ReturnToIdle();
        if(Input.GetKeyDown(KeyCode.Mouse0)) PlayPrepareBaseAttack();
        if(Input.GetKeyDown(KeyCode.Mouse1)) PlayAttack();
        if(Input.GetKeyDown(KeyCode.N)) PlayCanonShot();

        float h = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");
        isWalking = h != 0 || y != 0;
    }
}
