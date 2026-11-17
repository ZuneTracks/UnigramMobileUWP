# Packages the built ARM UWP modern calls engine (TgCalls + WebRTC M123 bridge) as a
# prebuilt ZIP, so the app can be built with UseModernTgCalls=true without the
# external WebRTC/TgCalls build. See "Prebuilt bridge package" in README.md.
param(
    [string]$WorkRoot = (Join-Path $env:LOCALAPPDATA 'UnigramTdlibExperiment'),
    [string]$BridgeRoot,
    [string]$WebRtcRoot = 'C:\wrtcar\src',
    [string]$DepotToolsRoot = 'C:\wrtcar\depot_tools',
    [string]$OutputDirectory,
    [string]$PackageName = 'ModernCallsBridge-UWP-ARM'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

if (-not $BridgeRoot) { $BridgeRoot = Join-Path $WorkRoot 'bridge-probe' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $WorkRoot 'artifacts\tgcalls' }
$tgCallsRoot = Join-Path $WorkRoot 'tgcalls'
$zlibRoot = Join-Path $WorkRoot 'vcpkg_installed\arm-uwp-dynamic-v141'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

$binaries = @('ModernCallsBridge.dll', 'Unigram.Native.Calls.Proof.winmd', 'Unigram.Native.Calls.Proof.pri')
foreach ($f in $binaries) {
    if (-not (Test-Path -LiteralPath (Join-Path $BridgeRoot $f))) { throw "Missing bridge output: $f" }
}

# Pins must match Build-ModernCallsBridgeProof.ps1 / Build-WebRtcUwpArm.ps1.
$expectedTgCallsCommit = '1c236c09f8d8569fead14bd68000618a52051225'
$expectedWebRtcCommit = '801b01361857fd9afd40f0efd29034b0e48001c7'
$tgCallsCommit = (& git -C $tgCallsRoot rev-parse HEAD).Trim()
if ($tgCallsCommit -ne $expectedTgCallsCommit) { throw "TgCalls checkout is at $tgCallsCommit; expected $expectedTgCallsCommit." }
$webRtcCommit = (& git -C $WebRtcRoot rev-parse HEAD).Trim()
if ($webRtcCommit -ne $expectedWebRtcCommit) { throw "WebRTC checkout is at $webRtcCommit; expected $expectedWebRtcCommit." }
$zlibVersion = (Select-String -LiteralPath (Join-Path $zlibRoot 'include\zlib.h') -Pattern '^#define ZLIB_VERSION "([^"]+)"' |
    Select-Object -First 1).Matches.Groups[1].Value
$webRtcOut = Join-Path $WebRtcRoot 'out\msvc\uwp\Release\arm'
$sourceCommit = (& git -C $repoRoot rev-parse HEAD).Trim()

$staging = Join-Path $OutputDirectory 'staging'
Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $staging | Out-Null
foreach ($f in $binaries) { Copy-Item -LiteralPath (Join-Path $BridgeRoot $f) $staging }

$licenses = Join-Path $staging 'licenses'
New-Item -ItemType Directory -Force -Path $licenses | Out-Null
Copy-Item -LiteralPath (Join-Path $tgCallsRoot 'LICENSE') (Join-Path $licenses 'tgcalls-LGPL-3.0.txt')
# LGPL-3.0 incorporates the GPL-3.0 text, which is this repository's own licence.
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') (Join-Path $licenses 'GPL-3.0.txt')
Copy-Item -LiteralPath (Join-Path $zlibRoot 'share\zlib\copyright') (Join-Path $licenses 'zlib-LICENSE.txt')
$json11Header = Get-Content -LiteralPath (Join-Path $tgCallsRoot 'tgcalls\third-party\json11.cpp') -TotalCount 20
[IO.File]::WriteAllLines((Join-Path $licenses 'json11-LICENSE.txt'), [string[]]$json11Header)

# WebRTC's own generator lists every third-party library linked into //:webrtc for these build args.
$python = Join-Path $DepotToolsRoot '.cipd_bin\3.11\bin\python3.exe'
if (-not (Test-Path -LiteralPath $python)) { throw "depot_tools Python was not found at '$python'." }
$oldPath = $env:PATH
$env:PATH = "$DepotToolsRoot;$env:PATH"
$env:DEPOT_TOOLS_WIN_TOOLCHAIN = '0'
Push-Location $WebRtcRoot
try {
    & $python tools_webrtc\libs\generate_licenses.py --target //:webrtc $licenses $webRtcOut 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'generate_licenses.py failed.' }
} finally {
    Pop-Location
    $env:PATH = $oldPath
}
Move-Item -LiteralPath (Join-Path $licenses 'LICENSE.md') (Join-Path $licenses 'webrtc-and-dependencies-LICENSE.md')

$patches = Join-Path $staging 'patches'
New-Item -ItemType Directory -Force -Path $patches | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'patches\*.patch') $patches

$utf8 = New-Object System.Text.UTF8Encoding($false)
$hashes = foreach ($f in $binaries) { "$((Get-FileHash -LiteralPath (Join-Path $staging $f) -Algorithm SHA256).Hash)  $f" }
$buildInfo = @(
    "PACKAGE=$PackageName",
    "SOURCE_REPOSITORY=https://github.com/ZuneTracks/UnigramMobileUWP",
    "SOURCE_COMMIT=$sourceCommit",
    "TGCALLS=TelegramMessenger/tgcalls@$tgCallsCommit",
    "WEBRTC=UnigramDev/webrtc-uwp@$webRtcCommit (branch-heads/6312, M123)",
    "WEBRTC_BUILD_SCRIPTS=UnigramDev/deps@6ce0019e1e5ea4e06ab3bc21651242567774a4e0",
    "ZLIB_VERSION=$zlibVersion",
    "TARGET=Windows 10 Mobile / UWP ARM32, MSVC v141 bridge, SDK 10.0.18362",
    "",
    "WebRTC args.gn:"
) + (Get-Content -LiteralPath (Join-Path $webRtcOut 'args.gn') | ForEach-Object { "  $_" }) + @("", "SHA256:") + $hashes
[IO.File]::WriteAllLines((Join-Path $staging 'BUILD-INFO.txt'), [string[]]$buildInfo, $utf8)

$notice = @(
    'Modern calls engine for Unigram Mobile (UWP ARM)',
    '',
    'ModernCallsBridge.dll, Unigram.Native.Calls.Proof.winmd, Unigram.Native.Calls.Proof.pri',
    '  Built by the UnigramMobile project from build/tgcalls (bridge-probe, native-engine).',
    '',
    '  Statically links:',
    "  - TgCalls (TelegramMessenger/tgcalls@$tgCallsCommit)",
    '    GNU LGPL 3.0 - see licenses/tgcalls-LGPL-3.0.txt and licenses/GPL-3.0.txt',
    '    Modified - see patches/tgcalls-*.patch',
    '  - json11 (bundled with TgCalls) - MIT - see licenses/json11-LICENSE.txt',
    "  - WebRTC M123 (UnigramDev/webrtc-uwp@$webRtcCommit) and the third-party",
    '    libraries it links (Abseil, BoringSSL, libyuv, Opus, libsrtp, rnnoise, and others)',
    '    - see licenses/webrtc-and-dependencies-LICENSE.md',
    '    Modified - see patches/webrtc-*.patch',
    "  - zlib $zlibVersion - zlib License - see licenses/zlib-LICENSE.txt",
    '',
    'Corresponding source',
    "  The complete source and build scripts are in https://github.com/ZuneTracks/UnigramMobileUWP at commit",
    "  $sourceCommit (build/tgcalls), together with the pinned upstream revisions above and the",
    '  patches in this package. build/tgcalls/README.md describes how to rebuild and relink the bridge.',
    ''
) -join "`r`n"
[IO.File]::WriteAllText((Join-Path $staging 'THIRD-PARTY-NOTICES.txt'), $notice, $utf8)

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$zipPath = Join-Path $OutputDirectory "$PackageName.zip"
Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
$zip = [IO.Compression.ZipFile]::Open($zipPath, 'Create')
try {
    Get-ChildItem $staging -Recurse -File | ForEach-Object {
        $entry = $_.FullName.Substring($staging.Length + 1).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $entry, 'Optimal') | Out-Null
    }
} finally {
    $zip.Dispose()
}
Remove-Item $staging -Recurse -Force

Write-Host "ZIP:    $zipPath"
Write-Host "SHA256: $((Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash)"
