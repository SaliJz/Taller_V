using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SaveGamePanel))]
public class SaveGamePanelEditor : Editor
{
    private SerializedProperty displayTypeProp;
    private SerializedProperty canvasGroupProp;
    private SerializedProperty saveSlotButtonsProp;
    private SerializedProperty deleteButtonsProp; 
    private SerializedProperty openCloseDurationProp;
    private SerializedProperty openEaseProp;
    private SerializedProperty closeEaseProp;
    private SerializedProperty startScaleProp;
    private SerializedProperty endScaleProp;
    private SerializedProperty firstSelectedButtonProp;

    private void OnEnable()
    {
        displayTypeProp = serializedObject.FindProperty("displayType");
        canvasGroupProp = serializedObject.FindProperty("canvasGroup");
        saveSlotButtonsProp = serializedObject.FindProperty("saveSlotButtons");
        deleteButtonsProp = serializedObject.FindProperty("deleteButtons"); 
        openCloseDurationProp = serializedObject.FindProperty("openCloseDuration");
        openEaseProp = serializedObject.FindProperty("openEase");
        closeEaseProp = serializedObject.FindProperty("closeEase");
        startScaleProp = serializedObject.FindProperty("startScale");
        endScaleProp = serializedObject.FindProperty("endScale");
        firstSelectedButtonProp = serializedObject.FindProperty("firstSelectedButton");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Panel Display Settings", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(displayTypeProp, new GUIContent("Display Type"));
        EditorGUILayout.PropertyField(canvasGroupProp, new GUIContent("Canvas Group Ref"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Save Slot Buttons", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(saveSlotButtonsProp, true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Delete Buttons", EditorStyles.boldLabel); 
        EditorGUILayout.PropertyField(deleteButtonsProp, true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("DOTween Settings", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(openCloseDurationProp, new GUIContent("Duration"));
        EditorGUILayout.PropertyField(openEaseProp, new GUIContent("Open Ease"));
        EditorGUILayout.PropertyField(closeEaseProp, new GUIContent("Close Ease"));

        if (displayTypeProp.enumValueIndex == (int)SaveGamePanel.PanelDisplayType.AnimatedScale)
        {
            EditorGUILayout.PropertyField(startScaleProp, new GUIContent("Start Scale"));
            EditorGUILayout.PropertyField(endScaleProp, new GUIContent("End Scale"));
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Focus Control (Gamepad)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(firstSelectedButtonProp, new GUIContent("First Selected Button"));

        serializedObject.ApplyModifiedProperties();
    }
}