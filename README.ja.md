# Guided Mesh LOD

> 🌐 [English README](README.md)

Unity 6.3+ 用のエディタパッケージ。Unity 純正の Mesh LOD API
（`Mesh.SetLods` / `MeshLodRange` / `RenderParams.forceMeshLod`）の上に、
**アーティストが手で誘導できる Mesh LOD ビルドパイプライン**を構築する。
アーティストは DCC ツール上で UV / 頂点カラーに lock channel を描き、
保護したい頂点を指定する。本パッケージはその lock を meshoptimizer の
`simplifyWithAttributes` に渡し、保護領域を維持したまま LOD ラダーを
生成する。

## 状態

🚧 **早期開発中** ── 実装計画は親プロジェクトの
`_doc/260503_07_アーティスト修正可能MeshLOD_implementation_spec.md` に
集約。同計画 Phase 7 完了までは API 変更があり得る。

## 必須環境

| 項目 | 値 |
|---|---|
| Unity | 6000.3.x（Unity 6.3）以上 |
| プラットフォーム | Windows x64（Phase 1〜7）、macOS arm64+x86_64 universal（Phase 8 以降） |
| Render Pipeline | URP / HDRP / Built-in どれでも可（Mesh LOD は SRP 非依存） |

## インストール

本パッケージは Unity プロジェクト内の `Packages/jp.local.guided-meshlod/`
に embedded package として配置する想定。以下いずれかの方法で参照：

- パッケージフォルダをプロジェクトの `Packages/` 直下にそのまま置く
- `Packages/manifest.json` に `file:` 参照を追加する：

  ```json
  "jp.local.guided-meshlod": "file:../path/to/jp.local.guided-meshlod"
  ```

## ネイティブプラグイン

`Plugins/native/` にプリビルド済みの `meshoptimizer.dll`（Windows 用）を
同梱している。ピン留めした上流コミットから再ビルドしたい場合は同フォルダ
のスクリプトを実行：

- Windows: `pwsh ./build_windows.ps1`
- macOS:   `bash ./build_macos.sh`

ピン留めコミットハッシュ、および当該コミットでエクスポート確認済みのシンボル
は [`Plugins/native/version.txt`](Plugins/native/version.txt) を参照。

## ライセンス

- 本パッケージ自身のソース：未定（公開リリース前に確定予定）
- 第三者コンポーネント：[`Third Party Notices.ja.md`](Third%20Party%20Notices.ja.md) 参照
- meshoptimizer（MIT）：[`Plugins/native/meshoptimizer.LICENSE.txt`](Plugins/native/meshoptimizer.LICENSE.txt) 参照

> 本ファイルは英語版 [`README.md`](README.md) の参考訳。法的解釈に齟齬が
> 生じた場合は英語版および同梱されたライセンス原文が優先される。
