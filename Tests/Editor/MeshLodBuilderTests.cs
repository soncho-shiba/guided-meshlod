using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Jp.Local.GuidedMeshLod.Tests
{
    [TestFixture]
    public sealed class MeshLodBuilderTests
    {
        // ---- U-05: LOD0 一致 ＋ MeshLodRange.indexStart の submesh 相対 ----

        [Test]
        public void U05_BuildInMemory_LOD0_indexCount_matches_source()
        {
            var src = MakeMultiSubmesh(triPerSubmesh: 30, submeshCount: 2);
            var settings = MakeSettings();
            var parameters = new BuildParams
            {
                lodCount = 3,
                targetRatios = new[] { 0.5f, 0.25f },
                targetError = 0.01f,
            };

            var dst = MeshLodBuilder.BuildInMemory(src, parameters, settings);

            Assert.AreEqual(src.subMeshCount, dst.subMeshCount);

            var lods = new List<MeshLodRange>();
            for (var i = 0; i < src.subMeshCount; i++)
            {
                lods.Clear();
                dst.GetLods(lods, i);
                Assert.GreaterOrEqual(lods.Count, 1, $"submesh {i} must have at least LOD 0");

                var srcIdx = src.GetIndices(i);
                Assert.AreEqual(
                    (uint)srcIdx.Length, lods[0].indexCount,
                    $"submesh {i} LOD 0 indexCount must equal src.GetIndices(i).Length");
            }
        }

        [Test]
        public void U05_BuildInMemory_MeshLodRange_indexStart_is_submesh_relative()
        {
            // §12-A の回帰テスト：複数 submesh で submesh 1+ の LOD 0 が 0 始まりであること
            var src = MakeMultiSubmesh(triPerSubmesh: 30, submeshCount: 3);
            var settings = MakeSettings();
            var parameters = new BuildParams
            {
                lodCount = 2,
                targetRatios = new[] { 0.5f },
                targetError = 0.01f,
            };

            var dst = MeshLodBuilder.BuildInMemory(src, parameters, settings);

            var lods = new List<MeshLodRange>();
            for (var i = 0; i < dst.subMeshCount; i++)
            {
                lods.Clear();
                dst.GetLods(lods, i);
                Assert.GreaterOrEqual(lods.Count, 1);
                Assert.AreEqual(
                    0u, lods[0].indexStart,
                    $"submesh {i} LOD 0 indexStart must be 0 (submesh-relative, §12-A)");
            }
        }

        [Test]
        public void U05_BuildInMemory_LOD_indices_within_submesh_range()
        {
            // §12-B の回帰テスト：各 submesh の LOD ranges 合計 = SubMeshDescriptor.indexCount
            var src = MakeMultiSubmesh(triPerSubmesh: 30, submeshCount: 2);
            var settings = MakeSettings();
            var parameters = new BuildParams
            {
                lodCount = 3,
                targetRatios = new[] { 0.5f, 0.25f },
                targetError = 0.01f,
            };

            var dst = MeshLodBuilder.BuildInMemory(src, parameters, settings);
            var lods = new List<MeshLodRange>();
            for (var i = 0; i < dst.subMeshCount; i++)
            {
                lods.Clear();
                dst.GetLods(lods, i);
                var sd = dst.GetSubMesh(i);

                uint sumLodIndexCount = 0;
                foreach (var lr in lods)
                {
                    sumLodIndexCount += lr.indexCount;
                }
                Assert.AreEqual(
                    (uint)sd.indexCount, sumLodIndexCount,
                    $"submesh {i}: sum(MeshLodRange.indexCount) must equal SubMeshDescriptor.indexCount");
            }
        }

        [Test]
        public void U05_BuildInMemory_dst_lodCount_matches_max_per_submesh()
        {
            var src = MakeMultiSubmesh(triPerSubmesh: 30, submeshCount: 2);
            var settings = MakeSettings();
            var parameters = new BuildParams
            {
                lodCount = 4,
                targetRatios = new[] { 0.5f, 0.25f, 0.125f },
                targetError = 0.01f,
            };

            var dst = MeshLodBuilder.BuildInMemory(src, parameters, settings);

            var lods = new List<MeshLodRange>();
            var maxLod = 0;
            for (var i = 0; i < dst.subMeshCount; i++)
            {
                lods.Clear();
                dst.GetLods(lods, i);
                if (lods.Count > maxLod) maxLod = lods.Count;
            }
            Assert.AreEqual(maxLod, dst.lodCount,
                "dst.lodCount must equal max(GetLods(submesh).Count)");
        }

        // ---- U-06: meshopt 統合後に LOD k の indices が targetRatios[k-1] に近い ----

        [Test]
        public void U06_BuildInMemory_LOD_indices_near_target_ratio()
        {
            // 30x30 grid (961 verts, 1800 tri = 5400 idx)。十分大きく meshopt が削減できる構造。
            var src = MakeGridMesh(gridSize: 30);
            var settings = MakeSettings();
            var parameters = new BuildParams
            {
                lodCount = 3,
                targetRatios = new[] { 0.5f, 0.25f },
                targetError = 0.5f, // ほぼ無制限の品質許容で target を達成しやすくする
            };

            var dst = MeshLodBuilder.BuildInMemory(src, parameters, settings);

            var srcIdx = src.GetIndices(0);
            var lods = new List<MeshLodRange>();
            dst.GetLods(lods, 0);

            Assert.GreaterOrEqual(lods.Count, 2,
                $"meshopt should produce at least 2 LODs (got {lods.Count})");

            Assert.AreEqual((uint)srcIdx.Length, lods[0].indexCount,
                "LOD 0 must equal source");

            if (lods.Count >= 2)
            {
                var target1 = (uint)(srcIdx.Length * 0.5f);
                AssertWithinRatio(lods[1].indexCount, target1, tolerance: 0.5f,
                    label: "LOD 1");
            }

            if (lods.Count >= 3)
            {
                var target2 = (uint)(srcIdx.Length * 0.25f);
                AssertWithinRatio(lods[2].indexCount, target2, tolerance: 0.6f,
                    label: "LOD 2");
            }
        }

        [Test]
        public void U06_BuildInMemory_LOD_indices_strictly_decreasing()
        {
            var src = MakeGridMesh(gridSize: 20);
            var settings = MakeSettings();
            var parameters = new BuildParams
            {
                lodCount = 4,
                targetRatios = new[] { 0.5f, 0.25f, 0.125f },
                targetError = 0.5f,
            };

            var dst = MeshLodBuilder.BuildInMemory(src, parameters, settings);
            var lods = new List<MeshLodRange>();
            dst.GetLods(lods, 0);

            for (var k = 1; k < lods.Count; k++)
            {
                Assert.Less(lods[k].indexCount, lods[k - 1].indexCount,
                    $"LOD {k} indexCount ({lods[k].indexCount}) must be strictly less than LOD {k - 1} ({lods[k - 1].indexCount})");
            }
        }

        // ---- U-07: BuildParams バリデーション ----

        [Test]
        public void U07_ValidateInput_throws_when_lodCount_out_of_range()
        {
            var src = MakeMultiSubmesh(30, 1);
            var settings = MakeSettings();
            var parameters = new BuildParams { lodCount = 0, targetError = 0f };
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => MeshLodBuilder.BuildInMemory(src, parameters, settings));

            parameters.lodCount = 9;
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => MeshLodBuilder.BuildInMemory(src, parameters, settings));
        }

        [Test]
        public void U07_ValidateInput_throws_when_targetRatios_not_decreasing()
        {
            var src = MakeMultiSubmesh(30, 1);
            var settings = MakeSettings();
            var parameters = new BuildParams
            {
                lodCount = 3,
                targetRatios = new[] { 0.25f, 0.5f }, // 昇順 - NG
                targetError = 0.01f,
            };
            Assert.Throws<System.ArgumentException>(
                () => MeshLodBuilder.BuildInMemory(src, parameters, settings));
        }

        [Test]
        public void U07_ValidateInput_throws_when_mesh_null()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => MeshLodBuilder.BuildInMemory(
                    null,
                    new BuildParams { lodCount = 1, targetError = 0f },
                    MakeSettings()));
        }

        [Test]
        public void U07_ValidateInput_throws_when_targetError_out_of_range()
        {
            var src = MakeMultiSubmesh(30, 1);
            var settings = MakeSettings();

            var parameters = new BuildParams { lodCount = 1, targetError = -0.1f };
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => MeshLodBuilder.BuildInMemory(src, parameters, settings));

            parameters.targetError = 1.5f;
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => MeshLodBuilder.BuildInMemory(src, parameters, settings));
        }

        [Test]
        public void U07_ValidateInput_throws_when_targetRatios_length_mismatch()
        {
            var src = MakeMultiSubmesh(30, 1);
            var settings = MakeSettings();
            var parameters = new BuildParams
            {
                lodCount = 3,
                targetRatios = new[] { 0.5f }, // length should be 2
                targetError = 0.01f,
            };
            Assert.Throws<System.ArgumentException>(
                () => MeshLodBuilder.BuildInMemory(src, parameters, settings));
        }

        [Test]
        public void U07_ValidateInput_throws_when_targetRatios_out_of_open_unit_interval()
        {
            var src = MakeMultiSubmesh(30, 1);
            var settings = MakeSettings();

            // 0 は (0,1) に入らない
            var p1 = new BuildParams
            {
                lodCount = 2, targetRatios = new[] { 0f }, targetError = 0.01f
            };
            Assert.Throws<System.ArgumentException>(
                () => MeshLodBuilder.BuildInMemory(src, p1, settings));

            // 1 は (0,1) に入らない
            var p2 = new BuildParams
            {
                lodCount = 2, targetRatios = new[] { 1f }, targetError = 0.01f
            };
            Assert.Throws<System.ArgumentException>(
                () => MeshLodBuilder.BuildInMemory(src, p2, settings));
        }

        // ---- Helpers ----

        private static MeshLodSettings MakeSettings()
        {
            var s = ScriptableObject.CreateInstance<MeshLodSettings>();
            s.lockSource = MeshLodSettings.LockSourceKind.None; // テスト時は lock を切っておく
            s.stripLockSourceFromOutput = false;
            for (var i = 0; i < s.stripUV.Length; i++) s.stripUV[i] = false;
            return s;
        }

        // 1 submesh = triPerSubmesh 三角形。indices は 0,1,2, 3,4,5, ... と単純に並べる。
        // submeshCount が 2 以上のときは vertex を共有しつつ submesh ごとに別の三角形に分ける。
        private static Mesh MakeMultiSubmesh(int triPerSubmesh, int submeshCount)
        {
            var totalTri = triPerSubmesh * submeshCount;
            var totalVerts = totalTri * 3;
            var verts = new Vector3[totalVerts];
            for (var i = 0; i < totalVerts; i++)
            {
                verts[i] = new Vector3(i, 0, 0);
            }

            var mesh = new Mesh
            {
                name = $"TestMesh_{submeshCount}sub_{triPerSubmesh}tri",
                indexFormat = totalVerts <= ushort.MaxValue ? IndexFormat.UInt16 : IndexFormat.UInt32,
            };
            mesh.SetVertices(verts);
            mesh.subMeshCount = submeshCount;

            for (var s = 0; s < submeshCount; s++)
            {
                var indices = new int[triPerSubmesh * 3];
                for (var t = 0; t < triPerSubmesh; t++)
                {
                    var baseV = (s * triPerSubmesh + t) * 3;
                    indices[t * 3 + 0] = baseV + 0;
                    indices[t * 3 + 1] = baseV + 1;
                    indices[t * 3 + 2] = baseV + 2;
                }
                mesh.SetIndices(indices, MeshTopology.Triangles, s, calculateBounds: false);
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        // gridSize × gridSize の三角形分割平面（XY 平面）。
        // 頂点は (gridSize+1)^2、三角形は gridSize^2 * 2、indices は gridSize^2 * 6 個。
        private static Mesh MakeGridMesh(int gridSize)
        {
            var n = gridSize + 1;
            var verts = new Vector3[n * n];
            for (var y = 0; y < n; y++)
            {
                for (var x = 0; x < n; x++)
                {
                    verts[y * n + x] = new Vector3(x, y, 0);
                }
            }

            var indices = new int[gridSize * gridSize * 6];
            var idx = 0;
            for (var y = 0; y < gridSize; y++)
            {
                for (var x = 0; x < gridSize; x++)
                {
                    var v00 = y * n + x;
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

            var mesh = new Mesh
            {
                name = $"GridMesh_{gridSize}",
                indexFormat = verts.Length <= ushort.MaxValue
                    ? IndexFormat.UInt16
                    : IndexFormat.UInt32,
            };
            mesh.SetVertices(verts);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, calculateBounds: false);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AssertWithinRatio(uint actual, uint target, float tolerance, string label)
        {
            // |actual - target| / target <= tolerance
            var diff = (float)System.Math.Abs((int)actual - (int)target);
            var allowed = target * tolerance;
            Assert.LessOrEqual(diff, allowed,
                $"{label}: actual={actual}, target={target}, diff={diff}, allowed={allowed} (tolerance={tolerance:P0})");
        }
    }
}
