[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$OutputRoot
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$requiredFiles = @(
    "Telegram.Td.dll",
    "Telegram.Td.winmd",
    "Telegram.Td.pri",
    "libcrypto-3-arm.dll",
    "libssl-3-arm.dll",
    "z.dll"
)

foreach ($name in $requiredFiles) {
    $path = Join-Path $OutputRoot $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required ARM TDLib output is missing: $path"
    }
}

function Test-ArmPe {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 0x40 -or $bytes[0] -ne 0x4d -or $bytes[1] -ne 0x5a) {
        return $false
    }

    $peOffset = [System.BitConverter]::ToInt32($bytes, 0x3c)
    if ($peOffset -lt 0 -or $peOffset + 6 -gt $bytes.Length) {
        return $false
    }

    $signature = [System.BitConverter]::ToUInt32($bytes, $peOffset)
    $machine = [System.BitConverter]::ToUInt16($bytes, $peOffset + 4)
    return $signature -eq 0x00004550 -and $machine -eq 0x01c4
}

foreach ($name in @("Telegram.Td.dll", "libcrypto-3-arm.dll", "libssl-3-arm.dll", "z.dll")) {
    $path = Join-Path $OutputRoot $name
    if (-not (Test-ArmPe -Path $path)) {
        throw "$name does not contain an ARM PE image."
    }
}

Get-ChildItem -LiteralPath $OutputRoot -File |
    Where-Object { $_.Name -in $requiredFiles } |
    Get-FileHash -Algorithm SHA256 |
    ForEach-Object {
        [PSCustomObject]@{
            File = $_.Path
            Sha256 = $_.Hash
        }
    }
