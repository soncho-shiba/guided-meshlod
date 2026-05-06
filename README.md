# Guided Mesh LOD

Unity 6.3 純正の Mesh LOD API（`MeshLodRange` / `Mesh.SetLods` / `RenderParams.forceMeshLod`）の上に [meshoptimizer](https://github.com/zeux/meshoptimizer) を組み込み、**アーティストが UV / Color に塗った lock マスク** で simplify を誘導できる Mesh LOD ビルドパイプラインです。

## 必須環境

| 項目 | 値 |
|---|---|
| Unity | 6000.3.x（Unity 6.3）以上 |
| OS | Windows 11 64bit（Editor / Standalone） |
| Render Pipeline | URP / HDRP どちらでも可（Mesh LOD は SRP 非依存） |
| meshoptimizer | pinned commit `c8359675ccc26d8cca38e0d5b2282d5e3372356a`（2026-04-29、`Plugins/native/version.txt` 参照） |

---

## インストール

### Embedded package として導入する場合

Unity プロジェクトの `Packages/` 配下に `jp.local.guided-meshlod/` を配置すれば自動 embed されます（`manifest.json` への追加は不要）。

### 別プロジェクトから参照する場合

`<別プロジェクト>/Packages/manifest.json` の `dependencies` に local path 参照を追加：

```json
{
  "dependencies": {
    "jp.local.guided-meshlod": "file:../path/to/jp.local.guided-meshlod"
  }
}
```

### meshoptimizer.dll の再ビルド

`Plugins/native/version.txt` に手順あり。要約：

```bat
cd path/to/meshoptimizer
git checkout c8359675ccc26d8cca38e0d5b2282d5e3372356a
cmake -S . -B out/build -A x64 -DBUILD_SHARED_LIBS=ON
cmake --build out/build --config Release
copy out/build/Release/meshoptimizer.dll <package>/Plugins/native/
```

DLL に必要な export：`meshopt_simplifyWithAttributes`（このコミット時点で `src/meshoptimizer.h:514` に C 宣言あり）。

---

## クイックスタート

1. **Settings asset を作る**：`Assets > Create > GuidedMeshLod > Settings`
2. **Project Settings に既定登録**：`Edit > Project Settings > Guided Mesh LOD` で 1 の asset を Default に設定
3. **Mesh asset を選んで Build**：
   - Project ウィンドウで Mesh アセットを右クリック → `Guided Mesh LOD > Build LODs from Mesh`
   - もしくは `Tools > Guided Mesh LOD > Builder` でウィンドウを開いて Source slot に drag & drop
4. **Build パラメータを調整**：`LOD Count` / 各 LOD の `target ratio` / `target error` / `Variant Label`
5. **Build → Save Asset** ボタンを押す → 同じフォルダに `{元名}_MeshLOD.asset` が生成される

---

## アーティスト workflow（Maya 例）

LOD 削減で残したい部位（顔のシルエット／ロゴ／装飾）を **UV3.X = 1.0** で塗ることで、meshopt が当該頂点を保護する形で simplify します。

1. Maya でメッシュを開く
2. UV Editor で UV3 を追加
3. 残したい頂点を選び、UV3 の **U（X 軸）を 1** にシフト（残り部位は X = 0）
4. FBX export
5. Unity で import、ModelImporter > **Read/Write Enabled = ON**
6. Project Settings > Guided Mesh LOD の Default Settings が以下になっていることを確認：
   - `lockSource = UV`
   - `uvChannel = 2`（アーティスト呼称 UV3）
   - `uvComponent = X`
   - `lockThreshold = 0.5`
7. Build → 出力 `_MeshLOD.asset` を `Renderer.sharedMesh` に割り当て、`Renderer.forceMeshLod` で各 LOD の見た目を確認

---

## Settings 各項目

| 項目 | 値 / 既定 | 意味 |
|---|---|---|
| `lockSource` | None / Color / **UV** | lock マスクの読み取り元 |
| `colorComponent` | R / G / B / **A** | lockSource=Color のときの component |
| `uvChannel` | 0..7（既定 **2** = UV3） | lockSource=UV のときの UV ch |
| `uvComponent` | **X** / Y | lockSource=UV のときの軸 |
| `lockThreshold` | 0..1（既定 **0.5**） | この閾値より大きい値が lock 対象（**strict greater than**、境界値は unlocked） |
| `stripLockSourceFromOutput` | **true** / false | true なら lock 用 channel を出力 Mesh から除外（推奨） |
| `stripColor` | true / **false** | Color チャンネルを出力から strip するか |
| `stripUV[8]` | UV4..UV7 既定 **true** / UV1..UV3 既定 false | 残したい UV を false に |
| `defaultLodCount` | 1..8（既定 **4**） | 既定 LOD 数 |
| `defaultTargetRatios` | 既定 **[0.5, 0.25, 0.125]** | 各 LOD の target index ratio（lodCount-1 個、降順） |
| `defaultTargetError` | 0..1（既定 **0.01**） | meshopt の誤差許容（小さいほど厳格、純正に近い） |
| `markFinalAssetNoLongerReadable` | **true** / false | 出力 asset で `mesh.isReadable = false`（VRAM / CPU メモリ節約） |

---

## サンプル

`Samples~/Basic/` に Cube / Sphere / MultiMaterial の生成スクリプト。

Package Manager > Guided Mesh LOD > Samples > **Basic** > **Import** → メニュー `Tools > Guided Mesh LOD > Samples > Generate Basic Sample Meshes` で 3 つの Mesh asset を生成できる。詳細は `Samples~/Basic/README.md`。

---

## トラブルシューティング

### `DllNotFoundException: meshoptimizer`

`Packages/jp.local.guided-meshlod/Plugins/native/meshoptimizer.dll` が無いか Plugin Importer 設定が間違っている可能性：

- Inspector で `meshoptimizer.dll` を選択 → `Editor` / `Standalone (Windows x64)` が enabled になっているか確認
- 上記の DLL 再ビルド手順を実行

### `EntryPointNotFoundException: meshopt_simplifyWithAttributes`

古い meshopt commit からビルドした DLL を使っている可能性。`Plugins/native/version.txt` の commit から再ビルド。

### `InvalidOperationException: Mesh '...' is not readable`

ModelImporter の **Read/Write Enabled = ON** にする。

### `InvalidOperationException: Unable to modify LOD0... lodCount > 1`

通常は本パッケージが内部で適切に処理するので発生しないはず。発生した場合は `Mesh.SetLods` 呼び出し前に `Mesh.lodCount` が 2 以上であること、もしくは `BuildParams.lodCount = 1` の場合は SetLods を呼ばないことを確認。

---

## E2E 動作確認手順

1. Maya（または同等 DCC）で Cube を作成、UV3 を追加し半面（例：u > 0）に X = 1 を設定、FBX export
2. Unity に import、Mesh アセットを Project で選択し **Read/Write Enabled = ON**
3. `Tools > Guided Mesh LOD > Builder` で Source slot に drop、`lockSource = UV` で Detected lock vertices が non-zero になることを確認
4. **Build → Save Asset** で `Cube_MeshLOD.asset` を生成
5. Scene に GameObject を作成、生成 asset を `MeshFilter.sharedMesh` に割り当て、Material を設定
6. `MeshRenderer.forceMeshLod` を 0 / 1 / 2 / 3 に切替、**保護領域（X=1 を塗った半面）が低 LOD でも残ること** を目視確認

`Sphere.asset`（Samples > Basic > Generate）で試すと上半球の三角形密度が保たれ、下半球が削減される様子がより明瞭。

---

## 現状の制約・未対応項目

- 対応 OS：**Windows 64bit のみ**（macOS / Linux / Android / iOS は未対応）
- 対応モード：**Editor のみ**（Runtime Player でのアセット生成は不可）
- ソース Mesh は ModelImporter の **Read/Write Enabled = ON** が必須
- 単一 Mesh／単一 Prefab 単位の Build（複数アセットのバッチ処理／CI 自動処理は提供しない）
- `LOD Cross Fade` / `GPU Resident Drawer` の自動セットアップは行わない（Unity 標準の `LODGroup` 設定で利用してください）
- `AssetPostprocessor` での自動再実行は行わない
- 後段の Mesh 圧縮（meshopt の vertex/index encode）は範囲外

---

## ライセンス / クレジット

- **meshoptimizer**: MIT License（[github.com/zeux/meshoptimizer](https://github.com/zeux/meshoptimizer)）
