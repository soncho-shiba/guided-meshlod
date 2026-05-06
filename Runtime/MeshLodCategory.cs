using System;
using UnityEngine;

namespace Jp.Local.GuidedMeshLod
{
    // 1 カテゴリ（Foliage / Architecture / Character / Prop など）の Build / LODGroup 設定束。
    // MeshLodSetupComponent から参照され、Apply 時に BuildParams と LODGroup 閾値を提供する。
    [CreateAssetMenu(menuName = "GuidedMeshLod/Category", fileName = "MeshLodCategory")]
    public sealed class MeshLodCategory : ScriptableObject
    {
        [Tooltip("カテゴリ名（識別用、出力ファイル名にも使用される）")]
        public string categoryName = "Default";

        [Tooltip("使用する MeshLodSettings asset。空の場合は Project Settings の Default を使用")]
        public MeshLodSettings settingsOverride;

        [Header("Build Parameters")]
        [Range(1, 8)] public int lodCount = 4;
        public float[] targetRatios = { 0.5f, 0.25f, 0.125f };
        [Range(0f, 1f)] public float targetError = 0.01f;

        [Header("LODGroup screen-relative transition heights")]
        [Tooltip("LOD0→LOD1 / LOD1→LOD2 / ... / 最後のLOD→cull の閾値。長さ = lodCount。降順を推奨。")]
        public float[] screenRelativeTransitionHeights = { 0.6f, 0.3f, 0.1f, 0.01f };

        [Header("Output asset 命名")]
        [Tooltip("出力ファイル名末尾に付くサフィックス（カテゴリ識別用、空欄可）")]
        public string variantLabelSuffix = "";

        // BuildParams へ変換。baseLabel が指定されたら categoryName と組み合わせる。
        public BuildParams ToBuildParams(string extraLabel = null)
        {
            var label = string.IsNullOrEmpty(extraLabel)
                ? variantLabelSuffix
                : (string.IsNullOrEmpty(variantLabelSuffix) ? extraLabel : $"{variantLabelSuffix}_{extraLabel}");
            return new BuildParams
            {
                lodCount = this.lodCount,
                targetRatios = targetRatios != null ? (float[])targetRatios.Clone() : Array.Empty<float>(),
                targetError = this.targetError,
                variantLabel = string.IsNullOrEmpty(label) ? null : label,
            };
        }

        // 設定値が valid か（lodCount と各配列の長さなどを軽く検証）
        public bool IsValid(out string error)
        {
            error = null;
            if (lodCount < 1 || lodCount > 8)
            {
                error = $"lodCount must be 1..8, got {lodCount}";
                return false;
            }
            if (targetRatios == null || targetRatios.Length != lodCount - 1)
            {
                error = $"targetRatios length must be lodCount-1 = {lodCount - 1}";
                return false;
            }
            if (screenRelativeTransitionHeights == null || screenRelativeTransitionHeights.Length != lodCount)
            {
                error = $"screenRelativeTransitionHeights length must be lodCount = {lodCount}";
                return false;
            }
            return true;
        }
    }
}
