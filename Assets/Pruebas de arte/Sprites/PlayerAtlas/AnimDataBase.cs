using System.Collections.Generic;
using UnityEngine;

public class AnimDataBase : MonoBehaviour
{
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
            animDict.Add(anim.id, anim);
        }
    }

    public AnimDef GetAnim(string id)
    {
        return animDict[id];
    }

    public bool AnimExist(string ID)
    {
        if (string.IsNullOrEmpty(ID)) return false;

        return animDict.ContainsKey(ID);
    }


}
