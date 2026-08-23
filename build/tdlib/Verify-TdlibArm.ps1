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

$dumpbin = Get-Command dumpbin.exe -ErrorAction SilentlyContinue
if ($null -ne $dumpbin) {
    $headers = & $dumpbin.Source /headers (Join-Path $OutputRoot "Telegram.Td.dll")
    if ($LASTEXITCODE -ne 0 -or -not ($headers -match "ARM")) {
        throw "Telegram.Td.dll does not report an ARM image."
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
