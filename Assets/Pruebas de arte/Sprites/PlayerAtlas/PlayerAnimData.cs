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

    // public AnimDef[] animations;
    // Dictionary<string, AnimDef> animDict = new Dictionary<string, AnimDef>();

    [Header("Animation Assets")]
    Dictionary<string, JsonAnimAsset> animLookup = new Dictionary<string, JsonAnimAsset>();
    public JsonAnimAsset[] Assets;

    private void Awake()
    {
        BuildDataBase();

        // foreach (var anim in animations)
        // {
        //     if (!animDict.ContainsKey(anim.id))
        //     {
        //         animDict.Add(anim.id, anim);
        //     } 
        // }
    }

    // public AnimDef GetAnim(string id) => animDict.TryGetValue(id, out var def)? def : null;

    // public bool AnimExist(string ID) => animDict.ContainsKey(ID);

    void BuildDataBase()
    {
        foreach(var asset in Assets)
        {
            if (asset == null || string.IsNullOrEmpty(asset.id)) continue;

            if (!animLookup.ContainsKey(asset.id))
            {
                animLookup.Add(asset.id, asset);
                // Debug.Log($"[DB] ADD -> {asset.id} | SHEET: {asset.spriteSheet} | ATLAS {asset.atlasJson} | ANIM {asset.animJson}");
            }
            else
            {
                Debug.LogWarning($"Duplicated animations ID: {asset.id}");
            }
        }

        // Debug.Log($"[DB] TOTAL ANIMS: {animLookup.Count}");
        // Debug.Log($"[DB] HAS begin:idle1? {animLookup.ContainsKey("begin:idle1")}");
    }

    public JsonAnimAsset GetAnim(string ID)
    {
        if (animLookup.TryGetValue(ID, out var asset))
        {
            // Debug.Log($"[DB] GET OK -> '{ID}'");
            return asset;
        }

        Debug.LogError($"[DB] GET FAIL ->' {ID}' not found");
        // Debug.Log("[DB] KEYS");
        // foreach(var k in animLookup.Keys)
        // {
        //     Debug.Log($"    - {k}");
        // }
        return null;
    }

    public bool AnimExist(string ID) => animLookup.ContainsKey(ID);

}
