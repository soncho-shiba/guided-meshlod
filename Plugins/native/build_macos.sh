#!/usr/bin/env bash
# Build meshoptimizer for macOS (universal: arm64 + x86_64) at the pinned commit.
# Place the resulting meshoptimizer.dylib next to this script so Unity can pick it up
# via DllImport("meshoptimizer").
#
# Requirements: git, cmake, Xcode command line tools (run `xcode-select --install`).
# Run from inside Plugins/native/.

set -euo pipefail

UPSTREAM=https://github.com/zeux/meshoptimizer
COMMIT=c8359675ccc26d8cca38e0d5b2282d5e3372356a
DEPLOYMENT_TARGET=11.0

HERE=$(cd "$(dirname "$0")" && pwd)
WORK=$(mktemp -d -t meshopt-build.XXXXXX)
cleanup() { rm -rf "$WORK"; }
trap cleanup EXIT

echo "==> Cloning $UPSTREAM"
git clone --quiet "$UPSTREAM" "$WORK/src"

cd "$WORK/src"
echo "==> Checking out pinned commit ${COMMIT:0:12}"
git checkout --quiet "$COMMIT"

echo "==> Configuring (universal arm64 + x86_64, deployment target $DEPLOYMENT_TARGET)"
cmake -S . -B build \
    -DBUILD_SHARED_LIBS=ON \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_OSX_ARCHITECTURES="arm64;x86_64" \
    -DCMAKE_OSX_DEPLOYMENT_TARGET=$DEPLOYMENT_TARGET

echo "==> Building"
cmake --build build --config Release -j

# meshoptimizer's CMakeLists builds libmeshoptimizer.dylib. Unity's
# DllImport("meshoptimizer") resolves to meshoptimizer.dylib (no lib prefix)
# on macOS, so we drop the prefix when copying.
SRC_DYLIB="$WORK/src/build/libmeshoptimizer.dylib"
if [ ! -f "$SRC_DYLIB" ]; then
    echo "Error: build did not produce $SRC_DYLIB" >&2
    exit 1
fi

echo "==> Verifying universal binary"
file "$SRC_DYLIB"
lipo -info "$SRC_DYLIB" || true

DEST="$HERE/meshoptimizer.dylib"
cp "$SRC_DYLIB" "$DEST"

# Set install_name to @rpath form. Unity loads plugins via dlopen with an absolute
# path, so install_name is mostly cosmetic, but the @rpath form is the convention
# and avoids surprises if the dylib is later linked into another binary.
install_name_tool -id "@rpath/meshoptimizer.dylib" "$DEST" || true

echo "==> Done: $DEST"
echo
echo "Open the Unity project on this machine. Unity will import the .dylib and"
echo "auto-create its .meta file. Verify the Plugin Importer enables Editor and"
echo "Standalone macOS, then commit both files (.dylib + .dylib.meta)."
