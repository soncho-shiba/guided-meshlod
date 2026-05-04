using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Jp.Local.GuidedMeshLod
{
    public static class ChannelMapping
    {
        // Lock 抽出：選択 channel × component が threshold を超えたら lock = 1。
        // 該当 channel が無い／要素数不一致の場合は全 0（unlocked）で返し、Debug.Log で通知。
        public static byte[] ExtractLockFlags(Mesh src, MeshLodSettings settings)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            var n = src.vertexCount;
            var flags = new byte[n];

            switch (settings.lockSource)
            {
                case MeshLodSettings.LockSourceKind.None:
                    return flags;

                case MeshLodSettings.LockSourceKind.Color:
                {
                    var colors = src.colors;
                    if (colors == null || colors.Length != n)
                    {
                        Debug.Log($"[GuidedMeshLod] Mesh '{src.name}' has no colors or count mismatch " +
                                  $"({(colors == null ? 0 : colors.Length)} vs {n}). All vertices treated as unlocked.");
                        return flags;
                    }
                    for (var i = 0; i < n; i++)
                    {
                        var c = colors[i];
                        var v = settings.colorComponent switch
                        {
                            MeshLodSettings.ColorComponent.R => c.r,
                            MeshLodSettings.ColorComponent.G => c.g,
                            MeshLodSettings.ColorComponent.B => c.b,
                            _ => c.a,
                        };
                        flags[i] = v > settings.lockThreshold ? (byte)1 : (byte)0;
                    }
                    return flags;
                }

                case MeshLodSettings.LockSourceKind.UV:
                {
                    var uvs = new List<Vector4>();
                    src.GetUVs(settings.uvChannel, uvs);
                    if (uvs.Count != n)
                    {
                        Debug.Log($"[GuidedMeshLod] Mesh '{src.name}' UV{settings.uvChannel} not found or count mismatch " +
                                  $"({uvs.Count} vs {n}). All vertices treated as unlocked.");
                        return flags;
                    }
                    for (var i = 0; i < n; i++)
                    {
                        var v = settings.uvComponent == MeshLodSettings.UVComponent.X ? uvs[i].x : uvs[i].y;
                        flags[i] = v > settings.lockThreshold ? (byte)1 : (byte)0;
                    }
                    return flags;
                }
            }

            return flags;
        }

        public static int CountLocked(byte[] lockFlags)
        {
            if (lockFlags == null) return 0;
            var count = 0;
            for (var i = 0; i < lockFlags.Length; i++)
            {
                if (lockFlags[i] != 0) count++;
            }
            return count;
        }

        // 設定に従って vertex buffer を strip コピー。
        // typed setter（SetVertices/SetUVs 等）経由で attribute を都度追加する方式。
        // UV は src 側の dimension（2/3/4）を保ったままコピーする。
        public static void CopyVertexBufferStripped(Mesh src, Mesh dst, MeshLodSettings settings)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            var stripLockSrc = settings.stripLockSourceFromOutput;

            var keepColor = !settings.stripColor &&
                            !(stripLockSrc && settings.lockSource == MeshLodSettings.LockSourceKind.Color);

            var keepUV = new bool[8];
            for (var i = 0; i < 8; i++)
            {
                var stripBySettings = i < settings.stripUV.Length && settings.stripUV[i];
                var stripByLockSource = stripLockSrc &&
                                        settings.lockSource == MeshLodSettings.LockSourceKind.UV &&
                                        settings.uvChannel == i;
                keepUV[i] = !stripBySettings && !stripByLockSource;
            }

            // Position は必須
            dst.SetVertices(src.vertices);

            if (src.HasVertexAttribute(VertexAttribute.Normal))
            {
                var normals = src.normals;
                if (normals != null && normals.Length == src.vertexCount)
                {
                    dst.SetNormals(normals);
                }
            }

            if (src.HasVertexAttribute(VertexAttribute.Tangent))
            {
                var tangents = src.tangents;
                if (tangents != null && tangents.Length == src.vertexCount)
                {
                    dst.SetTangents(tangents);
                }
            }

            if (keepColor && src.HasVertexAttribute(VertexAttribute.Color))
            {
                var colors = src.colors;
                if (colors != null && colors.Length == src.vertexCount)
                {
                    dst.SetColors(colors);
                }
            }

            for (var ch = 0; ch < 8; ch++)
            {
                if (!keepUV[ch]) continue;
                var attr = (VertexAttribute)((int)VertexAttribute.TexCoord0 + ch);
                if (!src.HasVertexAttribute(attr)) continue;

                var dim = src.GetVertexAttributeDimension(attr);
                switch (dim)
                {
                    case 2:
                    {
                        var list = new List<Vector2>();
                        src.GetUVs(ch, list);
                        if (list.Count == src.vertexCount) dst.SetUVs(ch, list);
                        break;
                    }
                    case 3:
                    {
                        var list = new List<Vector3>();
                        src.GetUVs(ch, list);
                        if (list.Count == src.vertexCount) dst.SetUVs(ch, list);
                        break;
                    }
                    default:
                    {
                        var list = new List<Vector4>();
                        src.GetUVs(ch, list);
                        if (list.Count == src.vertexCount) dst.SetUVs(ch, list);
                        break;
                    }
                }
            }
        }
    }
}
