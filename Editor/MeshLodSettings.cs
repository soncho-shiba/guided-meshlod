using UnityEngine;

namespace Jp.Local.GuidedMeshLod
{
    [CreateAssetMenu(menuName = "GuidedMeshLod/Settings", fileName = "MeshLodSettings")]
    public sealed class MeshLodSettings : ScriptableObject
    {
        public enum LockSourceKind { None, Color, UV }
        public enum ColorComponent { R, G, B, A }
        public enum UVComponent { X, Y }

        [Header("Lock input")]
        public LockSourceKind lockSource = LockSourceKind.UV;
        public ColorComponent colorComponent = ColorComponent.A;
        [Range(0, 7)] public int uvChannel = 2;
        public UVComponent uvComponent = UVComponent.X;
        [Range(0f, 1f)] public float lockThreshold = 0.5f;

        [Header("Output strip")]
        public bool stripLockSourceFromOutput = true;
        public bool stripColor = false;
        public bool[] stripUV = new bool[8] { false, false, false, true, true, true, true, true };

        [Header("LOD generation defaults")]
        [Range(1, 8)] public int defaultLodCount = 4;
        public float[] defaultTargetRatios = new float[] { 0.5f, 0.25f, 0.125f };
        [Range(0f, 1f)] public float defaultTargetError = 0.01f;

        [Header("Output asset")]
        // 最終アセットで Read/Write を無効化（VRAM/CPU メモリ節約）。
        // true の場合、生成後の Mesh に対する CPU 側 API は使用不可になる（仕様 §6.1）。
        public bool markFinalAssetNoLongerReadable = true;
    }
}
