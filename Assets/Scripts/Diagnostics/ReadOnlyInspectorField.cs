#region

using System;
using UnityEditor;
using UnityEngine;

#endregion

namespace Wyd.Diagnostics
{
    [AttributeUsage(AttributeTargets.Field)]
    public class ReadOnlyInspectorField : PropertyAttribute { }

#if UNITY_EDITOR
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
#endif
}
