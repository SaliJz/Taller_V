using System.Collections.Generic;
using UnityEngine;

public class PlayerAnimDataBase : MonoBehaviour
{
    Dictionary<string, JsonAnimAsset> animLookup = new Dictionary<string, JsonAnimAsset>();
    public JsonAnimAsset[] assets;

    public enum Age
    {
        young, adult, old
    }

    // [System.Serializable]
    // public class AnimDef
    // {
    //     public string id;
    //     public Texture2D sheet;
    //     public TextAsset atlasJSON;
    //     public TextAsset animJSON;
    // }

    // [Header("Animation Assets")]
    // Dictionary<string, JsonAnimAsset> animLookup = new Dictionary<string, JsonAnimAsset>();
    // public JsonAnimAsset[] Assets;

    private void Awake()
    {
        BuildDataBase();
    }

    void BuildDataBase()
    {
        foreach(var a in assets)
        {
            if (a == null || string.IsNullOrEmpty(a.id)) continue;

            if (!animLookup.ContainsKey(a.id))
            {
                animLookup.Add(a.id, a);
                Debug.Log($"[DB] ADD -> {a.id} | SHEET: {a.spriteSheet} | ATLAS {a.atlasJson} | ANIM {a.animJson}");
            }
            else
            {
                Debug.LogWarning($"Duplicated animations ID: {a.id}");
            }
        }

        Debug.Log($"[DB] TOTAL ANIMS: {animLookup.Count}");
        Debug.Log($"[DB] HAS begin:idle1? {animLookup.ContainsKey("begin:idle1")}");
    }

    public JsonAnimAsset GetAnim(string ID)
    {
        Debug.Log($"[DB] Searching asset -> {ID}");

        foreach(var a in assets)
        {
            Debug.Log($"[DB] Asset Available -> {a.id}");
            if(a.id == ID) break;
        }

        foreach(var a in assets)
        {
            if(a.id == ID)
            {
                Debug.Log($"[DB] Asset Found {a.id}");
                return a;
            }
        }

        // if (animLookup.TryGetValue(ID, out var asset))
        // {
        //     // Debug.Log($"[DB] GET OK -> '{ID}'");
        //     return asset;
        // }

        Debug.LogError($"[DB] Asset NOT FOUND ->'{ID}'");
        // Debug.Log("[DB] KEYS");
        // foreach(var k in animLookup.Keys)
        // {
        //     Debug.Log($"    - {k}");
        // }
        return null;
    }

    public bool AnimExist(string ID) => animLookup.ContainsKey(ID);

}
