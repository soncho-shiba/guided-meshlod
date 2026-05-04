# 第三者通知

本パッケージは以下の第三者コンポーネントを再配布しています。各コンポーネントの
ライセンス全文は、対象バイナリと同じ場所に原文（英語）のまま収録しています。

> ⚠ 本ファイルは英語版 [`Third Party Notices.md`](Third Party Notices.md)
> の参考訳です。法的に有効なのは英語版およびリンク先のライセンス原文です。

---

## meshoptimizer

| 項目 | 値 |
|---|---|
| 上流リポジトリ | <https://github.com/zeux/meshoptimizer> |
| ピン留めコミット | `c8359675ccc26d8cca38e0d5b2282d5e3372356a`（2026-04-29） |
| 著作者 | Arseny Kapoulkine |
| ライセンス | MIT |
| ライセンス本文 | [`Plugins/native/meshoptimizer.LICENSE.txt`](Plugins/native/meshoptimizer.LICENSE.txt) |
| 配布形態 | プリビルドバイナリ ── `meshoptimizer.dll`（Windows x64）／ `meshoptimizer.dylib`（macOS arm64+x86_64 universal、Phase 8 で追加） |
| ビルド手順 | [`Plugins/native/version.txt`](Plugins/native/version.txt) と同フォルダの `build_macos.sh` / `build_windows.ps1` を参照 |

バイナリは上記ピン留めコミットの上流ソースから、`Plugins/native/` 配下の
ビルドスクリプトを用いて生成されています。再現性を確認したい場合は、
ピン留めコミットで上流リポジトリをクローンし、対応するスクリプトを再実行
してください。

本パッケージ自身のソースコード（`Plugins/native/` 以外）は meshoptimizer
の派生物ではありません。公開 C API を P/Invoke 経由で利用しているのみです。
