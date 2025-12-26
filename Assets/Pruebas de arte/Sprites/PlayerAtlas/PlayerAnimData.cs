using System.Collections.Generic;
using UnityEngine;

public class PlayerAnimData : MonoBehaviour
{
    public enum Age
    {
        young, adult, old
    }

    [System.Serializable]
    public class AnimDef
    {
        public string id;
        public Texture2D sheet;
        public TextAsset atlasJSON;
        public TextAsset animJSON;
    }

    public AnimDef[] animations;
    Dictionary<string, AnimDef> animDict = new Dictionary<string, AnimDef>();

    private void Awake()
    {
        foreach (var anim in animations)
        {
            if (!animDict.ContainsKey(anim.id))
            {
                animDict.Add(anim.id, anim);
            } 
        }
    }

    // public void loadAllAges(SpriteAnimator SA)
    // {
    //     foreach (var kvp in animDict)
    //     {
    //         string id = kvp.Key;
    //         var set = kvp.Value;

    //         SA.LoadAnim(id, set.young, set.adult, set.old);
    //     }
    // }
    public AnimDef GetAnim(string id) => animDict.TryGetValue(id, out var def)? def : null;

    public bool AnimExist(string ID) => animDict.ContainsKey(ID);
    // {
    //     // if (string.IsNullOrEmpty(ID)) return false;

    //     // return animDict.ContainsKey(ID);

    //     return animDict.ContainsKey(ID);
    // }


}
