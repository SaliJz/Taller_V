using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(AnimDataBase))]
public class SpriteAnimator : MonoBehaviour
{
    SpriteRenderer sr;
    public AtlasLoader atlasLoader;
    public AnimDataLoader AnimLoader;
    AnimDataBase DataBase;

    AnimDataLoader.AnimData currentAnim;
    int frameIndex;
    float timer;

    public System.Action onAnimFinished;

    private void Awake()
    {
        DataBase = GetComponent<AnimDataBase>();
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

                string spriteKey = currentAnim.frames[frameIndex].frame;

                if( atlasLoader.spriteAtlas.TryGetValue(spriteKey, out Sprite sprite ))
                {
                    sr.sprite = sprite;
                }
            }
        }
    }

    public void LoadAnim(string id)
    {
        var def = DataBase.GetAnim(id);

        atlasLoader.LoadAtlas(def.sheet, def.atlasJSON);
        AnimLoader.LoadAnim(def.id, def.animJSON);
    }

    public void Play(string animID, string direction, bool reset = true)
    {
        currentAnim = AnimLoader.GetAnim(animID, direction);

        if (reset)
        {
            frameIndex = 0;
            timer = 0f;
        }

        Debug.Log($"{animID} - {direction} - {reset}");
    }


}
