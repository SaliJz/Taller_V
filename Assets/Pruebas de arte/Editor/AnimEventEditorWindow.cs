using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class AnimEventEditorWindow : EditorWindow
{
    AnimJson.AnimJsonData animData;
    AtlasJson.AtlasData atlasData;
    Dictionary<string, AtlasJson.AtlasFrame> atlasLookup;
    Texture2D spriteSheet;
    TextAsset atlasJson;
    TextAsset animJsonFile;
    TextAsset LASTanimJsonFile;
    bool HasUnsavedChanges = false;

    int selectedDirectionIndex = 0;
    string[] directionKeys;

    int selectedFrameIndex = -1;
    Vector2 framesScroll;

    Color SelectedFrameColor = new Color(0.3f, 0.8f, 1f);
    Color EventIndicatorColor = new Color(1f, 0.6f, 0.1f);

    const string EVENT_COLOR_PREF = "AninEventEditor_EventColor";
    

    [MenuItem("Tools/Animation/Anim Event Editor")]
    public static void Open()
    {
        GetWindow<AnimEventEditorWindow>("Anim Event Editor");
    }

    void OnEnable()
    {
        LoadEventColor();
    }

    void OnGUI()
    {
        DrawTopBar();
        EditorGUILayout.Space(10);

        if(animData == null)
        {
            EditorGUILayout.HelpBox("Carga un anim.json para editar", MessageType.Info);
            return;
        }

        EditorGUILayout.BeginHorizontal();

            /////IZQUIERDA
            EditorGUILayout.BeginVertical(GUILayout.Width((position.width * 0.6f)));
                
                DrawClipSelector();
                DrawClipInfo();
                EditorGUILayout.Space(10);

                DrawFramesTimeline();
                EditorGUILayout.Space(6);

                DrawFrameInspector();
            EditorGUILayout.EndVertical();

        /////DERECHA
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

                drawFramePreview();
                // EditorGUILayout.Space(12);

            EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }

    void loadJson()
    {
        if (animJsonFile == null)
        {
            Debug.LogWarning("No Anim.Json selected");

            animData = null;
            directionKeys = null;
            return;
        }

        animData = JsonUtility.FromJson<AnimJson.AnimJsonData>(animJsonFile.text);

        if(animData == null || animData.anims == null)
        {
            Debug.LogError("Anim.json invalido o imcompatible");
            animData = null;
            directionKeys = null;
            return;
        }

        selectedDirectionIndex = 0;
        selectedFrameIndex = -1;

        directionKeys = animData.anims.Select(a => a.key).ToArray();
        HasUnsavedChanges = false;
    }
    void loadAtlas()
    {
        atlasData = null;
        atlasLookup = null;

        if (atlasJson == null) return;
        

        atlasData = JsonUtility.FromJson<AtlasJson.AtlasData>(atlasJson.text);

        if(atlasData == null || atlasData.frames == null) return;

        atlasLookup = new Dictionary<string, AtlasJson.AtlasFrame>();
        foreach(var f in atlasData.frames)
        {
            if (!atlasLookup.ContainsKey(f.filename))
            {
                atlasLookup.Add(f.filename, f);
            }
        }

        // foreach(var key in atlasLookup.Keys.Take(10))
        // {
        //     Debug.Log($"[Atlas] Frame disponible {key}");
        // }
    }

    void saveJson()
    {
        if (animData == null || animJsonFile == null)
        {
            Debug.Log("Nothing to save");
            return;
        } 

        ApplyDataToAllDirections();

        string json = JsonUtility.ToJson(animData, true);
        string path = AssetDatabase.GetAssetPath(animJsonFile);

        System.IO.File.WriteAllText(path, json);
        AssetDatabase.Refresh();

        HasUnsavedChanges = false;

        EditorUtility.SetDirty(animJsonFile);
        AssetDatabase.SaveAssets();

        Debug.Log("AnimJson Saved");
    }

    void ApplyDataToAllDirections()
    {
        var sourceClip = animData.anims[selectedDirectionIndex];

        for (int i = 0; i < animData.anims.Length; i++)
        {
            if(i == selectedDirectionIndex) continue;

            animData.anims[i].frameRate = sourceClip.frameRate;
            animData.anims[i].repeat = sourceClip.repeat;
        }

        Debug.Log("FrameRate y Repeat aplicado a todas las direcciones");
    }

    bool hasValidClip()
    {
        if (animData == null) return false;
        if (animData.anims == null) return false;
        if (animData.anims.Length == 0) return false;
        if (selectedDirectionIndex < 0) return false;
        if (selectedDirectionIndex >= animData.anims.Length) return false;

        return true;
    }

    void OnAnimJsonChanged()
    {
        if(animJsonFile == LASTanimJsonFile) return;

        if (HasUnsavedChanges)
        {
            int options = EditorUtility.DisplayDialogComplex("Cambios sin guardar", "Hay cambios sin guardar en el anim.json actual. \n\n ¿Deseas guardar?",
            "Guardar y continuar", "Cancelar", "No guardar");

            switch (options)
            {
                case 0: saveJson(); break;
                case 1: animJsonFile = LASTanimJsonFile; return;
                case 2: break;
            }
        }

        LASTanimJsonFile = animJsonFile;
        loadJson();

    }

    void DrawTopBar()
    {
        string title = HasUnsavedChanges ? "Anim Event Editor *" : "Anim Event Editor";
        titleContent = new GUIContent(title);

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        //// ANIM JSON
        EditorGUI.BeginChangeCheck();
        animJsonFile = (TextAsset)EditorGUILayout.ObjectField("Anim.json",animJsonFile, typeof(TextAsset), false, GUILayout.Width(300));
        if (EditorGUI.EndChangeCheck())
        {
            OnAnimJsonChanged();
        }

        //// ATLAS Y SPRITESHEET
        EditorGUI.BeginChangeCheck();
        atlasJson = (TextAsset)EditorGUILayout.ObjectField("Atlas.json",atlasJson, typeof(TextAsset), false, GUILayout.Width(300));
        spriteSheet = (Texture2D)EditorGUILayout.ObjectField("Sprite Sheet",spriteSheet, typeof(Texture2D), false, GUILayout.Width(300));

        if(EditorGUI.EndChangeCheck()) 
        {
            loadAtlas();
        }

        EditorGUILayout.EndHorizontal();

        //// EVENT COLOR
        EditorGUILayout.Space(4);
        EditorGUI.BeginChangeCheck();
        EventIndicatorColor = EditorGUILayout.ColorField(new GUIContent("Event Color"), EventIndicatorColor, GUILayout.Width(220));
        if (EditorGUI.EndChangeCheck())
        {
            SaveEventColor();
        }

        //// SAVE
        GUI.enabled = animData != null && HasUnsavedChanges;
        if(GUILayout.Button("Save", EditorStyles.toolbarButton))
        {
            saveJson();
        }
        GUI.enabled = true;

    }

    void DrawClipSelector()
    {
        int newIndex = EditorGUILayout.Popup("Direction", selectedDirectionIndex, directionKeys);

        if (newIndex != selectedDirectionIndex)
        {
            selectedDirectionIndex =  newIndex;
            selectedFrameIndex = -1;
        }
    }

    void DrawClipInfo()
    {
        if (!hasValidClip()) return;

        var clip = animData.anims[selectedDirectionIndex];

        EditorGUI.BeginChangeCheck();

        clip.frameRate = EditorGUILayout.IntField("Frame Rate", clip.frameRate);
        clip.repeat = EditorGUILayout.IntField("Repeat", clip.repeat);

        if(EditorGUI.EndChangeCheck()) HasUnsavedChanges = true;

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if(GUILayout.Button("Apply to all directions", GUILayout.Width(180)))
        {
            ApplyDataToAllDirections();
            HasUnsavedChanges = true;
        }

        EditorGUILayout.EndHorizontal();

    }

    void DrawFramesTimeline()
    {
        if (!hasValidClip()) return;

        var clip = animData.anims[selectedDirectionIndex];

        float timelineHeight = 100f;

        framesScroll = EditorGUILayout.BeginScrollView(framesScroll, alwaysShowHorizontal: true, alwaysShowVertical: false, 
            GUILayout.Height(timelineHeight));

            EditorGUILayout.BeginHorizontal();

            for(int i = 0; i < clip.frames.Count; i++)
            {
                DrawFrameButton(i, clip.frames[i]);
            }

            EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
    }

    bool TryGetSelectedAtlasFrame (out AtlasJson.AtlasFrame atlasFrame)
    {
        atlasFrame = null;

        if (!hasValidClip()) return false;
        if (selectedFrameIndex < 0) return false;
        if (atlasData == null) return false;
        if (atlasLookup == null) return false;
 
        var clip = animData.anims[selectedDirectionIndex];
        if (selectedDirectionIndex >= clip.frames.Count) return false;

        string frameName = clip.frames[selectedFrameIndex].frame;

        if (string.IsNullOrEmpty(frameName)) return false;

        return atlasLookup.TryGetValue(frameName, out atlasFrame);
    }

    void drawFramePreview()
    {
        GUILayout.Label("Preview", EditorStyles.boldLabel);

        if (selectedFrameIndex < 0)
        {
            EditorGUILayout.HelpBox("Selecciona un frame para ver el preview", MessageType.Info);
            return;
        }
        if (atlasJson == null || spriteSheet == null)
        {
            EditorGUILayout.HelpBox("Asigna un AtlasJson/SpriteSheet para ver el preview", MessageType.Info);
            return;
        }
        if (!TryGetSelectedAtlasFrame(out var atlasFrame))
        {
            EditorGUILayout.HelpBox("No hay frame valido para preview", MessageType.None);
            return;
        }

        Rect previewRect = GUILayoutUtility.GetRect(350, 350, GUILayout.ExpandWidth(false));
        Rect texCoords = new Rect(atlasFrame.frame.x / (float)spriteSheet.width,
            1f - (atlasFrame.frame.y + atlasFrame.frame.h) / (float)spriteSheet.height, 
            (float)atlasFrame.frame.w / spriteSheet.width, (float)atlasFrame.frame.h / spriteSheet.height);

        GUI.DrawTextureWithTexCoords(previewRect, spriteSheet, texCoords, true);
        
        // GUI.Box(previewRect, GUIContent.none);
    }

    void DrawFrameButton(int index, AnimJson.AnimFrameData frame)
    {
        bool isSelected = index == selectedFrameIndex;
        bool hasEvents = frame.evnt != null && frame.evnt.Length > 0;

        float size = 60f;

        GUIStyle style = new GUIStyle(GUI.skin.button);
        style.fixedHeight = size;
        style.fixedWidth = size;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = Color.white;

        Rect rect = GUILayoutUtility.GetRect(style.fixedWidth, style.fixedHeight, GUILayout.ExpandWidth(false));

        if(GUI.Button(rect, index.ToString(), style))
        {
            selectedFrameIndex = index;
            Repaint();
        }

        drawEvenIndicator(hasEvents, rect);

        if (isSelected)
        {
            drawSelectionBorder(rect);
        }
    }

    void drawEvenIndicator(bool hasEvents, Rect frameRect)
    {
        if (!hasEvents) return;

        Rect indicator = new Rect(frameRect.x + frameRect.width * 0.5f - 6, frameRect.yMax - 10, 12, 6);

        EditorGUI.DrawRect(indicator, EventIndicatorColor);
    }

    void LoadEventColor()
    {
        if (EditorPrefs.HasKey(EVENT_COLOR_PREF))
        {
            ColorUtility.TryParseHtmlString(EditorPrefs.GetString(EVENT_COLOR_PREF), out EventIndicatorColor);
        }
    }
    void SaveEventColor()
    {
        EditorPrefs.SetString(EVENT_COLOR_PREF, "#"+ ColorUtility.ToHtmlStringRGBA(EventIndicatorColor));
    }

    void drawSelectionBorder(Rect rect)
    {
        float thickness = 2f;
        Color color = SelectedFrameColor;

        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);

        
    }

    void DrawFrameInspector()
    {
        if (!hasValidClip()) return;
        if (selectedFrameIndex < 0) return;

        var clip = animData.anims[selectedDirectionIndex];
        var frame = clip.frames[selectedFrameIndex];

        EditorGUILayout.LabelField($"Frame {selectedFrameIndex}", EditorStyles.boldLabel);

        if(frame.evnt == null) frame.evnt = new string[0];

        if (frame.evnt.Length == 0)
        {
            EditorGUILayout.HelpBox("Este frame no tiene eventos de animacion", MessageType.Info);
        }
        
        int removeIndex = -1;

            for (int i = 0; i < frame.evnt.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();

                    frame.evnt[i] = EditorGUILayout.TextField($"Event {i}", frame.evnt[i]);

                    if(GUILayout.Button("X", GUILayout.Width(28)))
                    {
                        removeIndex = i;
                    }

                EditorGUILayout.EndHorizontal();
            }

            if(removeIndex != -1)
            {
                RemoveEventAt(ref frame.evnt, removeIndex);
                HasUnsavedChanges = true;
                Repaint();
            }

        if (GUILayout.Button("+ Add Event"))
        {
            AddEvent(ref frame.evnt);
        }

        EditorGUILayout.Space(6);
        if(GUILayout.Button("Duplicate events to all directions"))
        {
            DuplicateEventsToAllDirections();
            HasUnsavedChanges = true;
        }
    }

    void AddEvent(ref string[] eventArray)
    {
        System.Array.Resize(ref eventArray, eventArray.Length + 1);
        eventArray[eventArray.Length - 1] = "";
    }
    void RemoveEventAt(ref string[] eventArray, int index)
    {
        var list = eventArray.ToList();
        list.RemoveAt(index);
        eventArray = list.ToArray();
    }

    void DuplicateEventsToAllDirections()
    {
        if (!hasValidClip()) return;
        if (selectedFrameIndex < 0) return;

        var sourceClip = animData.anims[selectedDirectionIndex];
        var sourceFrame = sourceClip.frames[selectedFrameIndex];

        if(sourceFrame.evnt == null || sourceFrame.evnt.Length == 0) return;

        for (int i = 0; i < animData.anims.Length; i++)
        {
            if (i == selectedDirectionIndex) continue;

            var targetClip = animData.anims[i];

            if(selectedFrameIndex >= targetClip.frames.Count) continue;

            targetClip.frames[selectedFrameIndex].evnt = (string[])sourceFrame.evnt.Clone();
        }
    }
}
