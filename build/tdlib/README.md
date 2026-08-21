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
the modern `LinkPreviewType` variants. This is a narrow compatibility surface
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

## Application port status

The native proof build completes and produces `Telegram.Td.dll` and
`Telegram.Td.winmd`. The application project consumes those files only when
`UseModernTdlib=true`; the stable Extension SDK remains the default when that
property is disabled.

The modern schema is not source-compatible with the 26.8 application surface.
The experimental build explicitly disables VoIP, nearby chats, and chat-folder
editing. Those areas are gated at compile time and emit a diagnostic event when
invoked. Notification registration continues to use
`RegisterDevice(DeviceTokenWindowsPush)` and preserves the existing
`PushReceiverId` session mapping and native background-task entry point.

The current Release ARM application compile is blocked by the remaining broad
schema migration (message constructors, topic-aware search, event-log filters,
and media/settings APIs). No APPX or sideload ZIP is produced until that
compile is clean; the native proof output is therefore the only reproducible
artifact at this stage.
