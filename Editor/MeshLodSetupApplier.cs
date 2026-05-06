using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Jp.Local.GuidedMeshLod
{
    // MeshLodSetupComponent を受け取って LOD asset 生成 ＋ sharedMesh 差し替え ＋ LODGroup 構築までを
    // 一括実行するロジック。CustomEditor からも tests からも呼べるよう public static で公開する。
    //
    // 対象 Renderer は MeshLodSetupComponent.ResolveTargets() で MeshRenderer + SkinnedMeshRenderer
    // を自動収集（Phase 9 設計）。Apply ロジックは両者を統一して扱う。
    public static class MeshLodSetupApplier
    {
        public static void Apply(MeshLodSetupComponent component)
        {
            if (component == null) throw new System.ArgumentNullException(nameof(component));
            if (component.category == null)
            {
                throw new System.InvalidOperationException(
                    "MeshLodSetupComponent.category is not assigned.");
            }
            if (!component.category.IsValid(out var categoryError))
            {
                throw new System.InvalidOperationException($"MeshLodCategory invalid: {categoryError}");
            }

            var targets = component.ResolveTargets();
            if (targets == null || targets.Length == 0)
            {
                throw new System.InvalidOperationException(
                    "MeshLodSetupComponent: no MeshFilter+MeshRenderer or SkinnedMeshRenderer found in self/children.");
            }

            var category = component.category;
            var settings = ResolveSettings(category);
            if (settings == null)
            {
                throw new System.InvalidOperationException(
                    "MeshLodSettings asset not found. Assign settingsOverride on the Category or set Project Default.");
            }

            try
            {
                AssetDatabase.StartAssetEditing();

                var outputDir = ResolveOutputDir(component);
                var renderersByLod = new List<Renderer>[category.lodCount];
                for (var i = 0; i < category.lodCount; i++) renderersByLod[i] = new List<Renderer>();

                foreach (var renderer in targets)
                {
                    var src = MeshLodSetupComponent.GetSharedMesh(renderer);
                    var owner = MeshLodSetupComponent.GetSharedMeshOwner(renderer);
                    if (src == null || owner == null) continue;

                    var dirForThis = string.IsNullOrEmpty(outputDir)
                        ? GetDirectoryOf(src)
                        : outputDir;
                    var path = $"{dirForThis}/{src.name}_MeshLOD_{SanitizeFileName(category.categoryName)}.asset";

                    var built = MeshLodBuilder.BuildAsAsset(
                        src,
                        category.ToBuildParams(),
                        settings,
                        path);

                    Undo.RecordObject(owner, "Apply Guided Mesh LOD");
                    MeshLodSetupComponent.SetSharedMesh(owner, built);

                    for (var lod = 0; lod < category.lodCount; lod++)
                    {
                        renderersByLod[lod].Add(renderer);
                    }
                }

                BuildLODGroup(component, category, renderersByLod);

                Debug.Log(
                    $"[GuidedMeshLod] Setup applied: {component.gameObject.name} / category='{category.categoryName}' / " +
                    $"renderers processed={targets.Length}");
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
            }
        }

        private static MeshLodSettings ResolveSettings(MeshLodCategory category)
        {
            if (category.settingsOverride != null) return category.settingsOverride;
            return MeshLodSettingsProvider.LoadDefault();
        }

        private static string ResolveOutputDir(MeshLodSetupComponent component)
        {
            var dir = component.outputDirectory;
            if (string.IsNullOrEmpty(dir)) return null;
            return dir.Replace('\\', '/').TrimEnd('/');
        }

        private static string GetDirectoryOf(Mesh mesh)
        {
            var path = AssetDatabase.GetAssetPath(mesh);
            if (string.IsNullOrEmpty(path)) return "Assets";
            var dir = Path.GetDirectoryName(path)?.Replace('\\', '/');
            return string.IsNullOrEmpty(dir) ? "Assets" : dir;
        }

        private static void BuildLODGroup(
            MeshLodSetupComponent component,
            MeshLodCategory category,
            List<Renderer>[] renderersByLod)
        {
            var lodGroup = component.GetComponent<LODGroup>();
            if (lodGroup == null)
            {
                lodGroup = Undo.AddComponent<LODGroup>(component.gameObject);
            }
            else if (!component.overwriteExistingLODGroup)
            {
                Debug.LogWarning(
                    $"[GuidedMeshLod] {component.gameObject.name} already has LODGroup, " +
                    "skip overwrite (overwriteExistingLODGroup=false).");
                return;
            }

            var lods = new LOD[category.lodCount];
            for (var i = 0; i < category.lodCount; i++)
            {
                var transition = category.screenRelativeTransitionHeights != null
                                 && i < category.screenRelativeTransitionHeights.Length
                    ? category.screenRelativeTransitionHeights[i]
                    : Mathf.Pow(0.5f, i + 1);
                lods[i] = new LOD(transition, renderersByLod[i].ToArray());
            }

            Undo.RecordObject(lodGroup, "Apply Guided Mesh LOD - LODGroup");
            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrEmpty(value)) return "Default";
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }
            return value.Replace(' ', '_');
        }
    }
}
