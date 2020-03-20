#region

using UnityEditor;
using UnityEngine;

#endregion

namespace Wyd.System
{
    public class ReadOnlyInspectorField : PropertyAttribute { }

    [CustomPropertyDrawer(typeof(ReadOnlyInspectorField))]
    public class ReadOnlyInspectorFieldDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUI.GetPropertyHeight(property, label, true);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false;
            EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = true;
        }
    }
}
