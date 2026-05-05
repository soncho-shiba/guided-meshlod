using UnityEditor;
using UnityEngine;

namespace Jp.Local.GuidedMeshLod
{
    // BuilderWindow から使う簡易プレビュー：
    //   - SpawnPreviewInScene でシーンに MeshFilter+MeshRenderer の GameObject を一つ立てる
    //   - UpdateSpawnedForcedLod で Renderer.forceMeshLod を更新（slider と連動）
    //   - 同じオブジェクトは 1 つだけ維持（毎回置き換え）
    //
    // EditorWindow 内に PreviewRenderUtility を埋め込む案も検討したが、forceMeshLod の挙動を
    // RenderParams 経由ではなく Renderer 側で確実に切り替えたいため、シーンに実体を出す方式を採る。
    public static class MeshLodPreview
    {
        private const string PreviewObjectName = "__GuidedMeshLodPreview";

        public static void SpawnPreviewInScene(Mesh mesh, int forcedLod)
        {
            if (mesh == null) return;

            var existing = GameObject.Find(PreviewObjectName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            var go = new GameObject(PreviewObjectName);
            Undo.RegisterCreatedObjectUndo(go, "Spawn Guided Mesh LOD Preview");

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mats = new Material[Mathf.Max(1, mesh.subMeshCount)];
            for (var i = 0; i < mats.Length; i++)
            {
                var m = new Material(shader)
                {
                    name = $"GuidedMeshLodPreview_Submesh{i}",
                };
                // submesh ごとに少し色を変える（視認性向上）
                m.color = Color.HSVToRGB((i / (float)mats.Length) % 1f, 0.5f, 0.9f);
                mats[i] = m;
            }
            mr.sharedMaterials = mats;
            mr.forceMeshLod = (short)forcedLod;

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        public static void UpdateSpawnedForcedLod(short forcedLod)
        {
            var existing = GameObject.Find(PreviewObjectName);
            if (existing == null) return;
            var mr = existing.GetComponent<MeshRenderer>();
            if (mr != null && mr.forceMeshLod != forcedLod)
            {
                mr.forceMeshLod = forcedLod;
                EditorUtility.SetDirty(mr);
            }
        }
    }
}
