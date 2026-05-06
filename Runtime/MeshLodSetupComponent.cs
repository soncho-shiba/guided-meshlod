using System.Collections.Generic;
using UnityEngine;

namespace Jp.Local.GuidedMeshLod
{
    // Prefab セットアップ用の MonoBehaviour（データのみ）。Apply ロジックは Editor 側
    // （MeshLodSetupApplier）に置く。Runtime でも型は存在するため Prefab 参照が壊れない。
    //
    // 使い方：
    //   1. Prefab Root に AddComponent
    //   2. category を割り当てる（targetMeshFilters の手動指定は不要）
    //   3. Inspector の Apply ボタンで LOD 生成 ＋ LODGroup 構築を一発実行
    //
    // 対象 Renderer は **自分自身＋全ての子** から MeshFilter+MeshRenderer ペア と
    // SkinnedMeshRenderer を自動収集する（Phase 9 設計）。
    [DisallowMultipleComponent]
    public sealed class MeshLodSetupComponent : MonoBehaviour
    {
        [Tooltip("適用するカテゴリ（Settings + BuildParams + LODGroup 閾値の束）")]
        public MeshLodCategory category;

        [Tooltip("出力 .asset の保存ディレクトリ（Assets/ 配下）。空の場合は各 source mesh と同じフォルダに保存")]
        public string outputDirectory = "";

        [Tooltip("Apply 時に既存の LODGroup を上書きするか（false の場合 LODGroup が既にあればスキップ）")]
        public bool overwriteExistingLODGroup = true;

        // 対象 Renderer を自動収集して返す。
        //   - 自分自身＋全子孫から MeshFilter+MeshRenderer ペア（sharedMesh 非 null）
        //   - 自分自身＋全子孫から SkinnedMeshRenderer（sharedMesh 非 null）
        // 両方を平坦化した Renderer[] を返す。
        public Renderer[] ResolveTargets()
        {
            var list = new List<Renderer>();

            foreach (var mf in GetComponentsInChildren<MeshFilter>(includeInactive: true))
            {
                if (mf == null || mf.sharedMesh == null) continue;
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr != null) list.Add(mr);
            }

            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true))
            {
                if (smr == null || smr.sharedMesh == null) continue;
                list.Add(smr);
            }

            return list.ToArray();
        }

        // Renderer の種類に応じて sharedMesh を取得（Apply ロジックが Editor 側で使う）。
        // MeshRenderer の場合は同 GameObject の MeshFilter を経由する。
        public static Mesh GetSharedMesh(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer smr) return smr.sharedMesh;
            if (renderer is MeshRenderer mr)
            {
                var mf = mr.GetComponent<MeshFilter>();
                return mf != null ? mf.sharedMesh : null;
            }
            return null;
        }

        // sharedMesh を実際に持つ Object を返す（Undo.RecordObject の対象決定に使う）。
        // MeshRenderer の場合は MeshFilter、SkinnedMeshRenderer の場合は smr 自身。
        public static Object GetSharedMeshOwner(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer smr) return smr;
            if (renderer is MeshRenderer mr)
            {
                var mf = mr.GetComponent<MeshFilter>();
                return mf;
            }
            return null;
        }

        // owner（MeshFilter または SkinnedMeshRenderer）の sharedMesh を差し替える。
        public static void SetSharedMesh(Object owner, Mesh mesh)
        {
            if (owner is MeshFilter mf) mf.sharedMesh = mesh;
            else if (owner is SkinnedMeshRenderer smr) smr.sharedMesh = mesh;
        }
    }
}
