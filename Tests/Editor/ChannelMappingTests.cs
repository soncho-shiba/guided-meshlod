using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Jp.Local.GuidedMeshLod.Tests
{
    [TestFixture]
    public sealed class ChannelMappingTests
    {
        // ---- U-01: ExtractLockFlags の lockSource × component 組み合わせ ----

        [Test]
        public void U01_ExtractLockFlags_UV_X_above_threshold_locks()
        {
            var mesh = MakeMeshWithUv(channel: 2, xs: new[] { 0f, 0.6f, 0.4f, 1f }, ys: new[] { 0f, 0f, 0f, 0f });
            var settings = MakeSettings();
            settings.lockSource = MeshLodSettings.LockSourceKind.UV;
            settings.uvChannel = 2;
            settings.uvComponent = MeshLodSettings.UVComponent.X;
            settings.lockThreshold = 0.5f;

            var flags = ChannelMapping.ExtractLockFlags(mesh, settings);

            Assert.AreEqual(new byte[] { 0, 1, 0, 1 }, flags);
        }

        [Test]
        public void U01_ExtractLockFlags_UV_Y_above_threshold_locks()
        {
            var mesh = MakeMeshWithUv(channel: 2, xs: new[] { 1f, 0f, 1f }, ys: new[] { 0.4f, 0.6f, 1f });
            var settings = MakeSettings();
            settings.lockSource = MeshLodSettings.LockSourceKind.UV;
            settings.uvChannel = 2;
            settings.uvComponent = MeshLodSettings.UVComponent.Y;
            settings.lockThreshold = 0.5f;

            var flags = ChannelMapping.ExtractLockFlags(mesh, settings);

            Assert.AreEqual(new byte[] { 0, 1, 1 }, flags);
        }

        [Test]
        public void U01_ExtractLockFlags_Color_A_above_threshold_locks()
        {
            var mesh = MakeMeshWithColors(new[]
            {
                new Color(0f, 0f, 0f, 0.1f),
                new Color(0f, 0f, 0f, 0.6f),
                new Color(0f, 0f, 0f, 0.5f),  // strict > のため境界値は unlocked
            });
            var settings = MakeSettings();
            settings.lockSource = MeshLodSettings.LockSourceKind.Color;
            settings.colorComponent = MeshLodSettings.ColorComponent.A;
            settings.lockThreshold = 0.5f;

            var flags = ChannelMapping.ExtractLockFlags(mesh, settings);

            Assert.AreEqual(new byte[] { 0, 1, 0 }, flags);
        }

        [Test]
        public void U01_ExtractLockFlags_Color_R_above_threshold_locks()
        {
            var mesh = MakeMeshWithColors(new[]
            {
                new Color(0.9f, 0f, 0f, 0f),
                new Color(0.1f, 0f, 0f, 0f),
            });
            var settings = MakeSettings();
            settings.lockSource = MeshLodSettings.LockSourceKind.Color;
            settings.colorComponent = MeshLodSettings.ColorComponent.R;
            settings.lockThreshold = 0.5f;

            var flags = ChannelMapping.ExtractLockFlags(mesh, settings);

            Assert.AreEqual(new byte[] { 1, 0 }, flags);
        }

        [Test]
        public void U01_ExtractLockFlags_None_returns_all_zero()
        {
            var mesh = MakeMeshWithColors(new[] { Color.white, Color.white });
            var settings = MakeSettings();
            settings.lockSource = MeshLodSettings.LockSourceKind.None;

            var flags = ChannelMapping.ExtractLockFlags(mesh, settings);

            Assert.AreEqual(new byte[] { 0, 0 }, flags);
        }

        // ---- U-02: 対象 channel が無い場合に全 0 ----

        [Test]
        public void U02_ExtractLockFlags_no_target_uv_channel_returns_all_zero()
        {
            var mesh = MakeMeshNoExtraChannels(vertexCount: 5);
            var settings = MakeSettings();
            settings.lockSource = MeshLodSettings.LockSourceKind.UV;
            settings.uvChannel = 2;

            var flags = ChannelMapping.ExtractLockFlags(mesh, settings);

            Assert.AreEqual(5, flags.Length);
            for (var i = 0; i < flags.Length; i++)
            {
                Assert.AreEqual(0, flags[i], $"flags[{i}] should be 0");
            }
        }

        [Test]
        public void U02_ExtractLockFlags_no_colors_returns_all_zero()
        {
            var mesh = MakeMeshNoExtraChannels(vertexCount: 4);
            var settings = MakeSettings();
            settings.lockSource = MeshLodSettings.LockSourceKind.Color;

            var flags = ChannelMapping.ExtractLockFlags(mesh, settings);

            Assert.AreEqual(4, flags.Length);
            foreach (var f in flags)
            {
                Assert.AreEqual(0, f);
            }
        }

        // ---- U-03: strip 設定通りに VertexAttributeDescriptor が反映される ----

        [Test]
        public void U03_StripUV4Plus_excludes_those_channels()
        {
            var src = MakeFullMesh();
            var dst = new Mesh();
            var settings = MakeSettings();
            settings.lockSource = MeshLodSettings.LockSourceKind.None; // lock-source strip を抑止
            settings.stripUV = new[] { false, false, false, false, true, true, true, true };

            ChannelMapping.CopyVertexBufferStripped(src, dst, settings);

            Assert.IsTrue(dst.HasVertexAttribute(VertexAttribute.TexCoord0));
            Assert.IsTrue(dst.HasVertexAttribute(VertexAttribute.TexCoord3));
            // src 側で 5 ch (0..4) しか入れていないので、TexCoord4 が strip で消えていることを検証
            Assert.IsFalse(dst.HasVertexAttribute(VertexAttribute.TexCoord4));
        }

        [Test]
        public void U03_StripLockSource_UV_removes_that_channel()
        {
            var src = MakeFullMesh();
            var dst = new Mesh();
            var settings = MakeSettings();
            settings.stripUV = new bool[8]; // すべて keep
            settings.stripLockSourceFromOutput = true;
            settings.lockSource = MeshLodSettings.LockSourceKind.UV;
            settings.uvChannel = 2;

            ChannelMapping.CopyVertexBufferStripped(src, dst, settings);

            Assert.IsFalse(dst.HasVertexAttribute(VertexAttribute.TexCoord2),
                "lock 用 UV2 は stripLockSourceFromOutput=true のとき除外される");
            Assert.IsTrue(dst.HasVertexAttribute(VertexAttribute.TexCoord3),
                "他の UV は保持される");
        }

        [Test]
        public void U03_StripLockSource_Color_removes_color()
        {
            var src = MakeFullMesh();
            var dst = new Mesh();
            var settings = MakeSettings();
            settings.stripUV = new bool[8];
            settings.stripColor = false;
            settings.stripLockSourceFromOutput = true;
            settings.lockSource = MeshLodSettings.LockSourceKind.Color;

            ChannelMapping.CopyVertexBufferStripped(src, dst, settings);

            Assert.IsFalse(dst.HasVertexAttribute(VertexAttribute.Color));
        }

        // ---- U-04: strip された channel のデータが dst に含まれない ----

        [Test]
        public void U04_StripColor_dst_has_no_colors()
        {
            var src = MakeMeshWithColors(new[] { Color.red, Color.green, Color.blue });
            var dst = new Mesh();
            var settings = MakeSettings();
            settings.lockSource = MeshLodSettings.LockSourceKind.None;
            settings.stripColor = true;

            ChannelMapping.CopyVertexBufferStripped(src, dst, settings);

            Assert.IsFalse(dst.HasVertexAttribute(VertexAttribute.Color));
            var dc = dst.colors;
            Assert.IsTrue(dc == null || dc.Length == 0,
                $"dst.colors should be empty after strip, got length={dc?.Length ?? 0}");
        }

        [Test]
        public void U04_StripUV2_dst_uv2_returns_empty_list()
        {
            var src = MakeFullMesh();
            var dst = new Mesh();
            var settings = MakeSettings();
            settings.lockSource = MeshLodSettings.LockSourceKind.None;
            settings.stripUV = new[] { false, false, true, false, false, false, false, false };

            ChannelMapping.CopyVertexBufferStripped(src, dst, settings);

            var list = new System.Collections.Generic.List<Vector2>();
            dst.GetUVs(2, list);
            Assert.AreEqual(0, list.Count, "stripped UV channel returns empty list");

            // 残されたチャンネルにはデータが入っていることも確認
            list.Clear();
            dst.GetUVs(0, list);
            Assert.AreEqual(src.vertexCount, list.Count, "non-stripped UV channel should retain data");
        }

        // ---- helpers ----

        private static MeshLodSettings MakeSettings()
        {
            return ScriptableObject.CreateInstance<MeshLodSettings>();
        }

        private static Mesh MakeMeshNoExtraChannels(int vertexCount)
        {
            var m = new Mesh { name = "NoExtraChannels" };
            var verts = new Vector3[vertexCount];
            for (var i = 0; i < vertexCount; i++) verts[i] = new Vector3(i, 0, 0);
            m.SetVertices(verts);
            return m;
        }

        private static Mesh MakeMeshWithUv(int channel, float[] xs, float[] ys)
        {
            var m = new Mesh { name = $"MeshWithUv{channel}" };
            var verts = new Vector3[xs.Length];
            m.SetVertices(verts);

            var uvs = new Vector2[xs.Length];
            for (var i = 0; i < xs.Length; i++) uvs[i] = new Vector2(xs[i], ys[i]);
            m.SetUVs(channel, uvs);
            return m;
        }

        private static Mesh MakeMeshWithColors(Color[] colors)
        {
            var m = new Mesh { name = "MeshWithColors" };
            var verts = new Vector3[colors.Length];
            m.SetVertices(verts);
            m.SetColors(colors);
            return m;
        }

        // Position / Normal / Tangent / Color / UV0..UV4
        private static Mesh MakeFullMesh()
        {
            var m = new Mesh { name = "FullMesh" };
            const int n = 3;
            m.SetVertices(new Vector3[n]);
            m.SetNormals(new Vector3[n]);
            m.SetTangents(new Vector4[n]);
            m.SetColors(new Color[n]);
            for (var ch = 0; ch < 5; ch++)
            {
                m.SetUVs(ch, new Vector2[n]);
            }
            return m;
        }
    }
}
