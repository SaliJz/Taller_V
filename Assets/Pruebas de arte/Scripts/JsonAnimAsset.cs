using UnityEngine;

[CreateAssetMenu(fileName = "AnimAsset", menuName = "Sprite Animations/Anim Asset")]
public class JsonAnimAsset : ScriptableObject
{
    public string id;

    public Texture2D spriteSheet;
    public TextAsset atlasJson;
    public TextAsset animJson;
}
