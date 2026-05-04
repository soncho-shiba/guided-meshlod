# Third Party Notices

This package redistributes the following third-party components. The full
license text for each component is reproduced verbatim alongside the binary
it covers.

> 🌐 日本語版は [`Third Party Notices.ja.md`](Third%20Party%20Notices.ja.md) を参照。
> Japanese version is available at the link above (informational only; the
> English text and the linked license files are authoritative).

---

## meshoptimizer

| Field | Value |
|---|---|
| Source | <https://github.com/zeux/meshoptimizer> |
| Pinned commit | `c8359675ccc26d8cca38e0d5b2282d5e3372356a` (2026-04-29) |
| Author | Arseny Kapoulkine |
| License | MIT |
| License text | [`Plugins/native/meshoptimizer.LICENSE.txt`](Plugins/native/meshoptimizer.LICENSE.txt) |
| Form distributed | Pre-built binary — `meshoptimizer.dll` (Windows x64), `meshoptimizer.dylib` (macOS arm64+x86_64 universal, added in Phase 8) |
| Build instructions | See [`Plugins/native/version.txt`](Plugins/native/version.txt) and the `build_macos.sh` / `build_windows.ps1` scripts in the same folder |

The binary is compiled from the upstream source at the pinned commit using
the build scripts in `Plugins/native/`. To verify reproducibility, clone the
upstream repository at the pinned commit and re-run the appropriate script.

This package's own source code (everything outside `Plugins/native/`) is
not derivative work of meshoptimizer; it interacts with the library through
its public C API via P/Invoke.
