using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Jp.Local.GuidedMeshLod.Tests
{
    [TestFixture]
    public sealed class IntegrationTests
    {
        private const string TempAssetDir = "Assets/__GuidedMeshLodTests_Temp";

        [SetUp]
        public void EnsureTempDir()
        {
            if (!AssetDatabase.IsValidFolder(TempAssetDir))
            {
                AssetDatabase.CreateFolder("Assets", "__GuidedMeshLodTests_Temp");
            }
        }

        [TearDown]
        public void CleanupTempDir()
        {
            if (AssetDatabase.IsValidFolder(TempAssetDir))
            {
                AssetDatabase.DeleteAsset(TempAssetDir);
            }
        }

        // ---- I-01: Cube は三角形不足で早期停止 ----

        [Test]
        public void I_01_Cube_low_complexity_stops_early()
        {
            var src = GetUnityPrimitive(PrimitiveType.Cube);
            var settings = MakeSettings();
            var parameters = new BuildParams
            {
                lodCount = 4,
                targetRatios = new[] { 0.5f, 0.25f, 0.125f },
                targetError = 0.01f,
            };
            var dst = MeshLodBuilder.BuildInMemory(src, parameters, settings);

            var lods = MeshStructureAssertions.GetLodsList(dst, 0);
            // Cube は 12 tri と単純なので meshopt が早期収束しがち：
            //   - 全 LOD k>=1 で reduced.Length が prev 以上になり break → actualLodCount=1 (lods.Count==0、§12-F の挙動)
            //   - あるいは 1〜数 LOD だけ生成
            Assert.LessOrEqual(lods.Count, 4);

            // LOD 0 size：lods が空（maxLod==1 で SetLods を呼んでいない）なら SubMeshDescriptor で確認
            var lod0Count = lods.Count > 0
                ? (long)lods[0].indexCount
                : (long)dst.GetSubMesh(0).indexCount;
            Assert.AreEqual((long)src.GetIndices(0).Length, lod0Count,
                "LOD 0 indexCount must equal source");
        }

        // ---- I-02: Sphere は複数 LOD 生成 ----

        [Test]
        public void I_02_Sphere_produces_multiple_LODs()
        {
            var src = GetUnityPrimitive(PrimitiveType.Sphere);
            var settings = MakeSettings();
            var parameters = new BuildParams
            {
                lodCount = 4,
                targetRatios = new[] { 0.5f, 0.25f, 0.125f },
                targetError = 0.5f,
            };
            var dst = MeshLodBuilder.BuildInMemory(src, parameters, settings);

            var lods = MeshStructureAssertions.GetLodsList(dst, 0);
            Assert.GreaterOrEqual(lods.Count, 3,
                $"Sphere should produce at least 3 LODs (got {lods.Count})");

            for (var k = 1; k < lods.Count; k++)
            {
                Assert.Less(lods[k].indexCount, lods[k - 1].indexCount,
                    $"LOD {k} indexCount must be < LOD {k - 1}");
            }
        }

        // ---- I-03: Multi-submesh で全 submesh × 全 LOD 生成 ----

        [Test]
        public void I_03_MultiSubmesh_produces_lods_for_each_submesh()
        {
            var src = MakeMultiSubmeshGrid(submeshSizes: new[] { 10, 15 });
            var settings = MakeSettings();
            var parameters = new BuildParams
            {
                lodCount = 3,
                targetRatios = new[] { 0.5f, 0.25f },
                targetError = 0.5f,
            };
            var dst = MeshLodBuilder.BuildInMemory(src, parameters, settings);

            Assert.AreEqual(2, dst.subMeshCount);
            for (var i = 0; i < dst.subMeshCount; i++)
            {
                var lods = MeshStructureAssertions.GetLodsList(dst, i);
                Assert.GreaterOrEqual(lods.Count, 2,
                    $"submesh {i} should have at least 2 LODs (got {lods.Count})");
            }
            MeshStructureAssertions.AssertSubmeshRelativeLodIndexStart(dst);
            MeshStructureAssertions.AssertSubmeshMajorLayout(dst);
        }

        // ---- I-04: UV3.X (uvChannel=2 の X) lock で lock 領域が低 LOD でも残る ----

        [Test]
        public void I_04_UV3_X_lock_preserves_locked_vertices_in_low_LOD()
        {
            const int gridSize = 20;
            var n = gridSize + 1;
            var locked = new[]
            {
                0,
                gridSize,
                gridSize * n,
                gridSize * n + gridSize,
            };
            var src = MakeGridMeshWithLockUV(gridSize, locked);
            var settings = MakeSettings();
            settings.lockSource = MeshLodSettings.LockSourceKind.UV;
            settings.uvChannel = 2;
            settings.uvComponent = MeshLodSettings.UVComponent.X;
            settings.lockThreshold = 0.5f;
            // strip OFF にして dst から UV2 を読みやすくする（UV2 は lock 用）
            settings.stripLockSourceFromOutput = false;

            var parameters = new BuildParams
            {
                lodCount = 3,
                targetRatios = new[] { 0.5f, 0.25f },
                targetError = 0.5f,
            };
            var dst = MeshLodBuilder.BuildInMemory(src, parameters, settings);
            AssertLockedVerticesPresentInLod(dst, locked, lod: 2);
        }

        // ---- I-05: Color.A lock で lock 領域が低 LOD でも残る ----

        [Test]
        public void I_05_ColorA_lock_preserves_locked_vertices_in_low_LOD()
        {
            const int gridSize = 20;
            var n = gridSize + 1;
            var locked = new[]
            {
                0,
                gridSize,
                gridSize * n,
                gridSize * n + gridSize,
            };
            var src = MakeGridMeshWithColorALock(gridSize, locked);
            var settings = MakeSettings();
            settings.lockSource = MeshLodSettings.LockSourceKind.Color;
            settings.colorComponent = MeshLodSettings.ColorComponent.A;
            settings.lockThreshold = 0.5f;
            settings.stripLockSourceFromOutput = false;

            var parameters = new BuildParams
            {
                lodCount = 3,
                targetRatios = new[] { 0.5f, 0.25f },
                targetError = 0.5f,
            };
            var dst = MeshLodBuilder.BuildInMemory(src, parameters, settings);
            AssertLockedVerticesPresentInLod(dst, locked, lod: 2);
        }

        // ---- I-06: strip=true で出力 Mesh の VertexAttributes から指定 channel が消える ----

        [Test]
        public void I_06_stripLockSourceFromOutput_removes_lock_channel()
        {
            const int gridSize = 5;
            var src = MakeGridMeshWithLockUV(gridSize, new[] { 0 });
            // src には Position と TexCoord2 がある
            Assert.IsTrue(src.HasVertexAttribute(VertexAttribute.TexCoord2),
                "source should have TexCoord2 (lock channel)");

            var settings = MakeSettings();
            settings.lockSource = MeshLodSettings.LockSourceKind.UV;
            settings.uvChannel = 2;
            settings.stripLockSourceFromOutput = true;

            var parameters = new BuildParams { lodCount = 1, targetError = 0f };
            var dst = MeshLodBuilder.BuildInMemory(src, parameters, settings);

            Assert.IsFalse(dst.HasVertexAttribute(VertexAttribute.TexCoord2),
                "lock-source UV channel must be stripped from output");
        }

        // ---- I-07: variant ラベルで 2 つの asset を共存生成 ----

        [Test]
        public void I_07_variant_labels_produce_distinct_assets()
        {
            var src = GetUnityPrimitive(PrimitiveType.Cube);
            var settings = MakeSettings();

            var pathAg = $"{TempAssetDir}/Cube_MeshLOD_aggressive.asset";
            var pathCs = $"{TempAssetDir}/Cube_MeshLOD_conservative.asset";

            var dstAg = MeshLodBuilder.BuildAsAsset(
                src,
                new BuildParams { lodCount = 1, targetError = 0f, variantLabel = "aggressive" },
                settings,
                pathAg);
            var dstCs = MeshLodBuilder.BuildAsAsset(
                src,
                new BuildParams { lodCount = 1, targetError = 0f, variantLabel = "conservative" },
                settings,
                pathCs);

            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Mesh>(pathAg),
                "aggressive variant asset must exist");
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Mesh>(pathCs),
                "conservative variant asset must exist");
            Assert.AreNotSame(dstAg, dstCs);
        }

        // ---- I-08: Renderer.forceMeshLod で各 LOD を選択できる ----

        [Test]
        public void I_08_forceMeshLod_can_select_each_LOD_per_renderer()
        {
            var src = GetUnityPrimitive(PrimitiveType.Sphere);
            var settings = MakeSettings();
            var parameters = new BuildParams
            {
                lodCount = 3,
                targetRatios = new[] { 0.5f, 0.25f },
                targetError = 0.5f,
            };
            var dst = MeshLodBuilder.BuildInMemory(src, parameters, settings);

            var lods = MeshStructureAssertions.GetLodsList(dst, 0);
            Assert.GreaterOrEqual(lods.Count, 2);

            var go = new GameObject("__I08_ForceMeshLodTest");
            try
            {
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = dst;
                var mr = go.AddComponent<MeshRenderer>();

                for (var lod = 0; lod < lods.Count; lod++)
                {
                    mr.forceMeshLod = (short)lod;
                    Assert.AreEqual((short)lod, mr.forceMeshLod,
                        $"Renderer.forceMeshLod must accept LOD index {lod}");
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ---- I-09: markFinalAssetNoLongerReadable=true で保存後 isReadable=false ----

        [Test]
        public void I_09_markFinalAssetNoLongerReadable_makes_isReadable_false()
        {
            var src = GetUnityPrimitive(PrimitiveType.Cube);
            var settings = MakeSettings();
            settings.markFinalAssetNoLongerReadable = true;

            var path = $"{TempAssetDir}/I09Cube_MeshLOD.asset";
            var dst = MeshLodBuilder.BuildAsAsset(
                src,
                new BuildParams { lodCount = 1, targetError = 0f },
                settings,
                path);

            Assert.IsFalse(dst.isReadable,
                "markFinalAssetNoLongerReadable=true must produce a non-readable mesh after save");
        }

        // ---- I-10: per-submesh で actualLodCount が独立に決まり MeshLodRange[] も独立 ----

        [Test]
        public void I_10_per_submesh_LOD_count_independence()
        {
            // submesh 0 = 小さい grid（早期停止しがち）／ submesh 1 = 大きい grid（多 LOD）
            var src = MakeMultiSubmeshGrid(submeshSizes: new[] { 4, 20 });
            var settings = MakeSettings();
            var parameters = new BuildParams
            {
                lodCount = 4,
                targetRatios = new[] { 0.5f, 0.25f, 0.125f },
                targetError = 0.5f,
            };
            var dst = MeshLodBuilder.BuildInMemory(src, parameters, settings);

            Assert.AreEqual(2, dst.subMeshCount);
            var lods0 = MeshStructureAssertions.GetLodsList(dst, 0);
            var lods1 = MeshStructureAssertions.GetLodsList(dst, 1);
            Assert.GreaterOrEqual(lods0.Count, 1);
            Assert.GreaterOrEqual(lods1.Count, 1);

            // 構造的回帰：submesh 相対 indexStart / submesh-major / per-submesh sum
            MeshStructureAssertions.AssertSubmeshRelativeLodIndexStart(dst);
            MeshStructureAssertions.AssertSubmeshMajorLayout(dst);
            MeshStructureAssertions.AssertSumLodRangesEqualSubmeshIndexCount(dst);
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private static MeshLodSettings MakeSettings()
        {
            var s = ScriptableObject.CreateInstance<MeshLodSettings>();
            s.lockSource = MeshLodSettings.LockSourceKind.None;
            s.stripLockSourceFromOutput = false;
            for (var i = 0; i < s.stripUV.Length; i++) s.stripUV[i] = false;
            s.stripColor = false;
            s.markFinalAssetNoLongerReadable = false;
            return s;
        }

        private static Mesh GetUnityPrimitive(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(go);
            return mesh;
        }

        // dst の LOD k の indices に locked vertex set がすべて含まれることを assert
        private static void AssertLockedVerticesPresentInLod(Mesh dst, int[] locked, int lod)
        {
            var lods = MeshStructureAssertions.GetLodsList(dst, 0);
            Assert.GreaterOrEqual(lods.Count, lod + 1,
                $"meshopt should produce >= {lod + 1} LODs (got {lods.Count})");

            var lodIndices = dst.GetIndices(submesh: 0, meshLod: lod, applyBaseVertex: true);
            var lodSet = new HashSet<int>(lodIndices);
            foreach (var v in locked)
            {
                Assert.IsTrue(
                    lodSet.Contains(v),
                    $"locked vertex {v} must remain in LOD {lod}. " +
                    $"LOD {lod} has {lodIndices.Length} indices ({lodSet.Count} unique).");
            }
        }

        // 単一 submesh の grid mesh + UV2.X に lock マスク
        private static Mesh MakeGridMeshWithLockUV(int gridSize, int[] lockedVertexIndices)
        {
            var n = gridSize + 1;
            var verts = new Vector3[n * n];
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
                verts[y * n + x] = new Vector3(x, y, 0);

            var indices = BuildGridIndices(gridSize, n, vStart: 0);

            var uv2 = new Vector2[verts.Length];
            var lockSet = new HashSet<int>(lockedVertexIndices);
            for (var i = 0; i < verts.Length; i++)
                uv2[i] = lockSet.Contains(i) ? new Vector2(1f, 0f) : Vector2.zero;

            var mesh = new Mesh
            {
                name = $"GridLockUV_{gridSize}",
                indexFormat = verts.Length <= ushort.MaxValue
                    ? IndexFormat.UInt16
                    : IndexFormat.UInt32,
            };
            mesh.SetVertices(verts);
            mesh.SetUVs(2, uv2);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, calculateBounds: false);
            mesh.RecalculateBounds();
            return mesh;
        }

        // 単一 submesh の grid mesh + Color.A に lock マスク
        private static Mesh MakeGridMeshWithColorALock(int gridSize, int[] lockedVertexIndices)
        {
            var n = gridSize + 1;
            var verts = new Vector3[n * n];
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
                verts[y * n + x] = new Vector3(x, y, 0);

            var indices = BuildGridIndices(gridSize, n, vStart: 0);

            var colors = new Color[verts.Length];
            var lockSet = new HashSet<int>(lockedVertexIndices);
            for (var i = 0; i < verts.Length; i++)
                colors[i] = lockSet.Contains(i) ? new Color(0f, 0f, 0f, 1f) : new Color(0f, 0f, 0f, 0f);

            var mesh = new Mesh
            {
                name = $"GridColorALock_{gridSize}",
                indexFormat = verts.Length <= ushort.MaxValue
                    ? IndexFormat.UInt16
                    : IndexFormat.UInt32,
            };
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, calculateBounds: false);
            mesh.RecalculateBounds();
            return mesh;
        }

        // 各 submesh が独立 grid を持つ multi-submesh mesh
        private static Mesh MakeMultiSubmeshGrid(int[] submeshSizes)
        {
            var submeshCount = submeshSizes.Length;
            var dims = new (int n, int gs, int vCount, int iCount, int vStart)[submeshCount];
            var totalVerts = 0;
            for (var s = 0; s < submeshCount; s++)
            {
                var gs = submeshSizes[s];
                var n = gs + 1;
                var v = n * n;
                var ic = gs * gs * 6;
                dims[s] = (n, gs, v, ic, totalVerts);
                totalVerts += v;
            }

            var verts = new Vector3[totalVerts];
            for (var s = 0; s < submeshCount; s++)
            {
                var dim = dims[s];
                for (var y = 0; y < dim.n; y++)
                for (var x = 0; x < dim.n; x++)
                {
                    verts[dim.vStart + y * dim.n + x] = new Vector3(s * 30 + x, y, 0);
                }
            }

            var mesh = new Mesh
            {
                name = "MultiSubmeshGrid",
                indexFormat = totalVerts <= ushort.MaxValue
                    ? IndexFormat.UInt16
                    : IndexFormat.UInt32,
            };
            mesh.SetVertices(verts);
            mesh.subMeshCount = submeshCount;

            for (var s = 0; s < submeshCount; s++)
            {
                var dim = dims[s];
                var indices = BuildGridIndices(dim.gs, dim.n, dim.vStart);
                mesh.SetIndices(indices, MeshTopology.Triangles, s, calculateBounds: false);
            }
            mesh.RecalculateBounds();
            return mesh;
        }

        private static int[] BuildGridIndices(int gridSize, int n, int vStart)
        {
            var indices = new int[gridSize * gridSize * 6];
            var idx = 0;
            for (var y = 0; y < gridSize; y++)
            {
                for (var x = 0; x < gridSize; x++)
                {
                    var v00 = vStart + y * n + x;
                    var v10 = v00 + 1;
                    var v01 = v00 + n;
                    var v11 = v01 + 1;
                    indices[idx++] = v00;
                    indices[idx++] = v01;
                    indices[idx++] = v11;
                    indices[idx++] = v00;
                    indices[idx++] = v11;
                    indices[idx++] = v10;
                }
            }
            return indices;
        }
    }
}
