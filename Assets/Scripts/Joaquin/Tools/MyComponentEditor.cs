using UnityEditor;
using UnityEngine;

public class MyComponentEditor : Editor
{
    SerializedProperty myProp;

    void OnEnable()
    {
        // Protege contra targets nulos / recompilaciones
        if (targets == null || targets.Length == 0) return;
        if (targets[0] == null) return; // evita crear serializedObject para algo nulo

        // A partir de aqu� es seguro usar serializedObject
        myProp = serializedObject.FindProperty("myField");
    }

    public override void OnInspectorGUI()
    {
        // Protecci�n adicional
        if (serializedObject == null) return;

        serializedObject.Update();
        EditorGUILayout.PropertyField(myProp);
        serializedObject.ApplyModifiedProperties();
    }
}