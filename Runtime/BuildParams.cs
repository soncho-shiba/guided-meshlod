using System;

namespace Jp.Local.GuidedMeshLod
{
    [Serializable]
    public struct BuildParams
    {
        public int lodCount;          // 1..8
        public float[] targetRatios;  // length = lodCount - 1, 降順, 0 < r < 1
        public float targetError;     // 0..1
        public string variantLabel;   // null/empty 可

        public static BuildParams FromSettings(MeshLodSettings settings, string variantLabel = null)
        {
            return new BuildParams
            {
                lodCount = settings != null ? settings.defaultLodCount : 4,
                targetRatios = settings != null
                    ? (float[])settings.defaultTargetRatios.Clone()
                    : new[] { 0.5f, 0.25f, 0.125f },
                targetError = settings != null ? settings.defaultTargetError : 0.01f,
                variantLabel = variantLabel,
            };
        }
    }
}
