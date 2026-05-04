using System.Collections;
using UnityEngine;

public class PlayerShaderCtrl : MonoBehaviour
{
    #region Variables

    [Header("Referencias")]
    [SerializeField] private Renderer targetRenderer; // Renderer del material que queremos controlar
    [SerializeField] private SpriteRenderer spriteRend; // Renderer del sprite para el flash de daño

    [Header("Berserker Settings")]
    [Tooltip("Valor del grosor del outline cuando está activo (en estado de berserker)")]
    [SerializeField] private float outlineActive = 0.001f;
    [Tooltip("Valor máximo del grosor del outline durante la transición (en estado de berserker)")]
    [SerializeField] private float outlineMax = 0.002f;
    [SerializeField] private GameObject BrokenVFX; // Prefab del VFX de ruptura del outline
    [SerializeField] private Material BrokenMat; // Material para el VFX de ruptura, que cambiará de color según el estado de stamina

    [Header("Shine Settings")]
    [Tooltip("Valor máximo del brillo durante el efecto de shine")]
    [SerializeField] private float shinePeak = 0.5f;
    [Tooltip("Valor máximo del brillo durante el efecto de shine")]
    [SerializeField] private float damagePeak = 0.5f;

    [Header("Damage Settings")]
    [Tooltip("Duración total del flash de daño (ida y vuelta)")]
    [SerializeField] private float damageFlashDuration = 0.15f;

    private MaterialPropertyBlock mpb;
    private Coroutine shineRoutine;
    private Coroutine outlineRoutine;
    private Coroutine damageRoutine;

    private static readonly int GrossorID = Shader.PropertyToID("_Grossor");
    private static readonly int ShineID = Shader.PropertyToID("_ShineAmount");
    private static readonly int HasStaminaID = Shader.PropertyToID("_HasStamina");

    //private bool testBerserActive = false;
    //private bool testStamina = true;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        mpb = new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(mpb);

        //Estado base
        mpb.SetFloat(GrossorID, 0f);
        mpb.SetFloat(ShineID, 0f);
        mpb.SetFloat(HasStaminaID, 1f);

        targetRenderer.SetPropertyBlock(mpb);
    }

    private void Update()
    {
        //TEST_IMPUTS();
    }

    private void LateUpdate()
    {
        if (targetRenderer == null) return;

        Bounds b = targetRenderer.bounds;
        
        b.center = transform.position;
        
        b.Expand(0.1f);

        targetRenderer.bounds = b;
    }

    #endregion

    #region Testing
    /*
    private void TEST_IMPUTS()
    {
        if (Input.GetKeyDown(KeyCode.Keypad9))
        {
            ShineTrigger();
        }

        if (Input.GetKeyDown(KeyCode.Keypad8))
        {
            testBerserActive = !testBerserActive;
            BersekerOutline(testBerserActive);
        }
        if (Input.GetKeyDown(KeyCode.Keypad7))
        {
            testStamina = !testStamina;
            SetHasStamina(testStamina);
        }
        if (Input.GetKeyDown(KeyCode.J))
        {
            DamageTrigger();
        }
    }
    */
    #endregion

    #region Shine Functions

    public void ShineTrigger(float duration = 0.22f)
    {
        if(shineRoutine != null) StopCoroutine(shineRoutine);

        shineRoutine = StartCoroutine(ShineRoutine(duration));
    }

    private IEnumerator ShineRoutine(float duration)
    {
        float half = duration * 0.5f;
        float t = 0f;

        while (t < half)
        {
            t += Time.deltaTime;
            SetShine(Mathf.Lerp(0f, shinePeak, t / half));
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            SetShine(Mathf.Lerp(shinePeak, 0f, t / half));
            yield return null;
        }

        SetShine(0f);
        shineRoutine = null;
    }

    private void SetShine(float value)
    {
        targetRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat(ShineID, value);

        targetRenderer.SetPropertyBlock(mpb);
    }

    #endregion

    #region Berserker Functions

    public void BersekerOutline(bool Active)
    {
        if (outlineRoutine != null) StopCoroutine(outlineRoutine);

        outlineRoutine = StartCoroutine(Active? BerserkerOn():BerserkerOff());
    }

    private IEnumerator BerserkerOn()
    {
        ShineTrigger(0.5f);
        float t = 0f;
        const float time = 0.1f;

        while (t < time)
        {
            t += Time.deltaTime;
            SetOutline(Mathf.Lerp(0f, outlineMax, t / time));
            yield return null;
        }

        t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            SetOutline(Mathf.Lerp(outlineMax, outlineActive, t / time));
            yield return null;
        }

        SetOutline(outlineActive);
        outlineRoutine = null;
    }

    private IEnumerator BerserkerOff()
    {
        float t = 0f;
        float time = 0.15f;
        ShineTrigger();

        float start = GetCurrentOutline();

        while (t < time)
        {
            t += Time.deltaTime;
            SetOutline(Mathf.Lerp(start, 0f, t / time));
            yield return null;
        }

        GameObject obj = Instantiate(BrokenVFX);
        obj.transform.position = transform.position;

        OutlineBrokeColor(obj);
        
        SetOutline(0f);
        outlineRoutine = null;
    }

    private void SetOutline(float value)
    {
        targetRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat(GrossorID, value);

        targetRenderer.SetPropertyBlock(mpb);
    }

    private float GetCurrentOutline()
    {
        targetRenderer.GetPropertyBlock(mpb);
        return mpb.GetFloat(GrossorID);
    }

    public void SetHasStamina(bool hasStamina)
    {
        targetRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat(HasStaminaID, hasStamina? 1f: 0f);

        targetRenderer.SetPropertyBlock(mpb);
    }

    private void OutlineBrokeColor(GameObject RefVFX)
    {
        Material copy = Instantiate(BrokenMat);
        ParticleSystem party = RefVFX.GetComponent<ParticleSystem>();

        targetRenderer.GetPropertyBlock(mpb);
        float stamina = mpb.GetFloat(HasStaminaID);

        copy.color = stamina == 1? Color.yellow: Color.red;

        ParticleSystemRenderer psRender = RefVFX.GetComponent<ParticleSystemRenderer>();
        psRender.material = copy;
    }

    #endregion

    #region Damage Functions

    public void DamageTrigger()
    {
        if(damageRoutine != null) StopCoroutine(damageRoutine);

        damageRoutine = StartCoroutine(DamageRoutine());
    }

    private IEnumerator DamageRoutine()
    {
        float t = 0;

        while (t < 1)
        {
            t += Time.deltaTime / damageFlashDuration;
            spriteRend.color = Color.Lerp(Color.white, Color.red, t);
            yield return null;
        }

        t = 0;

        while (t < 1)
        {
            t += Time.deltaTime / damageFlashDuration;
            spriteRend.color = Color.Lerp(Color.red, Color.white, t);
            yield return null;
        }

        spriteRend.color = Color.white;
    }

    #endregion
}