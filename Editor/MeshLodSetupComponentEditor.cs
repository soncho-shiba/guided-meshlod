using UnityEditor;
using UnityEngine;

namespace Jp.Local.GuidedMeshLod
{
    // MeshLodSetupComponent の Custom Inspector：
    //   - データフィールド表示（標準）
    //   - Apply ボタン → MeshLodSetupApplier.Apply に委譲
    //   - カテゴリ未設定／対象なしのときは HelpBox で通知
    [CustomEditor(typeof(MeshLodSetupComponent))]
    public sealed class MeshLodSetupComponentEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var component = (MeshLodSetupComponent)target;

            EditorGUILayout.Space();

            if (component.category == null)
            {
                EditorGUILayout.HelpBox(
                    "MeshLodCategory を割り当ててください。\n" +
                    "Assets > Create > GuidedMeshLod > Category で新規作成できます。",
                    MessageType.Warning);
                return;
            }

            if (!component.category.IsValid(out var categoryError))
            {
                EditorGUILayout.HelpBox($"Category invalid: {categoryError}", MessageType.Error);
                return;
            }

            var targets = component.ResolveTargets();
            EditorGUILayout.LabelField($"対象 MeshFilter 数: {targets.Length}");
            if (targets.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "対象 MeshFilter が見つかりません。targetMeshFilters に直接指定するか、" +
                    "子オブジェクトに MeshFilter を持つ GameObject を含めてください。",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Apply Guided Mesh LOD Setup", GUILayout.Height(30f)))
            {
                try
                {
                    MeshLodSetupApplier.Apply(component);
                }
                catch (System.Exception e)
                {
                    EditorUtility.DisplayDialog("Guided Mesh LOD", $"Apply failed:\n{e.Message}", "OK");
                    Debug.LogException(e);
                }
            }

            EditorGUILayout.HelpBox(
                "Apply 後はこのコンポーネントを削除しても LOD Mesh asset と LODGroup は残ります。" +
                "再 Apply したい場合は本コンポーネントを残しておくと便利です。",
                MessageType.None);
        }
    }
}
