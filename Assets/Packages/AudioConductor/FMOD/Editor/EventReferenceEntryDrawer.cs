using UnityEditor;
using UnityEngine;

namespace ThanhDV.AudioConductor.FMOD
{
    [CustomPropertyDrawer(typeof(EventReferenceEntry))]
    public class EventReferenceEntryDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            var keyProp = property.FindPropertyRelative(nameof(EventReferenceEntry.Key));
            var eventRefProp = property.FindPropertyRelative(nameof(EventReferenceEntry.EventReference));

            float height = EditorGUIUtility.singleLineHeight;
            height += EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(keyProp, includeChildren: true);
            height += EditorGUIUtility.standardVerticalSpacing + EditorGUI.GetPropertyHeight(eventRefProp, includeChildren: true);
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var keyProp = property.FindPropertyRelative(nameof(EventReferenceEntry.Key));
            var eventRefProp = property.FindPropertyRelative(nameof(EventReferenceEntry.EventReference));

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

                float eventHeight = EditorGUI.GetPropertyHeight(eventRefProp, includeChildren: true);
                var eventRect = new Rect(position.x, y, position.width, eventHeight);
                EditorGUI.PropertyField(eventRect, eventRefProp, includeChildren: true);

                EditorGUI.indentLevel--;
            }
        }
    }
}