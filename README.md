# Guided Mesh LOD

> 🌐 [日本語版 README](README.ja.md)

A Unity 6.3+ Editor package that builds artist-tunable Mesh LOD assets on top
of Unity's native Mesh LOD API (`Mesh.SetLods` / `MeshLodRange` /
`RenderParams.forceMeshLod`). Artists paint a *lock channel* (UV or vertex
color) in their DCC tool to mark vertices that must survive simplification;
the package then drives meshoptimizer's `simplifyWithAttributes` to honour
that lock while generating the LOD ladder.

## Status

🚧 **Early development** — the implementation plan is in
`_doc/260503_07_アーティスト修正可能MeshLOD_implementation_spec.md` of the
parent project. Expect API churn until Phase 7 of that plan is complete.

## Requirements

| Item | Value |
|---|---|
| Unity | 6000.3.x (Unity 6.3) or newer |
| Platforms | Windows x64 (Phases 1–7), macOS arm64+x86_64 universal (Phase 8) |
| Render pipeline | URP / HDRP / Built-in (Mesh LOD is SRP-agnostic) |

## Install

This package lives at `Packages/jp.local.guided-meshlod/` inside a Unity
project (embedded package). Either:

- Place the package folder directly under your project's `Packages/`
  directory, or
- Add a `file:` reference to `Packages/manifest.json`:

  ```json
  "jp.local.guided-meshlod": "file:../path/to/jp.local.guided-meshlod"
  ```

## Native plugin

The package ships a pre-built `meshoptimizer.dll` (Windows) at
`Plugins/native/`. To rebuild from the pinned upstream commit, run the
appropriate script in that folder:

- Windows: `pwsh ./build_windows.ps1`
- macOS:   `bash ./build_macos.sh`

See [`Plugins/native/version.txt`](Plugins/native/version.txt) for the pinned
commit hash and exported symbols verified at that commit.

## Licensing

- This package's own source: TBD (will be set before first public release).
- Third-party components: see [`Third Party Notices.md`](Third%20Party%20Notices.md).
- meshoptimizer (MIT): see
  [`Plugins/native/meshoptimizer.LICENSE.txt`](Plugins/native/meshoptimizer.LICENSE.txt).
