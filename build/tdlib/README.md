# Experimental TDLib 1.8.66 ARM UWP build

This directory contains reproducible inputs for an experimental C++/CX ARM UWP
build. Upstream source and compiled output stay outside the application
repository.

## Pinned inputs

- TDLib: `022d60202e446ad1287b9fb68e687c8a0760788b` (declares 1.8.66)
- vcpkg: `45f9f39362a4c52e2b1fbe57b7e649db7f3d96d4`
- OpenSSL: 3.5.7, from tag `openssl-3.5.7`
- zlib: 1.3.2, vcpkg port revision 2
- Generator host: Visual Studio 2022 Build Tools
- Compiler/toolset: Visual Studio v141 ARM (`14.16.27023`)
- Windows SDK: 10.0.18362.0
- UWP dependencies: dynamic CRT and dynamic libraries

The OpenSSL overlay is based on the pinned vcpkg port recipe because the vcpkg
registry at the pinned commit does not contain OpenSSL 3.5.7. Its source archive
SHA-512 is pinned in `overlays/openssl/portfile.cmake`. UWP builds disable only
the OpenSSL Windows certificate-store provider, which calls desktop-only
`CertOpenSystemStoreW`; TDLib does not use that provider.

`patches/0001-qualify-cx-field-types.patch` fixes the C++/CX generator to fully
qualify reference types in properties. This avoids a v141 name-hiding error when
an object has both a `Message` property and a later `Array<Message^>` property;
it does not change the projected WinMD API.

`patches/0002-project-legacy-web-page.patch` keeps the legacy `WebPage` projected
name for the modern `LinkPreview` object and exposes typed media accessors over
the direct `LinkPreviewType` variants. This is a narrow compatibility surface
for the existing message renderer; unsupported preview categories still retain
their modern typed `Type` object.

## Build

From a PowerShell prompt:

```powershell
.\build\tdlib\Build-TdlibArm.ps1 -WorkRoot C:\tdlib-build
```

The work root must be outside this repository. The script verifies both Git
commits, installs only the pinned manifest dependencies, generates the C++/CX
API with a native v141 build, configures WindowsStore ARM against SDK 18362,
and builds only `tddotnet`. Use `-VisualStudioPath` if the Build Tools instance
is installed elsewhere.

The script imports explicit v141 developer environments before invoking vcpkg.
This avoids vcpkg selecting another same-version Visual Studio instance that
does not have the ARM compiler installed.

The existing machine-installed `Telegram.Td.UWP` Extension SDK is never changed.
Integration must consume proof-build files from the external work root, leaving
the stable SDK as a reproducible rollback path.

To verify an existing proof output and print stable SHA-256 values:

```powershell
.\build\tdlib\Verify-TdlibArm.ps1 -OutputRoot C:\tdlib-build\build-uwp-arm\RelWithDebInfo
```

## Application port status

The native proof build completes and produces `Telegram.Td.dll` and
`Telegram.Td.winmd`. The application project consumes those files only when
`UseModernTdlib=true`; the stable Extension SDK remains the default unless the
experimental property is explicitly enabled for an ARM build.
`UpdateManifest.ps1` selects the stable package identity/display name for the
default configuration and the isolated experimental identity when that property
is enabled. Modern bundles are ARM-only so they cannot accidentally include
unsupported x64 TDLib payloads. The experimental `uap:VisualElements` label is
unmistakable; the top-level `Properties/DisplayName` remains the localized
`Unigram Mobile` resource required by APPX validation.

The modern schema is not source-compatible with the 26.8 application surface.
The experimental build explicitly disables VoIP, nearby chats, and chat-folder
editing. Those areas are gated at compile time and emit a diagnostic event when
invoked. Notification registration continues to use
`RegisterDevice(DeviceTokenWindowsPush)` and preserves the existing
`PushReceiverId` session mapping and native background-task entry point.
The package manifest explicitly registers `Telegram.Td.Client` against
`Telegram.Td.dll`; the TDLib component PRI is also added as an APPX payload.
This is required for WinRT activation of the external C++/CX proof payload.

The modern Release ARM managed/XAML compile and signed APPX packaging complete
with the pinned WinMD and native payload when the ignored local
`Constants.Secret.cs` and test signing certificate are present. The PFX and
public CER remain local-only; the current test certificate thumbprint is
`B5E7EBF1650558A9D670BD1C5B9F82E24A213434`. The generated APPX and
`.appxupload` files are under `Unigram\Unigram\bin\ARM\Release\Upload` and
`Unigram\Unigram\AppPackages`. Modern chat/message reporting is compile-time
disabled until the server-driven `ReportChatResult` option flow is implemented.
Device installation, fresh login, push, and Live Tile validation remain
outstanding.
