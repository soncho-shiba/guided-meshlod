# Build meshoptimizer.dll (x86_64) for Windows at the pinned commit.
# Place the result next to this script so Unity picks it up via
# DllImport("meshoptimizer").
#
# Requirements: git, cmake, Visual Studio 2022 (or compatible MSBuild toolchain).
# Run from inside Plugins/native/. Works on Windows PowerShell 5.1 and pwsh 7+.

$ErrorActionPreference = 'Stop'

$Upstream = 'https://github.com/zeux/meshoptimizer'
$Commit   = 'c8359675ccc26d8cca38e0d5b2282d5e3372356a'

$Here = Split-Path -Parent $MyInvocation.MyCommand.Path
$WorkRoot = Join-Path $env:TEMP ("meshopt-build-{0}" -f ([guid]::NewGuid().ToString('N')))
$null = New-Item -ItemType Directory -Path $WorkRoot

try {
    Write-Host "==> Cloning $Upstream"
    git clone --quiet $Upstream (Join-Path $WorkRoot 'src')

    Push-Location (Join-Path $WorkRoot 'src')
    try {
        Write-Host ("==> Checking out pinned commit {0}" -f $Commit.Substring(0, 12))
        git checkout --quiet $Commit

        Write-Host '==> Configuring (Visual Studio x64, Release)'
        cmake -S . -B build -A x64 -DBUILD_SHARED_LIBS=ON

        Write-Host '==> Building'
        cmake --build build --config Release

        $SrcDll = Join-Path $WorkRoot 'src\build\Release\meshoptimizer.dll'
        if (-not (Test-Path $SrcDll)) {
            throw "Build did not produce $SrcDll"
        }

        $Dest = Join-Path $Here 'meshoptimizer.dll'
        Copy-Item -Force -Path $SrcDll -Destination $Dest
        Write-Host "==> Done: $Dest"
        Write-Host ''
        Write-Host 'Open the Unity project on this machine. Unity will reimport the .dll'
        Write-Host 'and verify the Plugin Importer enables Editor and Standalone Win64.'
    }
    finally {
        Pop-Location
    }
}
finally {
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue -Path $WorkRoot
}
