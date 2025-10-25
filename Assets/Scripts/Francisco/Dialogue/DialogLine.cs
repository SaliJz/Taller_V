using UnityEngine;

[System.Serializable]
public class DialogLine
{
    public string CharacterName = "NAME NPC";

    public Sprite ProfileImage;

    [TextArea(3, 5)]
    public string Text = "Texto de la l�nea de di�logo.";

    [Header("Behavior")]
    public bool WaitForInput = true;
    public float AutoAdvanceDelay = 0.5f;
}