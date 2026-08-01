using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using UnityEditorInternal;

namespace ThanhDV.AudioConductor.FMOD
{
    public static class EditorHelper
    {
        private static readonly Dictionary<string, ReorderableList> _reorderableListCache = new();

        public static void CreateHeader(string title, string subtitle)
        {
            // Header
            GUIStyle titleStyle = new(EditorStyles.boldLabel)
            {
                fontSize = 30,
                alignment = TextAnchor.MiddleCenter
            };

            GUIStyle subtitleStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter
            };

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(title, titleStyle, GUILayout.Height(50));
            EditorGUILayout.LabelField(subtitle, subtitleStyle);
            DrawHorizontalLine();
        }

        public static void DrawHorizontalLine(int thickness = 1, int padding = 10, Color? color = null)
        {
            Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(padding + thickness));
            r.height = thickness;
            r.y += padding / 2f;
            r.x -= 2;
            r.width += 6;
            EditorGUI.DrawRect(r, color ?? new Color(0.5f, 0.5f, 0.5f, 1));
        }

        public static void DrawListWithoutHeader(SerializedProperty listProp, string countLabel)
        {
            if (listProp == null) return;

            string key = $"{listProp.serializedObject.targetObject.GetInstanceID()}-{listProp.propertyPath}";

            if (!_reorderableListCache.TryGetValue(key, out var list) || list.serializedProperty != listProp)
            {
                list = new ReorderableList(listProp.serializedObject, listProp, true, true, true, true)
                {
                    drawHeaderCallback = rect => EditorGUI.LabelField(rect, countLabel),
                    drawElementCallback = (rect, index, isActive, isFocused) =>
                    {
                        if (index < 0 || index >= listProp.arraySize) return;
                        SerializedProperty element = listProp.GetArrayElementAtIndex(index);
                        rect.y += 2;
                        EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, EditorGUI.GetPropertyHeight(element, true)), element, GUIContent.none, true);
                    },
                    elementHeightCallback = index =>
                    {
                        if (index < 0 || index >= listProp.arraySize) return 0;
                        return EditorGUI.GetPropertyHeight(listProp.GetArrayElementAtIndex(index), true) + 4;
                    }
                };

                _reorderableListCache[key] = list;
            }

            list.DoLayoutList();
        }

        public static void EnsureFolderPath(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath)) return;

            string[] parts = folderPath.Split('/');
            if (parts.Length == 0) return;

            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}