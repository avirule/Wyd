#region

using System;
using UnityEngine;

#endregion

namespace Wyd.System
{
    [AttributeUsage(AttributeTargets.Field)]
    public class ReadOnlyInspectorField : PropertyAttribute { }

#if UNITY_EDITOR
    [UnityEditor.CustomPropertyDrawer(typeof(ReadOnlyInspectorField))]
    public class ReadOnlyInspectorFieldDrawer : UnityEditor.PropertyDrawer
    {
        public override float GetPropertyHeight(UnityEditor.SerializedProperty property, GUIContent label) =>
            UnityEditor.EditorGUI.GetPropertyHeight(property, label, true);

        public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false;
            UnityEditor.EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = true;
        }
    }
#endif
}
