using System.Collections.Generic;
using UnityEngine;

public class AnimLoader: MonoBehaviour
{
    [System.Serializable]
    public class AnimFrame
    {
        public string key;
        public string frame;
        public string[] evnt;
    }

    [System.Serializable]
    public class AnimData
    {
        public string key;
        public string type;
        public int repeat;
        public int frameRate;
        public AnimFrame[] frames;
    }

    [System.Serializable]
    public class AnimFile
    {
        public AnimData[] anims;
    }

    public AnimFile Animations {  get; private set; }
    public Dictionary<string, Dictionary<string, AnimData>> animByDef = new Dictionary<string, Dictionary<string, AnimData>>();

    public void LoadAnim(string animID, TextAsset AnimJSON)
    {
        AnimFile file = JsonUtility.FromJson<AnimFile>(AnimJSON.text);

        var dirDict = new Dictionary<string, AnimData>();

        foreach (var anim in file.anims)
        {
            dirDict.Add(anim.key, anim);
        }

        animByDef[animID] = dirDict;

        Debug.Log($"[AnimDataLoader] AnimSet '{animID}' cargado con {dirDict.Count} direcciones");
    }

    public AnimData GetAnim(string animID, string direccion)
    {
        if (!animByDef.ContainsKey(animID))
        {
            Debug.LogError($"Animset {animID} no cargado");
        }

        if (!animByDef[animID].ContainsKey(direccion))
        {
            Debug.LogError($"Animset {animID} no tiene direccion: {direccion}");
            return null;
        }

        return animByDef[animID][direccion];
    }
}
