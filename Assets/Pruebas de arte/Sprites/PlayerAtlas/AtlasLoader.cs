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
        AtlasFile atlas = JsonUtility.FromJson<AtlasFile>(AtlasJSON.text);

        foreach(var f in atlas.frames)
        {
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
        }
    }

    public Sprite GetSprite(string frameName)
    {
        spriteAtlas.TryGetValue(frameName, out var sprite);
        return sprite;
    }
}
