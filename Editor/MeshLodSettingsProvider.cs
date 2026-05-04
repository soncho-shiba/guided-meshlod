using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Jp.Local.GuidedMeshLod
{
    public static class MeshLodSettingsProvider
    {
        private const string DefaultSettingsGuidKey = "GuidedMeshLod.DefaultSettingsGuid";

        public static MeshLodSettings LoadDefault()
        {
            var guid = EditorPrefs.GetString(DefaultSettingsGuidKey, string.Empty);
            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<MeshLodSettings>(path);
        }

        public static void SaveDefault(MeshLodSettings settings)
        {
            if (settings == null)
            {
                EditorPrefs.DeleteKey(DefaultSettingsGuidKey);
                return;
            }

            var path = AssetDatabase.GetAssetPath(settings);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("[GuidedMeshLod] MeshLodSettings は Project 内のアセットである必要があります。");
                return;
            }

            var guid = AssetDatabase.AssetPathToGUID(path);
            EditorPrefs.SetString(DefaultSettingsGuidKey, guid);
        }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Project/Guided Mesh LOD", SettingsScope.Project)
            {
                label = "Guided Mesh LOD",
                guiHandler = _ =>
                {
                    var current = LoadDefault();

                    EditorGUI.BeginChangeCheck();
                    var picked = (MeshLodSettings)EditorGUILayout.ObjectField(
                        new GUIContent("Default Settings",
                            "プロジェクト既定の MeshLodSettings アセット。EditorPrefs に GUID で保持される。"),
                        current,
                        typeof(MeshLodSettings),
                        allowSceneObjects: false);
                    if (EditorGUI.EndChangeCheck())
                    {
                        SaveDefault(picked);
                    }

                    EditorGUILayout.Space();

                    if (picked == null)
                    {
                        EditorGUILayout.HelpBox(
                            "Default Settings asset がありません。\n" +
                            "Assets > Create > GuidedMeshLod > Settings で作成し、上のフィールドに割り当ててください。",
                            MessageType.Info);
                        return;
                    }

                    // 内部に既定 settings の inline inspector を出す
                    EditorGUILayout.LabelField("Default settings asset content", EditorStyles.boldLabel);
                    using (new EditorGUI.IndentLevelScope())
                    {
                        var so = new SerializedObject(picked);
                        so.UpdateIfRequiredOrScript();
                        var iter = so.GetIterator();
                        iter.NextVisible(enterChildren: true); // m_Script
                        while (iter.NextVisible(enterChildren: false))
                        {
                            EditorGUILayout.PropertyField(iter, includeChildren: true);
                        }
                        so.ApplyModifiedProperties();
                    }
                },
                keywords = new HashSet<string>(new[] { "mesh", "lod", "guided", "meshoptimizer", "simplify" }),
            };
        }
    }
}
