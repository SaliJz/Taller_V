using System;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(PlayerAnimData))]
public class SpriteAnimator : MonoBehaviour
{
    SpriteRenderer sr;
    public AtlasLoader atlasLoader;
    public AnimLoader animLoader;
    PlayerAnimData DataBase;

    AnimLoader.AnimData currentAnim;
    int frameIndex;
    float timer;
    [NonSerialized] public bool holdOnLastFrame;
    int lastEventFrame = -1;

    public Action onAnimFinished;
    public Action<string> onAnimEvent;

    private void Awake()
    {
        DataBase = GetComponent<PlayerAnimData>();
        sr = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if(currentAnim != null)
        {
            timer += Time.deltaTime;
            float frameTime = 1f / currentAnim.frameRate;

            if(timer >= frameTime) 
            {
                timer -= frameTime;

                if(holdOnLastFrame && frameIndex >= currentAnim.frames.Length - 1) return;
                
                frameIndex++;

                // if(frameIndex != lastEventFrame)
                // {
                //     var ev = currentAnim.frames[frameIndex].evnt;
                //     if (!string.IsNullOrEmpty(ev))
                //     {
                //         onAnimEvent?.Invoke(ev);
                //         lastEventFrame = frameIndex;
                //     }
                // }

                if (frameIndex >= currentAnim.frames.Length)
                {
                    if(currentAnim.repeat == 0)
                    {
                        frameIndex = currentAnim.frames.Length - 1;
                        onAnimFinished?.Invoke();
                    }
                    else
                    {
                        frameIndex = 0;
                    }
                }

                triggerEnterEvents(frameIndex);

                // string spriteKey = currentAnim.frames[frameIndex].frame;

                //Evento
                // 

                //Aplicar frame
                ApplyCurrentFrame();
                // if( atlasLoader.spriteAtlas.TryGetValue(spriteKey, out Sprite sprite ))
                // {
                //     sr.sprite = sprite;
                // }
            }
        }
    }

    void ApplyCurrentFrame()
    {
        if (currentAnim == null) return;

        frameIndex = Mathf.Clamp(frameIndex, 0, currentAnim.frames.Length -1);

        string spriteKey = currentAnim.frames[frameIndex]. frame;

        if(atlasLoader.spriteAtlas.TryGetValue(spriteKey, out Sprite sprite))
        {
            sr.sprite = sprite;
        }
    }
    public void LoadAnim(string id)
    {
        // Debug.Log($"[ANIM] LoadAnim START -> {id}");

        var def = DataBase.GetAnim(id);

        // if(def == null) {Debug.LogError($"[ANIM] AnimDef es NULL for ID {id}"); return;}
        // Debug.Log($"[ANIM] ANIM DEF OK -> {def.id}");

        // Debug.Log($"sheet = {def.spriteSheet?.name}");
        // Debug.Log($"atlas = {def.atlasJson?.name}");

        atlasLoader.LoadAtlas(def.spriteSheet, def.atlasJson);
        animLoader.LoadAnim(def.id, def.animJson);
    }

    public void Play(string animID, string direction, bool reset = true)
    {
        currentAnim = animLoader.GetAnim(animID, direction);

        if (reset)
        {
            frameIndex = 0;
            timer = 0f;
            lastEventFrame = -1;
        }

        ApplyCurrentFrame();
        
        // DEBUG ANIMACION - DIRECCION-----
        // Debug.Log($"{animID} - {direction} - {reset}");
    }

    void triggerEnterEvents(int index)
    {
        if (currentAnim == null) return;
        if (index == lastEventFrame) return;

        var frame = currentAnim.frames[index];
        if(frame.evnt == null) return;

        foreach (var e in frame.evnt)
        {
            if (!string.IsNullOrEmpty(e))
            {
                onAnimEvent?.Invoke(e);

                // DEBUG CONFIRMACION DE EVENTOS-----------
                // Debug.Log($"[AnimEvent] {e} @ frame {index}");
            }
        }

        lastEventFrame = index;
    
    }


}
