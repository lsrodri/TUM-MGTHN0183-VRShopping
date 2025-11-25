using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        GUI.enabled = false; // Lock the GUI
        EditorGUI.PropertyField(position, property, label);
        GUI.enabled = true;  // Unlock for subsequent fields
    }
}
