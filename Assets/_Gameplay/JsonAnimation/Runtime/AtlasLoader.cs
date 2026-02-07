using System.Collections.Generic;
using UnityEngine;

public class AtlasLoader : MonoBehaviour
{
    [System.Serializable]
    public class AtlasFrameRect
    {
        public int x, y, w, h;
    }

    [System.Serializable]
    public class AtlasFrame
    {
        public string filename;
        public AtlasFrameRect frame;
    }

    [System.Serializable]
    public class AtlasFile
    {
        public AtlasFrame[] frames;
    }

    public Dictionary<string, Sprite> spriteAtlas = new Dictionary<string, Sprite>();
    

    public void LoadAtlas(Texture2D sheet, TextAsset AtlasJSON)
    {
        // Debug.Log($"[ATLAS] LOAD START -> sheet = {sheet?.name} | atlas = {AtlasJSON?.name}");

        AtlasFile atlas = JsonUtility.FromJson<AtlasFile>(AtlasJSON.text);

        // Debug.Log($"[ATLAS] CLEARING atlas. Previous count = {spriteAtlas.Count}");
        // spriteAtlas.Clear();

        foreach(var f in atlas.frames)
        {
            // Debug.Log($"[ATLAS] ADD FRAME -> {f.filename}");

            Rect rect = new Rect(
                f.frame.x, //x
                sheet.height - f.frame.y - f.frame.h, //invert y
                f.frame.w, //w
                f.frame.h //h
                );

            Sprite sprite = Sprite.Create(
                sheet,
                rect,
                new Vector2(0.5f, 0.5f), 100f);

            spriteAtlas[f.filename] = sprite;

            // Debug.Log($"[ATLAS] LOAD END -> total frames = {spriteAtlas.Count}");
        }
    }

    public Sprite GetSprite(string frameName)
    {
        return spriteAtlas[frameName];
    }
}
