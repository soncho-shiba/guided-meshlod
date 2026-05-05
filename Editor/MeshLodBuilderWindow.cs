using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Jp.Local.GuidedMeshLod
{
    public sealed class MeshLodBuilderWindow : EditorWindow
    {
        [MenuItem("Tools/Guided Mesh LOD/Builder")]
        public static void OpenWindow()
        {
            GetWindow<MeshLodBuilderWindow>("Guided Mesh LOD");
        }

        public static void OpenForMesh(Mesh mesh)
        {
            var w = GetWindow<MeshLodBuilderWindow>("Guided Mesh LOD");
            w._sourceMesh = mesh;
            w.UpdateOutputPath();
            w.RefreshLockInfo();
            w._previewMesh = null;
            w.Repaint();
        }

        // ----- Serialized state -----
        [SerializeField] private Mesh _sourceMesh;
        [SerializeField] private MeshLodSettings _settingsOverride;
        [SerializeField] private int _lodCount = 4;
        [SerializeField] private float[] _targetRatios = { 0.5f, 0.25f, 0.125f };
        [SerializeField] private float _targetError = 0.01f;
        [SerializeField] private string _variantLabel = string.Empty;
        [SerializeField] private string _outputPath = string.Empty;
        [SerializeField] private int _previewLodIndex;

        // ----- Runtime state -----
        private Mesh _previewMesh;
        private int _detectedLockedCount;
        private int _detectedTotalVertexCount;
        private string _lockExtractionError;
        private Vector2 _scroll;

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawSourceSection();
            EditorGUILayout.Space();
            var settings = ResolveSettings();
            if (settings == null)
            {
                EditorGUILayout.HelpBox(
                    "Settings asset がありません。Project Settings > Guided Mesh LOD で default を設定するか、" +
                    "Settings (override) フィールドに直接割り当ててください。",
                    MessageType.Warning);
                EditorGUILayout.EndScrollView();
                return;
            }

            DrawSettingsSummary(settings);
            EditorGUILayout.Space();
            DrawBuildParameters();
            EditorGUILayout.Space();
            DrawOutputSection();
            EditorGUILayout.Space();
            DrawInfoSection();
            EditorGUILayout.Space();
            DrawBuildButtons(settings);
            EditorGUILayout.Space();
            DrawPreviewSection();

            EditorGUILayout.EndScrollView();
        }

        // ----- UI sections -----

        private void DrawSourceSection()
        {
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            var newSource = (Mesh)EditorGUILayout.ObjectField(
                "Source Mesh", _sourceMesh, typeof(Mesh), allowSceneObjects: false);
            if (EditorGUI.EndChangeCheck())
            {
                _sourceMesh = newSource;
                UpdateOutputPath();
                RefreshLockInfo();
                _previewMesh = null;
            }

            EditorGUI.BeginChangeCheck();
            var newOverride = (MeshLodSettings)EditorGUILayout.ObjectField(
                new GUIContent(
                    "Settings (override)",
                    "空欄なら Project Settings > Guided Mesh LOD の Default を使用。"),
                _settingsOverride, typeof(MeshLodSettings), allowSceneObjects: false);
            if (EditorGUI.EndChangeCheck())
            {
                _settingsOverride = newOverride;
                RefreshLockInfo();
            }
        }

        private void DrawSettingsSummary(MeshLodSettings settings)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.LabelField("Effective settings", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"  lockSource = {settings.lockSource}");
                if (settings.lockSource == MeshLodSettings.LockSourceKind.UV)
                {
                    EditorGUILayout.LabelField($"  uvChannel = {settings.uvChannel}, component = {settings.uvComponent}");
                }
                else if (settings.lockSource == MeshLodSettings.LockSourceKind.Color)
                {
                    EditorGUILayout.LabelField($"  colorComponent = {settings.colorComponent}");
                }
                EditorGUILayout.LabelField($"  lockThreshold = {settings.lockThreshold:F2}");
                EditorGUILayout.LabelField($"  stripLockSource = {settings.stripLockSourceFromOutput}, stripColor = {settings.stripColor}");
                EditorGUILayout.LabelField($"  markNoLongerReadable = {settings.markFinalAssetNoLongerReadable}");
            }
        }

        private void DrawBuildParameters()
        {
            EditorGUILayout.LabelField("Build Parameters", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            var newLodCount = EditorGUILayout.IntSlider("LOD Count", _lodCount, 1, 8);
            if (EditorGUI.EndChangeCheck())
            {
                _lodCount = newLodCount;
                ResizeTargetRatios();
            }

            using (new EditorGUI.IndentLevelScope())
            {
                for (var i = 0; i < _targetRatios.Length; i++)
                {
                    _targetRatios[i] = EditorGUILayout.Slider(
                        $"LOD {i + 1} ratio", _targetRatios[i], 0.001f, 0.999f);
                }
            }

            if (!AreTargetRatiosValid())
            {
                EditorGUILayout.HelpBox(
                    "targetRatios は (0, 1) の開区間で **厳密に降順** である必要があります。",
                    MessageType.Warning);
            }

            _targetError = EditorGUILayout.Slider("Target Error", _targetError, 0f, 1f);
            _variantLabel = EditorGUILayout.TextField(
                new GUIContent("Variant Label", "出力ファイル名末尾に付くサフィックス。空欄可。"),
                _variantLabel ?? string.Empty);
        }

        private void DrawOutputSection()
        {
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _outputPath = EditorGUILayout.TextField("Output Asset Path", _outputPath ?? string.Empty);
                if (GUILayout.Button("...", GUILayout.Width(28f)) && _sourceMesh != null)
                {
                    var defaultDir = GetDefaultOutputDir();
                    var defaultName = GetDefaultOutputFileName();
                    var picked = EditorUtility.SaveFilePanelInProject(
                        "Save Guided Mesh LOD", defaultName, "asset", "保存先", defaultDir);
                    if (!string.IsNullOrEmpty(picked))
                    {
                        _outputPath = picked.Replace('\\', '/');
                    }
                }
            }
        }

        private void DrawInfoSection()
        {
            EditorGUILayout.LabelField("Info", EditorStyles.boldLabel);
            if (_sourceMesh == null)
            {
                EditorGUILayout.LabelField("  (no source)");
                return;
            }
            EditorGUILayout.LabelField($"  vertexCount: {_sourceMesh.vertexCount}");
            EditorGUILayout.LabelField($"  subMeshCount: {_sourceMesh.subMeshCount}");
            EditorGUILayout.LabelField($"  indexFormat: {_sourceMesh.indexFormat}");

            if (!string.IsNullOrEmpty(_lockExtractionError))
            {
                EditorGUILayout.HelpBox($"Lock 抽出エラー: {_lockExtractionError}", MessageType.Error);
            }
            else
            {
                EditorGUILayout.LabelField(
                    $"  Detected lock vertices: {_detectedLockedCount} / {_detectedTotalVertexCount}");
            }
        }

        private void DrawBuildButtons(MeshLodSettings settings)
        {
            using (new EditorGUI.DisabledScope(_sourceMesh == null || !AreTargetRatiosValid()))
            {
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_outputPath)))
                {
                    if (GUILayout.Button("Build → Save Asset", GUILayout.Height(28f)))
                    {
                        TryBuild(settings, asAsset: true);
                    }
                }
                if (GUILayout.Button("Build (Preview / In-Memory)", GUILayout.Height(22f)))
                {
                    TryBuild(settings, asAsset: false);
                }
            }
        }

        private void DrawPreviewSection()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            if (_previewMesh == null)
            {
                EditorGUILayout.HelpBox(
                    "プレビューは Build (Preview / In-Memory) で生成されます。\n" +
                    "アセット保存後（markNoLongerReadable=true 時）も再 Build で in-memory プレビュー可能（§6.1）。",
                    MessageType.Info);
                return;
            }

            var maxLod = Mathf.Max(0, _previewMesh.lodCount - 1);
            _previewLodIndex = EditorGUILayout.IntSlider("Forced LOD", _previewLodIndex, 0, maxLod);

            using (new EditorGUI.IndentLevelScope())
            {
                var lods = new List<MeshLodRange>();
                for (var i = 0; i < _previewMesh.subMeshCount; i++)
                {
                    lods.Clear();
                    _previewMesh.GetLods(lods, i);
                    if (lods.Count == 0) continue;
                    var actual = Mathf.Min(_previewLodIndex, lods.Count - 1);
                    var idx = lods[actual].indexCount;
                    var clampNote = actual < _previewLodIndex ? "  [clamped]" : string.Empty;
                    EditorGUILayout.LabelField(
                        $"submesh {i}: {idx} indices ({idx / 3} tris) at LOD {actual}{clampNote}");
                }
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Spawn Preview GameObject in current Scene"))
            {
                MeshLodPreview.SpawnPreviewInScene(_previewMesh, _previewLodIndex);
            }
            EditorGUILayout.HelpBox(
                "Spawn したオブジェクトの Renderer.forceMeshLod を上のスライダーが変えます\n" +
                "（ScrollWheel/Shift で値の細かい調整は Inspector からでも可能）。",
                MessageType.None);

            // スライダー変更時に Spawn 済みオブジェクトの forceMeshLod を反映
            MeshLodPreview.UpdateSpawnedForcedLod((short)_previewLodIndex);
        }

        // ----- Build / state helpers -----

        private void TryBuild(MeshLodSettings settings, bool asAsset)
        {
            try
            {
                var parameters = new BuildParams
                {
                    lodCount = _lodCount,
                    targetRatios = (float[])_targetRatios.Clone(),
                    targetError = _targetError,
                    variantLabel = string.IsNullOrEmpty(_variantLabel) ? null : _variantLabel,
                };

                if (asAsset)
                {
                    var path = NormalizeOutputPath(_outputPath);
                    var built = MeshLodBuilder.BuildAsAsset(_sourceMesh, parameters, settings, path);
                    Debug.Log(
                        $"[GuidedMeshLod] Built and saved: {path}\n" +
                        $"  subMeshCount={built.subMeshCount}, lodCount={built.lodCount}");
                    EditorGUIUtility.PingObject(built);

                    // markNoLongerReadable=true でアセット保存した場合でもプレビューを継続できるよう、
                    // in-memory mesh を別途生成しておく（§6.1）。
                    _previewMesh = MeshLodBuilder.BuildInMemory(_sourceMesh, parameters, settings);
                }
                else
                {
                    _previewMesh = MeshLodBuilder.BuildInMemory(_sourceMesh, parameters, settings);
                    Debug.Log(
                        $"[GuidedMeshLod] In-memory build OK. " +
                        $"subMeshCount={_previewMesh.subMeshCount}, lodCount={_previewMesh.lodCount}");
                }
                _previewLodIndex = 0;
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Guided Mesh LOD", $"Build failed:\n{e.Message}", "OK");
                Debug.LogException(e);
            }
        }

        private MeshLodSettings ResolveSettings()
        {
            return _settingsOverride != null
                ? _settingsOverride
                : MeshLodSettingsProvider.LoadDefault();
        }

        private void RefreshLockInfo()
        {
            _lockExtractionError = null;
            _detectedLockedCount = 0;
            _detectedTotalVertexCount = 0;
            if (_sourceMesh == null) return;
            _detectedTotalVertexCount = _sourceMesh.vertexCount;
            var settings = ResolveSettings();
            if (settings == null) return;

            try
            {
                var flags = ChannelMapping.ExtractLockFlags(_sourceMesh, settings);
                _detectedLockedCount = ChannelMapping.CountLocked(flags);
            }
            catch (System.Exception e)
            {
                _lockExtractionError = e.Message;
            }
        }

        private void ResizeTargetRatios()
        {
            var n = Mathf.Max(0, _lodCount - 1);
            var ratios = new float[n];
            for (var i = 0; i < n; i++)
            {
                ratios[i] = i < (_targetRatios?.Length ?? 0)
                    ? _targetRatios[i]
                    : Mathf.Pow(0.5f, i + 1);
            }
            _targetRatios = ratios;
        }

        private bool AreTargetRatiosValid()
        {
            if (_lodCount <= 1) return true;
            if (_targetRatios == null || _targetRatios.Length != _lodCount - 1) return false;
            var prev = 1f;
            foreach (var r in _targetRatios)
            {
                if (r <= 0f || r >= 1f || r >= prev) return false;
                prev = r;
            }
            return true;
        }

        private void UpdateOutputPath()
        {
            if (_sourceMesh == null) return;
            _outputPath = $"{GetDefaultOutputDir()}/{GetDefaultOutputFileName()}";
        }

        private string GetDefaultOutputDir()
        {
            if (_sourceMesh == null) return "Assets";
            var srcPath = AssetDatabase.GetAssetPath(_sourceMesh);
            var dir = Path.GetDirectoryName(srcPath)?.Replace('\\', '/');
            return string.IsNullOrEmpty(dir) ? "Assets" : dir;
        }

        private string GetDefaultOutputFileName()
        {
            var name = _sourceMesh != null ? _sourceMesh.name : "MeshLOD";
            var suffix = string.IsNullOrEmpty(_variantLabel) ? string.Empty : "_" + _variantLabel;
            return $"{name}_MeshLOD{suffix}.asset";
        }

        private static string NormalizeOutputPath(string path)
        {
            return path == null ? string.Empty : path.Replace('\\', '/');
        }
    }
}
