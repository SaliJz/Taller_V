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
                // Debug.Log($"[DB] ADD -> {a.id} | SHEET: {a.spriteSheet} | ATLAS {a.atlasJson} | ANIM {a.animJson}");
            }
            else
            {
                Debug.LogWarning($"Duplicated animations ID: {a.id}");
            }
        }

        // Debug.Log($"[DB] TOTAL ANIMS: {animLookup.Count}");
        // Debug.Log($"[DB] HAS begin:idle1? {animLookup.ContainsKey("begin:idle1")}");
    }

    public JsonAnimAsset GetAnim(string ID)
    {
        // Debug.Log($"[DB] Searching asset -> {ID}");

        foreach(var a in assets)
        {
            // Debug.Log($"[DB] Asset Available -> {a.id}");
            if(a.id == ID) break;
        }

        foreach(var a in assets)
        {
            if(a.id == ID)
            {
                // Debug.Log($"[DB] Asset Found {a.id}");
                return a;
            }
        }

        // Debug.LogError($"[DB] Asset NOT FOUND ->'{ID}'");
        
        return null;
    }

    public bool AnimExist(string ID) => animLookup.ContainsKey(ID);

}
