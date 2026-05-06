using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Jp.Local.GuidedMeshLod.Tests
{
    [TestFixture]
    public sealed class SkinnedMeshTransferTests
    {
        // ---- IsSkinned / HasBlendShapes ----

        [Test]
        public void IsSkinned_returns_false_for_non_skinned_mesh()
        {
            var mesh = MakeGrid(10);
            Assert.IsFalse(SkinnedMeshTransfer.IsSkinned(mesh));
        }

        [Test]
        public void IsSkinned_returns_true_for_skinned_mesh()
        {
            var mesh = MakeSkinnedGrid(10);
            Assert.IsTrue(SkinnedMeshTransfer.IsSkinned(mesh));
        }

        [Test]
        public void HasBlendShapes_returns_true_when_blendshapes_present()
        {
            var mesh = MakeGrid(10);
            Assert.IsFalse(SkinnedMeshTransfer.HasBlendShapes(mesh));

            mesh.AddBlendShapeFrame("Up", 100f,
                new Vector3[mesh.vertexCount], null, null);
            Assert.IsTrue(SkinnedMeshTransfer.HasBlendShapes(mesh));
        }

        // ---- CopySkinning ----

        [Test]
        public void CopySkinning_preserves_boneWeights_and_bindposes()
        {
            var src = MakeSkinnedGrid(8);
            var dst = MakeGrid(8); // 同じ vertex 数

            SkinnedMeshTransfer.CopySkinning(src, dst);

            Assert.AreEqual(src.bindposes.Length, dst.bindposes.Length);
            Assert.AreEqual(src.boneWeights.Length, dst.boneWeights.Length);
            for (var i = 0; i < src.bindposes.Length; i++)
            {
                Assert.AreEqual(src.bindposes[i], dst.bindposes[i]);
            }
            for (var i = 0; i < src.boneWeights.Length; i++)
            {
                Assert.AreEqual(src.boneWeights[i].boneIndex0, dst.boneWeights[i].boneIndex0);
                Assert.AreEqual(src.boneWeights[i].weight0, dst.boneWeights[i].weight0);
            }
        }

        // ---- CopyBlendShapes ----

        [Test]
        public void CopyBlendShapes_preserves_count_and_frame_weights()
        {
            var src = MakeGrid(8);
            var deltaV = new Vector3[src.vertexCount];
            for (var i = 0; i < deltaV.Length; i++) deltaV[i] = new Vector3(0, 0.5f, 0);
            // 同一 shape 内の frame weight は **昇順** で追加する必要がある
            src.AddBlendShapeFrame("Up", 50f, deltaV, null, null);
            src.AddBlendShapeFrame("Up", 100f, deltaV, null, null); // 2 frames
            src.AddBlendShapeFrame("Down", 100f, deltaV, null, null);

            var dst = MakeGrid(8);
            SkinnedMeshTransfer.CopyBlendShapes(src, dst);

            Assert.AreEqual(2, dst.blendShapeCount);
            Assert.AreEqual(2, dst.GetBlendShapeFrameCount(0)); // "Up" has 2 frames
            Assert.AreEqual(1, dst.GetBlendShapeFrameCount(1)); // "Down" has 1 frame
            Assert.AreEqual("Up", dst.GetBlendShapeName(0));
            Assert.AreEqual("Down", dst.GetBlendShapeName(1));
            Assert.AreEqual(50f, dst.GetBlendShapeFrameWeight(0, 0));
            Assert.AreEqual(100f, dst.GetBlendShapeFrameWeight(0, 1));
        }

        [Test]
        public void CopyBlendShapes_preserves_delta_vectors_per_vertex()
        {
            var src = MakeGrid(4);
            var deltaV = new Vector3[src.vertexCount];
            for (var i = 0; i < deltaV.Length; i++) deltaV[i] = new Vector3(i, 0, 0);
            src.AddBlendShapeFrame("Test", 100f, deltaV, null, null);

            var dst = MakeGrid(4);
            SkinnedMeshTransfer.CopyBlendShapes(src, dst);

            var srcRead = new Vector3[src.vertexCount];
            var dstRead = new Vector3[dst.vertexCount];
            src.GetBlendShapeFrameVertices(0, 0, srcRead, null, null);
            dst.GetBlendShapeFrameVertices(0, 0, dstRead, null, null);
            CollectionAssert.AreEqual(srcRead, dstRead);
        }

        // ---- BuildBoneWeightAttributes ----

        [Test]
        public void BuildBoneWeightAttributes_packs_4_floats_per_vertex()
        {
            var mesh = MakeSkinnedGrid(4);
            var attrs = SkinnedMeshTransfer.BuildBoneWeightAttributes(mesh);

            Assert.IsNotNull(attrs);
            Assert.AreEqual(mesh.vertexCount * 4, attrs.Length);

            // 各 vertex の weight が 4 成分に展開されているか
            var bws = mesh.boneWeights;
            for (var i = 0; i < mesh.vertexCount; i++)
            {
                Assert.AreEqual(bws[i].weight0, attrs[i * 4 + 0]);
                Assert.AreEqual(bws[i].weight1, attrs[i * 4 + 1]);
                Assert.AreEqual(bws[i].weight2, attrs[i * 4 + 2]);
                Assert.AreEqual(bws[i].weight3, attrs[i * 4 + 3]);
            }
        }

        [Test]
        public void BuildBoneWeightAttributes_returns_null_for_non_skinned_mesh()
        {
            var mesh = MakeGrid(4);
            Assert.IsNull(SkinnedMeshTransfer.BuildBoneWeightAttributes(mesh));
        }

        // ---- Helpers ----

        private static Mesh MakeGrid(int gridSize)
        {
            var n = gridSize + 1;
            var verts = new Vector3[n * n];
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
                verts[y * n + x] = new Vector3(x, y, 0);

            var indices = new int[gridSize * gridSize * 6];
            var idx = 0;
            for (var y = 0; y < gridSize; y++)
            for (var x = 0; x < gridSize; x++)
            {
                var v00 = y * n + x;
                var v10 = v00 + 1;
                var v01 = v00 + n;
                var v11 = v01 + 1;
                indices[idx++] = v00; indices[idx++] = v01; indices[idx++] = v11;
                indices[idx++] = v00; indices[idx++] = v11; indices[idx++] = v10;
            }

            var mesh = new Mesh
            {
                name = $"Grid_{gridSize}",
                indexFormat = verts.Length <= ushort.MaxValue ? IndexFormat.UInt16 : IndexFormat.UInt32,
            };
            mesh.SetVertices(verts);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, calculateBounds: false);
            mesh.RecalculateBounds();
            return mesh;
        }

        // 上半分（Y > gridSize/2）を bone 0、下半分を bone 1 に割り当てる skinned grid
        private static Mesh MakeSkinnedGrid(int gridSize)
        {
            var mesh = MakeGrid(gridSize);
            var verts = mesh.vertices;
            var bws = new BoneWeight[mesh.vertexCount];
            var halfY = gridSize * 0.5f;
            for (var i = 0; i < verts.Length; i++)
            {
                if (verts[i].y >= halfY)
                {
                    bws[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f };
                }
                else
                {
                    bws[i] = new BoneWeight { boneIndex0 = 1, weight0 = 1f };
                }
            }
            mesh.boneWeights = bws;
            mesh.bindposes = new[] { Matrix4x4.identity, Matrix4x4.identity };
            return mesh;
        }
    }
}
