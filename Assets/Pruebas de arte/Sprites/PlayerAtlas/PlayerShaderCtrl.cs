using System.Collections;
using UnityEngine;

public class PlayerShaderCtrl : MonoBehaviour
{
    [Header("Renderer")]
    [SerializeField] Renderer targetRenderer;

    [Header("Berserker Outline")]
    [SerializeField] float outlineActive = 0.001f;
    [SerializeField] float outlineMax = 0.002f;
    [SerializeField] GameObject BrokenVFX;
    [SerializeField] Material BrokenMat;
    

    [Header("ShineSettings")]
    [SerializeField] float shinePeak = 0.5f;
    [SerializeField] float damagePeak = 0.5f;

    MaterialPropertyBlock mpb;
    Coroutine shineRoutine;
    Coroutine outlineRoutine;

    static readonly int GrossorID = Shader.PropertyToID("_Grossor");
    static readonly int ShineID = Shader.PropertyToID("_ShineAmount");
    static readonly int HasStaminaID = Shader.PropertyToID("_HasStamina");


    bool testBerserActive = false;
    bool testStamina = true;

    void Start()
    {
        mpb = new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(mpb);

        //Estado base
        mpb.SetFloat(GrossorID, 0f);
        mpb.SetFloat(ShineID, 0f);
        mpb.SetFloat(HasStaminaID, 1f);

        targetRenderer.SetPropertyBlock(mpb);
    }

    void Update()
    {
        TEST_IMPUTS();
    }

    void LateUpdate()
    {
        if (targetRenderer == null) return;

        Bounds b = targetRenderer.bounds;
        b.Expand(0.1f);

        targetRenderer.bounds = b;
    }

    void TEST_IMPUTS()
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
    }


    //-----------------------------------------------
    //SHINE

    public void ShineTrigger(float duration = 0.22f)
    {
        if(shineRoutine != null) StopCoroutine(shineRoutine);

        shineRoutine = StartCoroutine(ShineRoutine(duration));
    }

    IEnumerator ShineRoutine(float duration)
    {
        float half = duration * 0.5f;
        float t = 0f;

        while (t < half)
        {
            t += Time.deltaTime;
            setShine(Mathf.Lerp(0f, shinePeak, t / half));
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            setShine(Mathf.Lerp(shinePeak, 0f, t / half));
            yield return null;
        }

        setShine(0f);
        shineRoutine = null;
    }

    void setShine(float value)
    {
        targetRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat(ShineID, value);

        targetRenderer.SetPropertyBlock(mpb);
    }

    //-------------------------------------------------
    //BERSERKER

    public void BersekerOutline(bool Active)
    {
        if (outlineRoutine != null) StopCoroutine(outlineRoutine);

        outlineRoutine = StartCoroutine(Active? berserkerOn():berserkerOff());
    }

    IEnumerator berserkerOn()
    {
        ShineTrigger(0.5f);
        float t = 0f;
        const float time = 0.1f;

        while (t < time)
        {
            t += Time.deltaTime;
            setOutline(Mathf.Lerp(0f, outlineMax, t / time));
            yield return null;
        }

        t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            setOutline(Mathf.Lerp(outlineMax, outlineActive, t / time));
            yield return null;
        }

        setOutline(outlineActive);
        outlineRoutine = null;
    }

    IEnumerator berserkerOff()
    {
        float t = 0f;
        float time = 0.15f;
        ShineTrigger();

        float start = GetCurrentOutline();

        while (t < time)
        {
            t += Time.deltaTime;
            setOutline(Mathf.Lerp(start, 0f, t / time));
            yield return null;
        }

        GameObject obj = Instantiate(BrokenVFX);
        obj.transform.position = transform.position;

        OutlineBrokeColor(obj);
        
        setOutline(0f);
        outlineRoutine = null;
    }

    void setOutline(float value)
    {
        targetRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat(GrossorID, value);

        targetRenderer.SetPropertyBlock(mpb);
    }

    float GetCurrentOutline()
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

    void OutlineBrokeColor(GameObject RefVFX)
    {
        Material copy = Instantiate(BrokenMat);
        ParticleSystem party = RefVFX.GetComponent<ParticleSystem>();

        targetRenderer.GetPropertyBlock(mpb);
        float stamina = mpb.GetFloat(HasStaminaID);

        copy.color = stamina == 1? Color.yellow: Color.red;

        ParticleSystemRenderer psRender = RefVFX.GetComponent<ParticleSystemRenderer>();
        psRender.material = copy;
    }
}
