using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Jp.Local.GuidedMeshLod
{
    public static class MeshLodBuilder
    {
        // メモリ上のみ生成（EditorWindow プレビュー用）。
        // 常に markNoLongerReadable: false で UploadMeshData する。
        public static Mesh BuildInMemory(Mesh source, BuildParams parameters, MeshLodSettings settings)
        {
            ValidateInput(source, parameters);
            var lockFlags = ChannelMapping.ExtractLockFlags(source, settings);
            return BuildCore(source, parameters, settings, lockFlags, markNoLongerReadable: false);
        }

        // BuildAsAsset は Phase 6 で実装。

        // -----------------------------------------------------------------
        // Build core (§6 シーケンスの実装)
        // -----------------------------------------------------------------
        private static Mesh BuildCore(
            Mesh src,
            BuildParams parameters,
            MeshLodSettings settings,
            byte[] lockFlags,
            bool markNoLongerReadable)
        {
            var subMeshCount = src.subMeshCount;
            var positions = src.vertices;

            // Step 3: per-submesh LOD generation (Phase 5: meshopt_simplifyWithAttributes via P/Invoke)
            var lodIndicesPerSubmesh = new uint[subMeshCount][][];
            var actualLodCount = new int[subMeshCount];
            for (var i = 0; i < subMeshCount; i++)
            {
                var baseIndices = ToUInt32(src.GetIndices(i));
                var lods = new List<uint[]> { baseIndices };

                for (var k = 1; k < parameters.lodCount; k++)
                {
                    var prev = lods[k - 1];
                    var targetIndexCount = (int)(baseIndices.Length * parameters.targetRatios[k - 1]);
                    targetIndexCount -= targetIndexCount % 3;
                    if (targetIndexCount < 3)
                    {
                        Debug.Log($"[GuidedMeshLod] submesh {i} stops at LOD {k - 1}: target<3.");
                        break;
                    }

                    var reduced = MeshoptNative.SimplifyWithLock(
                        prev,
                        positions,
                        lockFlags,
                        targetIndexCount,
                        parameters.targetError,
                        out var resultError);

                    if (reduced.Length < 3)
                    {
                        Debug.Log($"[GuidedMeshLod] submesh {i} stops at LOD {k - 1}: " +
                                  $"reduced={reduced.Length} (degenerate)");
                        break;
                    }

                    // 早期収束：前 LOD と差が無い／逆に大きいなら停止
                    if (reduced.Length >= prev.Length)
                    {
                        Debug.Log($"[GuidedMeshLod] submesh {i} stops at LOD {k - 1}: " +
                                  $"reduced={reduced.Length}, prev={prev.Length} (no progress)");
                        break;
                    }

                    // target に大きく逸脱した場合は Info ログ（§7.2）
                    if (reduced.Length > targetIndexCount * 1.5f || reduced.Length < targetIndexCount * 0.5f)
                    {
                        Debug.Log($"[GuidedMeshLod] submesh {i} LOD {k}: meshopt early convergence " +
                                  $"(target={targetIndexCount}, got={reduced.Length}, error={resultError:F4})");
                    }

                    lods.Add(reduced);
                }

                lodIndicesPerSubmesh[i] = lods.ToArray();
                actualLodCount[i] = lods.Count;
            }

            // Step 4: 連結 (submesh-major、§4.2)
            //   global offset = 連結 IB 内の絶対位置（SubMeshDescriptor.indexStart 用）
            //   local offset  = submesh 内の相対位置（MeshLodRange.indexStart 用、§12-A）
            var concat = new List<uint>();
            var submeshIndexStart = new int[subMeshCount];
            var submeshIndexCount = new int[subMeshCount];
            var lodRangeStart = new int[subMeshCount][];
            var lodRangeCount = new int[subMeshCount][];

            for (var i = 0; i < subMeshCount; i++)
            {
                submeshIndexStart[i] = concat.Count;
                var localOffset = 0;
                lodRangeStart[i] = new int[actualLodCount[i]];
                lodRangeCount[i] = new int[actualLodCount[i]];

                for (var k = 0; k < actualLodCount[i]; k++)
                {
                    lodRangeStart[i][k] = localOffset;
                    lodRangeCount[i][k] = lodIndicesPerSubmesh[i][k].Length;
                    concat.AddRange(lodIndicesPerSubmesh[i][k]);
                    localOffset += lodRangeCount[i][k];
                }

                submeshIndexCount[i] = localOffset;
            }

            var totalIndexCount = concat.Count;

            // Step 5: per-submesh MeshLodRange[]（indexStart は submesh 相対）
            var lodRanges = new MeshLodRange[subMeshCount][];
            for (var i = 0; i < subMeshCount; i++)
            {
                lodRanges[i] = new MeshLodRange[actualLodCount[i]];
                for (var k = 0; k < actualLodCount[i]; k++)
                {
                    lodRanges[i][k] = new MeshLodRange(
                        (uint)lodRangeStart[i][k],
                        (uint)lodRangeCount[i][k]);
                }
            }

            // Step 6: SubMeshDescriptor[]（indexStart/Count は submesh 全体＝LOD 全部を覆う）
            var descriptors = new SubMeshDescriptor[subMeshCount];
            for (var i = 0; i < subMeshCount; i++)
            {
                descriptors[i] = new SubMeshDescriptor(
                    submeshIndexStart[i],
                    submeshIndexCount[i],
                    MeshTopology.Triangles);
            }

            // Step 7: Mesh 構築
            var variantSuffix = string.IsNullOrEmpty(parameters.variantLabel)
                ? string.Empty
                : "_" + parameters.variantLabel;
            var dst = new Mesh { name = src.name + "_MeshLOD" + variantSuffix };

            ChannelMapping.CopyVertexBufferStripped(src, dst, settings);

            dst.indexFormat = src.indexFormat;
            dst.SetIndexBufferParams(totalIndexCount, src.indexFormat);
            WriteIndexBuffer(dst, concat, src.indexFormat);

            dst.subMeshCount = subMeshCount;

            // 呼び出し順は §12-C / §12-F：SetSubMesh → lodCount → SetLods
            for (var i = 0; i < subMeshCount; i++)
            {
                dst.SetSubMesh(i, descriptors[i], MeshUpdateFlags.Default);
            }

            var maxLod = 1;
            for (var i = 0; i < subMeshCount; i++)
            {
                if (actualLodCount[i] > maxLod) maxLod = actualLodCount[i];
            }
            dst.lodCount = maxLod;

            for (var i = 0; i < subMeshCount; i++)
            {
                dst.SetLods(lodRanges[i], i, MeshUpdateFlags.Default);
            }

            dst.RecalculateBounds();
            dst.UploadMeshData(markNoLongerReadable);

            return dst;
        }

        // -----------------------------------------------------------------
        // Validation (§7.1)
        // -----------------------------------------------------------------
        private static void ValidateInput(Mesh src, BuildParams parameters)
        {
            if (src == null)
            {
                throw new ArgumentNullException(nameof(src));
            }
            if (src.vertexCount <= 0)
            {
                throw new ArgumentException("Mesh has no vertices.", nameof(src));
            }
            if (!src.isReadable)
            {
                throw new InvalidOperationException(
                    $"Mesh '{src.name}' is not readable. ModelImporter > Read/Write Enabled を ON にしてください。");
            }
            if (src.subMeshCount <= 0)
            {
                throw new ArgumentException("Mesh has no submeshes.", nameof(src));
            }
            for (var i = 0; i < src.subMeshCount; i++)
            {
                var topology = src.GetTopology(i);
                if (topology != MeshTopology.Triangles)
                {
                    throw new ArgumentException(
                        $"Submesh {i} topology is {topology}, expected Triangles.");
                }
            }

            if (parameters.lodCount < 1 || parameters.lodCount > 8)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(parameters.lodCount),
                    $"lodCount must be 1..8, got {parameters.lodCount}.");
            }

            if (parameters.lodCount > 1)
            {
                if (parameters.targetRatios == null ||
                    parameters.targetRatios.Length != parameters.lodCount - 1)
                {
                    throw new ArgumentException(
                        $"targetRatios length must be lodCount - 1 = {parameters.lodCount - 1}, " +
                        $"got {parameters.targetRatios?.Length ?? 0}.");
                }

                var prev = 1f;
                for (var i = 0; i < parameters.targetRatios.Length; i++)
                {
                    var r = parameters.targetRatios[i];
                    if (r <= 0f || r >= 1f || r >= prev)
                    {
                        throw new ArgumentException(
                            "targetRatios must be strictly decreasing in (0, 1), " +
                            $"got [{string.Join(", ", parameters.targetRatios)}].");
                    }
                    prev = r;
                }
            }

            if (parameters.targetError < 0f || parameters.targetError > 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(parameters.targetError),
                    $"targetError must be in [0, 1], got {parameters.targetError}.");
            }
        }

        // -----------------------------------------------------------------
        // Index buffer write helpers
        // -----------------------------------------------------------------
        private static void WriteIndexBuffer(Mesh dst, List<uint> concat, IndexFormat fmt)
        {
            if (fmt == IndexFormat.UInt16)
            {
                var ushortArray = new ushort[concat.Count];
                for (var i = 0; i < concat.Count; i++)
                {
                    ushortArray[i] = (ushort)concat[i];
                }
                dst.SetIndexBufferData(ushortArray, 0, 0, ushortArray.Length);
            }
            else
            {
                var uintArray = concat.ToArray();
                dst.SetIndexBufferData(uintArray, 0, 0, uintArray.Length);
            }
        }

        private static uint[] ToUInt32(int[] src)
        {
            var dst = new uint[src.Length];
            for (var i = 0; i < src.Length; i++)
            {
                dst[i] = (uint)src[i];
            }
            return dst;
        }
    }
}
