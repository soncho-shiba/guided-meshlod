using System;
using UnityEngine;

namespace Jp.Local.GuidedMeshLod
{
    // SkinnedMesh / BlendShape 系のデータを src → dst へ転写するユーティリティ。
    //
    // Phase 8 の前提：meshopt_simplifyWithAttributes は **vertex を削除のみ** で **再構成しない**
    // （subset 選択のみ）。よって src と dst で vertex 集合は一致するため、
    //   - boneWeights[i] / blendShape delta[i] は src の i 番目を dst にそのままコピーできる
    //   - bindposes は mesh-level なのでそのままコピー
    public static class SkinnedMeshTransfer
    {
        public static bool IsSkinned(Mesh mesh)
        {
            if (mesh == null) return false;
            return mesh.bindposes != null && mesh.bindposes.Length > 0
                && mesh.boneWeights != null && mesh.boneWeights.Length == mesh.vertexCount
                && mesh.vertexCount > 0;
        }

        public static bool HasBlendShapes(Mesh mesh)
        {
            return mesh != null && mesh.blendShapeCount > 0;
        }

        // boneWeights ＋ bindposes の転写
        public static void CopySkinning(Mesh src, Mesh dst)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));

            var bindposes = src.bindposes;
            if (bindposes != null && bindposes.Length > 0)
            {
                dst.bindposes = bindposes;
            }

            var boneWeights = src.boneWeights;
            if (boneWeights != null && boneWeights.Length == src.vertexCount && src.vertexCount > 0)
            {
                dst.boneWeights = boneWeights;
            }
        }

        // BlendShape を全 frame ごと転写。vertex 集合が同一前提（meshopt の subset 選択）。
        public static void CopyBlendShapes(Mesh src, Mesh dst)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));

            var n = src.vertexCount;
            if (n <= 0 || src.blendShapeCount <= 0) return;

            var deltaV = new Vector3[n];
            var deltaN = new Vector3[n];
            var deltaT = new Vector3[n];

            for (var s = 0; s < src.blendShapeCount; s++)
            {
                var name = src.GetBlendShapeName(s);
                var frameCount = src.GetBlendShapeFrameCount(s);
                for (var f = 0; f < frameCount; f++)
                {
                    var weight = src.GetBlendShapeFrameWeight(s, f);
                    Array.Clear(deltaV, 0, deltaV.Length);
                    Array.Clear(deltaN, 0, deltaN.Length);
                    Array.Clear(deltaT, 0, deltaT.Length);
                    src.GetBlendShapeFrameVertices(s, f, deltaV, deltaN, deltaT);
                    dst.AddBlendShapeFrame(name, weight, deltaV, deltaN, deltaT);
                }
            }
        }

        // boneWeights を meshopt の attribute 配列として平坦化。
        // 戻り値は vertexCount * 4 floats。weight0..weight3 を順に配置。
        // 用途：meshopt_simplifyWithAttributes に渡して bone 境界を保護する。
        public static float[] BuildBoneWeightAttributes(Mesh mesh)
        {
            if (!IsSkinned(mesh)) return null;
            var bws = mesh.boneWeights;
            var attrs = new float[bws.Length * 4];
            for (var i = 0; i < bws.Length; i++)
            {
                attrs[i * 4 + 0] = bws[i].weight0;
                attrs[i * 4 + 1] = bws[i].weight1;
                attrs[i * 4 + 2] = bws[i].weight2;
                attrs[i * 4 + 3] = bws[i].weight3;
            }
            return attrs;
        }
    }
}
