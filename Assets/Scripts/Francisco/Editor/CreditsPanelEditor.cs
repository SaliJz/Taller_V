using UnityEditor;

[CustomEditor(typeof(CreditsPanel))]
public class CreditsPanelEditor : Editor
{
    SerializedProperty displayTypeProp;
    SerializedProperty canvasGroupProp;

    SerializedProperty panelAnimatorProp;
    SerializedProperty openTriggerProp;
    SerializedProperty closeTriggerProp;

    SerializedProperty openCloseDurationProp;
    SerializedProperty openEaseProp;
    SerializedProperty closeEaseProp;
    SerializedProperty startScaleProp;
    SerializedProperty endScaleProp;

    SerializedProperty firstSelectedButtonProp;

    SerializedProperty creditsContainerProp;
    SerializedProperty scrollRectProp;
    SerializedProperty scrollSpeedProp;
    SerializedProperty autoScrollDelayProp;

    SerializedProperty creditsEntriesProp;

    SerializedProperty titlePrefabProp;
    SerializedProperty subtitlePrefabProp;
    SerializedProperty rolePrefabProp;
    SerializedProperty namePrefabProp;
    SerializedProperty roleWithNamePrefabProp;
    SerializedProperty imageWithTextPrefabProp;
    SerializedProperty spacerPrefabProp;

    SerializedProperty defaultSpacingProp;
    SerializedProperty titleColorProp;
    SerializedProperty subtitleColorProp;
    SerializedProperty roleColorProp;
    SerializedProperty nameColorProp;

    private void OnEnable()
    {
        displayTypeProp = serializedObject.FindProperty("displayType");
        canvasGroupProp = serializedObject.FindProperty("canvasGroup");

        panelAnimatorProp = serializedObject.FindProperty("panelAnimator");
        openTriggerProp = serializedObject.FindProperty("openTrigger");
        closeTriggerProp = serializedObject.FindProperty("closeTrigger");

        openCloseDurationProp = serializedObject.FindProperty("openCloseDuration");
        openEaseProp = serializedObject.FindProperty("openEase");
        closeEaseProp = serializedObject.FindProperty("closeEase");
        startScaleProp = serializedObject.FindProperty("startScale");
        endScaleProp = serializedObject.FindProperty("endScale");

        firstSelectedButtonProp = serializedObject.FindProperty("firstSelectedButton");

        creditsContainerProp = serializedObject.FindProperty("creditsContainer");
        scrollRectProp = serializedObject.FindProperty("scrollRect");
        scrollSpeedProp = serializedObject.FindProperty("scrollSpeed");
        autoScrollDelayProp = serializedObject.FindProperty("autoScrollDelay");

        creditsEntriesProp = serializedObject.FindProperty("creditsEntries");

        titlePrefabProp = serializedObject.FindProperty("titlePrefab");
        subtitlePrefabProp = serializedObject.FindProperty("subtitlePrefab");
        rolePrefabProp = serializedObject.FindProperty("rolePrefab");
        namePrefabProp = serializedObject.FindProperty("namePrefab");
        roleWithNamePrefabProp = serializedObject.FindProperty("roleWithNamePrefab");
        imageWithTextPrefabProp = serializedObject.FindProperty("imageWithTextPrefab");
        spacerPrefabProp = serializedObject.FindProperty("spacerPrefab");

        defaultSpacingProp = serializedObject.FindProperty("defaultSpacing");
        titleColorProp = serializedObject.FindProperty("titleColor");
        subtitleColorProp = serializedObject.FindProperty("subtitleColor");
        roleColorProp = serializedObject.FindProperty("roleColor");
        nameColorProp = serializedObject.FindProperty("nameColor");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        CreditsPanel.PanelDisplayType currentDisplayType = (CreditsPanel.PanelDisplayType)displayTypeProp.enumValueIndex;

        EditorGUILayout.LabelField("Panel Display Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(displayTypeProp);

        if (currentDisplayType == CreditsPanel.PanelDisplayType.CanvasFade)
        {
            EditorGUILayout.PropertyField(canvasGroupProp);
            EditorGUILayout.Space();
        }

        if (currentDisplayType == CreditsPanel.PanelDisplayType.Animator)
        {
            EditorGUILayout.PropertyField(panelAnimatorProp);
            EditorGUILayout.PropertyField(openTriggerProp);
            EditorGUILayout.PropertyField(closeTriggerProp);
        }

        if (currentDisplayType != CreditsPanel.PanelDisplayType.Static && currentDisplayType != CreditsPanel.PanelDisplayType.Animator)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("DOTween Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(openCloseDurationProp);
            EditorGUILayout.PropertyField(openEaseProp);
            EditorGUILayout.PropertyField(closeEaseProp);

            if (currentDisplayType == CreditsPanel.PanelDisplayType.AnimatedScale)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Scale Animation Vectors", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(startScaleProp);
                EditorGUILayout.PropertyField(endScaleProp);
            }
            else if (currentDisplayType == CreditsPanel.PanelDisplayType.CanvasFade)
            {
                EditorGUILayout.HelpBox("Canvas Fade mode active. Animation uses Canvas Group Alpha. Scale vectors are hidden.", MessageType.Info);
            }
        }

        if (currentDisplayType == CreditsPanel.PanelDisplayType.Static)
        {
            EditorGUILayout.HelpBox("Static mode selected. Panel will simply activate/deactivate without animation.", MessageType.Info);
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Focus Control", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(firstSelectedButtonProp);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Credits Container", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(creditsContainerProp);
        EditorGUILayout.PropertyField(scrollRectProp);
        EditorGUILayout.PropertyField(scrollSpeedProp);
        EditorGUILayout.PropertyField(autoScrollDelayProp);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Prefab References", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(titlePrefabProp);
        EditorGUILayout.PropertyField(subtitlePrefabProp);
        EditorGUILayout.PropertyField(rolePrefabProp);
        EditorGUILayout.PropertyField(namePrefabProp);
        EditorGUILayout.PropertyField(roleWithNamePrefabProp);
        EditorGUILayout.PropertyField(imageWithTextPrefabProp);
        EditorGUILayout.PropertyField(spacerPrefabProp);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Style Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(defaultSpacingProp);
        EditorGUILayout.PropertyField(titleColorProp);
        EditorGUILayout.PropertyField(subtitleColorProp);
        EditorGUILayout.PropertyField(roleColorProp);
        EditorGUILayout.PropertyField(nameColorProp);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Credits Content", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(creditsEntriesProp, true);

        serializedObject.ApplyModifiedProperties();
    }
}