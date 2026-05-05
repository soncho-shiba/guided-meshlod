using UnityEditor;
using UnityEngine;

namespace Jp.Local.GuidedMeshLod
{
    public static class MeshLodMenuItems
    {
        // Project ウィンドウで Mesh asset を右クリックしたときの「Build LODs from Mesh」。
        // 選択中の Mesh を初期値にした BuilderWindow を開く。
        [MenuItem("Assets/Guided Mesh LOD/Build LODs from Mesh", true)]
        private static bool ValidateBuildFromMesh()
        {
            return Selection.activeObject is Mesh;
        }

        [MenuItem("Assets/Guided Mesh LOD/Build LODs from Mesh", false, 100)]
        private static void BuildFromMesh()
        {
            var mesh = Selection.activeObject as Mesh;
            if (mesh == null)
            {
                return;
            }
            MeshLodBuilderWindow.OpenForMesh(mesh);
        }
    }
}
