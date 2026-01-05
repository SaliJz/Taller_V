using System;
using UnityEditor.Experimental.GraphView;
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
        var def = DataBase.GetAnim(id);

        atlasLoader.LoadAtlas(def.sheet, def.atlasJSON);
        animLoader.LoadAnim(def.id, def.animJSON);
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
        
        Debug.Log($"{animID} - {direction} - {reset}");
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
                Debug.Log($"[AnimEvent] {e} @ frame {index}");
            }
        }

        lastEventFrame = index;
    
    }


}
