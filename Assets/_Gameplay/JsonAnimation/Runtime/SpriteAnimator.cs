using System;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(PlayerAnimDataBase))]
public class SpriteAnimator : MonoBehaviour
{
    SpriteRenderer sr;
    PlayerAnimDataBase DataBase;

    JsonAnimAsset currentAsset;
    AnimParser.AnimData currentAnim;
    int frameIndex;
    float timer;
    [NonSerialized] public bool holdOnLastFrame;
    int lastEventFrame = -1;

    public Action onAnimFinished;
    public Action<string> onAnimEvent;

    private void Awake()
    {
        DataBase = GetComponent<PlayerAnimDataBase>();
        sr = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if(currentAnim == null) return;

        timer += Time.deltaTime;
        float frameTime = 1f / currentAnim.frameRate;

        if(timer >= frameTime) 
        {
            timer -= frameTime;

            if(holdOnLastFrame && frameIndex >= currentAnim.frames.Length - 1) return;
            
            frameIndex++;

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
            
            //Aplicar frame
            ApplyCurrentFrame();
        }
        
    }

    void ApplyCurrentFrame()
    {
        if (currentAnim == null || currentAsset == null) return;

        frameIndex = Mathf.Clamp(frameIndex, 0, currentAnim.frames.Length -1);

        // int spriteIndex = frameIndex;
        // if(spriteIndex < currentAsset.animLookup.Count)
        {
            sr.sprite = currentAnim.frames[frameIndex].sprite;
        }
    }
    public void LoadAnim(string id)
    {
        currentAsset = DataBase.GetAnim(id);

        if(currentAsset == null)
        {
            Debug.LogError($"Anim Asser {id} not found");
            return;
        }
    }

    public void Play(string animID, string direction, bool reset = true)
    {
        Debug.Log($"[SpriteAnimator] PLAY request -> animID: {animID} | direction {direction}");

        currentAsset = DataBase.GetAnim(animID);

        if(currentAsset == null)
        {
            Debug.LogError($"[SpriteAnimator] Asset NOT FOUND -> {animID}");
            return;
        }

        Debug.Log($"[SpriteAnimator] Asset FOUND -> {currentAsset.name}");

        currentAnim = currentAsset.GetDirection(direction);

        if(currentAnim == null)
        {
            Debug.LogError($"Direccion '{direction}' not found in asset {animID}");
            return;
        }

        Debug.Log($"[SpriteAnimator] Direction OK -> {direction} | frames: {currentAnim.frames.Length}");

        if (reset)
        {
            frameIndex = 0;
            timer = 0;
            lastEventFrame = -1;
        }

        ApplyCurrentFrame();
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
