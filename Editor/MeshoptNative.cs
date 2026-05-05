using System;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace Jp.Local.GuidedMeshLod
{
    // P/Invoke wrapper for meshoptimizer (Win64). DLL は
    // Packages/jp.local.guided-meshlod/Plugins/native/meshoptimizer.dll に配置。
    // Pinned commit: c8359675ccc26d8cca38e0d5b2282d5e3372356a (version.txt 参照)
    internal static class MeshoptNative
    {
        private const string Dll = "meshoptimizer";

        // meshoptimizer.h:514 (commit c8359675):
        //   MESHOPTIMIZER_API size_t meshopt_simplifyWithAttributes(
        //       unsigned int* destination, const unsigned int* indices, size_t index_count,
        //       const float* vertex_positions, size_t vertex_count, size_t vertex_positions_stride,
        //       const float* vertex_attributes, size_t vertex_attributes_stride,
        //       const float* attribute_weights, size_t attribute_count,
        //       const unsigned char* vertex_lock,
        //       size_t target_index_count, float target_error, unsigned int options,
        //       float* result_error);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "meshopt_simplifyWithAttributes")]
        private static extern UIntPtr Meshopt_SimplifyWithAttributes(
            IntPtr destination,
            IntPtr indices, UIntPtr indexCount,
            IntPtr vertexPositions, UIntPtr vertexCount, UIntPtr vertexPositionsStride,
            IntPtr vertexAttributes, UIntPtr vertexAttributesStride,
            IntPtr attributeWeights, UIntPtr attributeCount,
            IntPtr vertexLock,
            UIntPtr targetIndexCount, float targetError, uint options,
            out float resultError);

        private static bool s_dllMissingDialogShown;

        // meshopt の simplify を lock 制約付きで呼ぶ。attributes は使わず positions + lock のみ。
        // resultError は meshopt が出した最終誤差（debug 用、ログに出す程度）。
        public static uint[] SimplifyWithLock(
            uint[] indices,
            Vector3[] positions,
            byte[] vertexLock,
            int targetIndexCount,
            float targetError,
            out float resultError)
        {
            if (indices == null) throw new ArgumentNullException(nameof(indices));
            if (positions == null) throw new ArgumentNullException(nameof(positions));
            if (targetIndexCount < 0) throw new ArgumentOutOfRangeException(nameof(targetIndexCount));

            resultError = 0f;
            if (indices.Length == 0)
            {
                return Array.Empty<uint>();
            }

            // destination: 上限は input と同じサイズ。実書き込み数は戻り値で得る。
            var destination = new uint[indices.Length];

            GCHandle hDest = default;
            GCHandle hIdx = default;
            GCHandle hPos = default;
            GCHandle hLock = default;
            try
            {
                hDest = GCHandle.Alloc(destination, GCHandleType.Pinned);
                hIdx = GCHandle.Alloc(indices, GCHandleType.Pinned);
                hPos = GCHandle.Alloc(positions, GCHandleType.Pinned);

                var lockPtr = IntPtr.Zero;
                if (vertexLock != null && vertexLock.Length >= positions.Length)
                {
                    hLock = GCHandle.Alloc(vertexLock, GCHandleType.Pinned);
                    lockPtr = hLock.AddrOfPinnedObject();
                }

                UIntPtr writtenCount;
                try
                {
                    writtenCount = Meshopt_SimplifyWithAttributes(
                        hDest.AddrOfPinnedObject(),
                        hIdx.AddrOfPinnedObject(), (UIntPtr)indices.Length,
                        hPos.AddrOfPinnedObject(), (UIntPtr)positions.Length, (UIntPtr)(sizeof(float) * 3),
                        IntPtr.Zero, UIntPtr.Zero,           // no vertex_attributes
                        IntPtr.Zero, UIntPtr.Zero,           // no attribute_weights
                        lockPtr,
                        (UIntPtr)targetIndexCount, targetError, 0u,
                        out resultError);
                }
                catch (DllNotFoundException)
                {
                    NotifyDllMissing();
                    throw;
                }
                catch (EntryPointNotFoundException)
                {
                    NotifyEntryPointMissing();
                    throw;
                }

                var n = (int)(ulong)writtenCount;
                if (n == destination.Length)
                {
                    return destination;
                }

                var trimmed = new uint[n];
                Array.Copy(destination, trimmed, n);
                return trimmed;
            }
            finally
            {
                if (hDest.IsAllocated) hDest.Free();
                if (hIdx.IsAllocated) hIdx.Free();
                if (hPos.IsAllocated) hPos.Free();
                if (hLock.IsAllocated) hLock.Free();
            }
        }

        private static void NotifyDllMissing()
        {
            if (s_dllMissingDialogShown) return;
            s_dllMissingDialogShown = true;
            EditorUtility.DisplayDialog(
                "Guided Mesh LOD",
                "meshoptimizer.dll が見つかりません。\n\n" +
                "Packages/jp.local.guided-meshlod/Plugins/native/meshoptimizer.dll を確認してください。\n" +
                "ビルド手順は同フォルダ内の version.txt および README を参照。",
                "OK");
        }

        private static void NotifyEntryPointMissing()
        {
            if (s_dllMissingDialogShown) return;
            s_dllMissingDialogShown = true;
            EditorUtility.DisplayDialog(
                "Guided Mesh LOD",
                "meshoptimizer.dll に meshopt_simplifyWithAttributes が見つかりません。\n\n" +
                "古いビルドの可能性があります。\n" +
                "Packages/jp.local.guided-meshlod/Plugins/native/version.txt の commit から再ビルドしてください。",
                "OK");
        }
    }
}
