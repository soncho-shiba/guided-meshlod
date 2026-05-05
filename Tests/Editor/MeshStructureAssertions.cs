using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Jp.Local.GuidedMeshLod.Tests
{
    // Phase 4 の退役で消える Phase 0.5 の MeshStructureDumper / MeshStructureDiff から
    // 再利用すべき検証ロジックを移植したテストヘルパー。Phase 6 の I-10 等で使用する。
    //
    // 内容：
    //   - GetLodsList: per-submesh の MeshLodRange[] を List で取得
    //   - AssertSubmeshRelativeLodIndexStart: §12-A 回帰（LOD 0 indexStart=0）
    //   - AssertSubmeshMajorLayout: §4.2 / §12-B 回帰（cross-submesh の indexStart 連続）
    //   - AssertSumLodRangesEqualSubmeshIndexCount: §12-B 回帰（per-submesh の sum）
    //   - Dump: 失敗時のデバッグ出力
    public static class MeshStructureAssertions
    {
        public static List<MeshLodRange> GetLodsList(Mesh mesh, int submesh)
        {
            var list = new List<MeshLodRange>();
            mesh.GetLods(list, submesh);
            return list;
        }

        // §12-A 回帰：各 submesh の LOD 0 が indexStart=0（submesh 相対）であること
        public static void AssertSubmeshRelativeLodIndexStart(Mesh mesh)
        {
            var list = new List<MeshLodRange>();
            for (var i = 0; i < mesh.subMeshCount; i++)
            {
                list.Clear();
                mesh.GetLods(list, i);
                if (list.Count == 0) continue;
                Assert.AreEqual(
                    0u, list[0].indexStart,
                    $"submesh {i} LOD 0 indexStart must be 0 (submesh-relative, §12-A)\n{Dump(mesh)}");
            }
        }

        // §4.2 / §12-B 回帰：submesh-major 連結 IB レイアウト
        public static void AssertSubmeshMajorLayout(Mesh mesh)
        {
            if (mesh.subMeshCount <= 0) return;
            Assert.AreEqual(
                0, mesh.GetSubMesh(0).indexStart,
                $"first submesh must start at index 0\n{Dump(mesh)}");
            for (var i = 1; i < mesh.subMeshCount; i++)
            {
                var prev = mesh.GetSubMesh(i - 1);
                var cur = mesh.GetSubMesh(i);
                Assert.AreEqual(
                    prev.indexStart + prev.indexCount, cur.indexStart,
                    $"submesh {i} must start where submesh {i - 1} ends (submesh-major)\n{Dump(mesh)}");
            }
        }

        // §12-B 回帰：per-submesh の sum(MeshLodRange.indexCount) = SubMeshDescriptor.indexCount
        // 空 list（= maxLod==1 で SetLods が呼ばれていない場合）はスキップ：
        // SubMeshDescriptor.indexCount が暗黙の LOD 0 範囲として機能するため検証不要。
        public static void AssertSumLodRangesEqualSubmeshIndexCount(Mesh mesh)
        {
            var list = new List<MeshLodRange>();
            for (var i = 0; i < mesh.subMeshCount; i++)
            {
                list.Clear();
                mesh.GetLods(list, i);
                if (list.Count == 0) continue;

                uint sum = 0;
                foreach (var r in list) sum += r.indexCount;
                Assert.AreEqual(
                    (uint)mesh.GetSubMesh(i).indexCount, sum,
                    $"submesh {i}: sum(MeshLodRange.indexCount) must equal SubMeshDescriptor.indexCount\n{Dump(mesh)}");
            }
        }

        // 失敗時のデバッグ出力。NUnit の assert メッセージに添付して原因切り分けを早める。
        public static string Dump(Mesh mesh)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== Mesh: {mesh.name} ===");
            sb.AppendLine($"  vertexCount={mesh.vertexCount}, indexFormat={mesh.indexFormat}");
            sb.AppendLine($"  subMeshCount={mesh.subMeshCount}, lodCount={mesh.lodCount}, isReadable={mesh.isReadable}");

            var lods = new List<MeshLodRange>();
            for (var i = 0; i < mesh.subMeshCount; i++)
            {
                var d = mesh.GetSubMesh(i);
                sb.AppendLine($"  [submesh {i}] indexStart={d.indexStart} indexCount={d.indexCount} topology={d.topology}");
                lods.Clear();
                mesh.GetLods(lods, i);
                for (var k = 0; k < lods.Count; k++)
                {
                    sb.AppendLine($"    [LOD {k}] indexStart={lods[k].indexStart} indexCount={lods[k].indexCount}");
                }
            }
            return sb.ToString();
        }
    }
}
