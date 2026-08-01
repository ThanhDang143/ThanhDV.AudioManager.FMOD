using UnityEditor;
using UnityEngine;

namespace ThanhDV.AudioConductor.FMOD
{
    [CustomPropertyDrawer(typeof(BusEntry))]
    public class BusEntryDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            var keyProp = property.FindPropertyRelative(nameof(BusEntry.Key));
            var busPathProp = property.FindPropertyRelative(nameof(BusEntry.BusPath));

            float height = EditorGUIUtility.singleLineHeight;
            height += EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(keyProp, includeChildren: true);
            height += EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(busPathProp, includeChildren: true);
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var keyProp = property.FindPropertyRelative(nameof(BusEntry.Key));
            var busPathProp = property.FindPropertyRelative(nameof(BusEntry.BusPath));

            string key = keyProp?.stringValue;
            var displayLabel = string.IsNullOrWhiteSpace(key) ? label : new GUIContent(key);

            using (new EditorGUI.PropertyScope(position, displayLabel, property))
            {
                var foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
                property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, displayLabel, true);

                if (!property.isExpanded)
                    return;

                EditorGUI.indentLevel++;

                float y = foldoutRect.yMax + EditorGUIUtility.standardVerticalSpacing;

                float keyHeight = EditorGUI.GetPropertyHeight(keyProp, includeChildren: true);
                var keyRect = new Rect(position.x, y, position.width, keyHeight);
                EditorGUI.PropertyField(keyRect, keyProp, includeChildren: true);
                y = keyRect.yMax + EditorGUIUtility.standardVerticalSpacing;

                float busHeight = EditorGUI.GetPropertyHeight(busPathProp, includeChildren: true);
                var busRect = new Rect(position.x, y, position.width, busHeight);
                EditorGUI.PropertyField(busRect, busPathProp, includeChildren: true);

                EditorGUI.indentLevel--;
            }
        }
    }
}