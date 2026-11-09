<#
.SYNOPSIS
Generates the non-BETA Metro logo set for the Unigram UWP package.

.DESCRIPTION
Uses only System.Drawing primitives so every PNG can be regenerated without
external artwork tools. Run from any working directory:

    powershell -ExecutionPolicy Bypass -File .\build\Generate-MetroLogoAssets.ps1

The script writes all scale, target-size, altform, tile, Store, splash, and
lock-screen assets declared in Unigram\Unigram\Assets\Logos.
#>

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$logosDirectory = Join-Path $repositoryRoot 'Unigram\Unigram\Assets\Logos'
$metroBlue = [System.Drawing.Color]::FromArgb(255, 0, 120, 215)
$white = [System.Drawing.Color]::White

if (-not (Test-Path -LiteralPath $logosDirectory -PathType Container)) {
    throw "Logo directory not found: $logosDirectory"
}

function New-RoundedRectanglePath {
    param(
        [System.Drawing.RectangleF] $Bounds,
        [single] $Radius
    )

    $diameter = [Math]::Min($Radius * 2.0, [Math]::Min($Bounds.Width, $Bounds.Height))
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($Bounds.Left, $Bounds.Top, $diameter, $diameter, 180, 90)
    $path.AddArc($Bounds.Right - $diameter, $Bounds.Top, $diameter, $diameter, 270, 90)
    $path.AddArc($Bounds.Right - $diameter, $Bounds.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($Bounds.Left, $Bounds.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-Graphics {
    param([System.Drawing.Bitmap] $Bitmap)

    $graphics = [System.Drawing.Graphics]::FromImage($Bitmap)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    return $graphics
}

function Draw-MetroMark {
    param(
        [System.Drawing.Graphics] $Graphics,
        [int] $Width,
        [int] $Height
    )

    $unit = [single][Math]::Min($Width, $Height)
    $offsetX = [single](($Width - $unit) / 2.0)
    $bubble = [System.Drawing.RectangleF]::new(
        $offsetX + $unit * 0.22,
        $unit * 0.21,
        $unit * 0.58,
        $unit * 0.52)
    [System.Drawing.PointF[]] $tail = @(
        [System.Drawing.PointF]::new($bubble.Left + $unit * 0.16, $bubble.Bottom - $unit * 0.02),
        [System.Drawing.PointF]::new($bubble.Left + $unit * 0.07, $bubble.Bottom + $unit * 0.14),
        [System.Drawing.PointF]::new($bubble.Left + $unit * 0.31, $bubble.Bottom))

    $bubblePath = New-RoundedRectanglePath -Bounds $bubble -Radius ($unit * 0.09)
    $bubblePath.StartFigure()
    $bubblePath.AddPolygon($tail)
    $bubblePath.CloseFigure()
    $whiteBrush = [System.Drawing.SolidBrush]::new($white)
    try {
        $Graphics.FillPath($whiteBrush, $bubblePath)
    }
    finally {
        $whiteBrush.Dispose()
        $bubblePath.Dispose()
    }

    $envelope = [System.Drawing.RectangleF]::new(
        $offsetX + $unit * 0.47,
        $unit * 0.36,
        $unit * 0.24,
        $unit * 0.20)
    $blueBrush = [System.Drawing.SolidBrush]::new($metroBlue)
    $trailPen = [System.Drawing.Pen]::new($metroBlue, [Math]::Max(1.0, $unit * 0.036))
    $seamPen = [System.Drawing.Pen]::new($white, [Math]::Max(1.0, $unit * 0.035))
    try {
        $trailPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $trailPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $seamPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $seamPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

        foreach ($fraction in @(0.40, 0.50, 0.60)) {
            $y = [single]($unit * $fraction)
            $Graphics.DrawLine($trailPen, $offsetX + $unit * 0.29, $y, $offsetX + $unit * 0.42, $y)
        }

        $Graphics.FillRectangle($blueBrush, $envelope)
        $Graphics.DrawLines(
            $seamPen,
            [System.Drawing.PointF[]]@(
                [System.Drawing.PointF]::new($envelope.Left + $unit * 0.015, $envelope.Top + $unit * 0.015),
                [System.Drawing.PointF]::new($envelope.Left + $envelope.Width / 2.0, $envelope.Top + $envelope.Height * 0.60),
                [System.Drawing.PointF]::new($envelope.Right - $unit * 0.015, $envelope.Top + $unit * 0.015)))
    }
    finally {
        $seamPen.Dispose()
        $trailPen.Dispose()
        $blueBrush.Dispose()
    }
}

function Draw-LockScreenBadge {
    param(
        [System.Drawing.Graphics] $Graphics,
        [int] $Width,
        [int] $Height
    )

    $unit = [single][Math]::Min($Width, $Height)
    $offsetX = [single](($Width - $unit) / 2.0)
    $bubble = [System.Drawing.RectangleF]::new(
        $offsetX + $unit * 0.17,
        $unit * 0.19,
        $unit * 0.66,
        $unit * 0.56)
    $outlinePen = [System.Drawing.Pen]::new($white, [Math]::Max(1.0, $unit * 0.105))
    $envelopePen = [System.Drawing.Pen]::new($white, [Math]::Max(1.0, $unit * 0.072))
    $bubblePath = New-RoundedRectanglePath -Bounds $bubble -Radius ($unit * 0.10)
    try {
        $outlinePen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $outlinePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $outlinePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $envelopePen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $envelopePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $envelopePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

        $Graphics.DrawPath($outlinePen, $bubblePath)
        $Graphics.DrawLines(
            $outlinePen,
            [System.Drawing.PointF[]]@(
                [System.Drawing.PointF]::new($bubble.Left + $unit * 0.20, $bubble.Bottom - $unit * 0.02),
                [System.Drawing.PointF]::new($bubble.Left + $unit * 0.10, $bubble.Bottom + $unit * 0.13),
                [System.Drawing.PointF]::new($bubble.Left + $unit * 0.32, $bubble.Bottom)))

        $envelope = [System.Drawing.RectangleF]::new(
            $offsetX + $unit * 0.34,
            $unit * 0.37,
            $unit * 0.32,
            $unit * 0.22)
        $Graphics.DrawRectangle($envelopePen, $envelope.X, $envelope.Y, $envelope.Width, $envelope.Height)
        $Graphics.DrawLines(
            $envelopePen,
            [System.Drawing.PointF[]]@(
                [System.Drawing.PointF]::new($envelope.Left, $envelope.Top),
                [System.Drawing.PointF]::new($envelope.Left + $envelope.Width / 2.0, $envelope.Top + $envelope.Height * 0.65),
                [System.Drawing.PointF]::new($envelope.Right, $envelope.Top)))
    }
    finally {
        $bubblePath.Dispose()
        $envelopePen.Dispose()
        $outlinePen.Dispose()
    }
}

function Save-Logo {
    param(
        [string] $FileName,
        [int] $Width,
        [int] $Height,
        [switch] $LockScreen
    )

    $bitmap = [System.Drawing.Bitmap]::new($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = New-Graphics -Bitmap $bitmap
    try {
        if ($LockScreen) {
            Draw-LockScreenBadge -Graphics $graphics -Width $Width -Height $Height
        }
        else {
            Draw-MetroMark -Graphics $graphics -Width $Width -Height $Height
        }

        $bitmap.Save((Join-Path $logosDirectory $FileName), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$assets = @(
    @{ Name = 'LockScreenLogo.scale-100.png'; Width = 24; Height = 24; LockScreen = $true },
    @{ Name = 'LockScreenLogo.scale-125.png'; Width = 30; Height = 30; LockScreen = $true },
    @{ Name = 'LockScreenLogo.scale-150.png'; Width = 36; Height = 36; LockScreen = $true },
    @{ Name = 'LockScreenLogo.scale-200.png'; Width = 48; Height = 48; LockScreen = $true },
    @{ Name = 'LockScreenLogo.scale-400.png'; Width = 96; Height = 96; LockScreen = $true },
    @{ Name = 'SplashScreen.scale-100.png'; Width = 620; Height = 300 },
    @{ Name = 'SplashScreen.scale-125.png'; Width = 775; Height = 375 },
    @{ Name = 'SplashScreen.scale-150.png'; Width = 930; Height = 450 },
    @{ Name = 'SplashScreen.scale-200.png'; Width = 1240; Height = 600 },
    @{ Name = 'SplashScreen.scale-400.png'; Width = 2480; Height = 1200 },
    @{ Name = 'Square150x150Logo.scale-100.png'; Width = 150; Height = 150 },
    @{ Name = 'Square150x150Logo.scale-125.png'; Width = 188; Height = 188 },
    @{ Name = 'Square150x150Logo.scale-150.png'; Width = 225; Height = 225 },
    @{ Name = 'Square150x150Logo.scale-200.png'; Width = 300; Height = 300 },
    @{ Name = 'Square150x150Logo.scale-400.png'; Width = 600; Height = 600 },
    @{ Name = 'Square310x310Logo.scale-100.png'; Width = 310; Height = 310 },
    @{ Name = 'Square310x310Logo.scale-125.png'; Width = 388; Height = 388 },
    @{ Name = 'Square310x310Logo.scale-150.png'; Width = 465; Height = 465 },
    @{ Name = 'Square310x310Logo.scale-200.png'; Width = 620; Height = 620 },
    @{ Name = 'Square310x310Logo.scale-400.png'; Width = 1240; Height = 1240 },
    @{ Name = 'Square44x44Logo.scale-100.png'; Width = 44; Height = 44 },
    @{ Name = 'Square44x44Logo.scale-125.png'; Width = 55; Height = 55 },
    @{ Name = 'Square44x44Logo.scale-150.png'; Width = 66; Height = 66 },
    @{ Name = 'Square44x44Logo.scale-200.png'; Width = 88; Height = 88 },
    @{ Name = 'Square44x44Logo.scale-400.png'; Width = 176; Height = 176 },
    @{ Name = 'Square44x44Logo.targetsize-16.png'; Width = 16; Height = 16 },
    @{ Name = 'Square44x44Logo.targetsize-24.png'; Width = 24; Height = 24 },
    @{ Name = 'Square44x44Logo.targetsize-32.png'; Width = 32; Height = 32 },
    @{ Name = 'Square44x44Logo.targetsize-48.png'; Width = 48; Height = 48 },
    @{ Name = 'Square44x44Logo.targetsize-256.png'; Width = 256; Height = 256 },
    @{ Name = 'Square44x44Logo.altform-lightunplated_targetsize-16.png'; Width = 16; Height = 16 },
    @{ Name = 'Square44x44Logo.altform-lightunplated_targetsize-24.png'; Width = 24; Height = 24 },
    @{ Name = 'Square44x44Logo.altform-lightunplated_targetsize-32.png'; Width = 32; Height = 32 },
    @{ Name = 'Square44x44Logo.altform-lightunplated_targetsize-48.png'; Width = 48; Height = 48 },
    @{ Name = 'Square44x44Logo.altform-lightunplated_targetsize-256.png'; Width = 256; Height = 256 },
    @{ Name = 'Square44x44Logo.altform-unplated_targetsize-16.png'; Width = 16; Height = 16 },
    @{ Name = 'Square44x44Logo.altform-unplated_targetsize-24.png'; Width = 24; Height = 24 },
    @{ Name = 'Square44x44Logo.altform-unplated_targetsize-32.png'; Width = 32; Height = 32 },
    @{ Name = 'Square44x44Logo.altform-unplated_targetsize-48.png'; Width = 48; Height = 48 },
    @{ Name = 'Square44x44Logo.altform-unplated_targetsize-256.png'; Width = 256; Height = 256 },
    @{ Name = 'Square71x71Logo.scale-100.png'; Width = 71; Height = 71 },
    @{ Name = 'Square71x71Logo.scale-125.png'; Width = 89; Height = 89 },
    @{ Name = 'Square71x71Logo.scale-150.png'; Width = 107; Height = 107 },
    @{ Name = 'Square71x71Logo.scale-200.png'; Width = 142; Height = 142 },
    @{ Name = 'Square71x71Logo.scale-400.png'; Width = 284; Height = 284 },
    @{ Name = 'StoreLogo.scale-100.png'; Width = 50; Height = 50 },
    @{ Name = 'StoreLogo.scale-125.png'; Width = 63; Height = 63 },
    @{ Name = 'StoreLogo.scale-150.png'; Width = 75; Height = 75 },
    @{ Name = 'StoreLogo.scale-200.png'; Width = 100; Height = 100 },
    @{ Name = 'StoreLogo.scale-400.png'; Width = 200; Height = 200 },
    @{ Name = 'Wide310x150Logo.scale-100.png'; Width = 310; Height = 150 },
    @{ Name = 'Wide310x150Logo.scale-125.png'; Width = 388; Height = 188 },
    @{ Name = 'Wide310x150Logo.scale-150.png'; Width = 465; Height = 225 },
    @{ Name = 'Wide310x150Logo.scale-200.png'; Width = 620; Height = 300 },
    @{ Name = 'Wide310x150Logo.scale-400.png'; Width = 1240; Height = 600 }
)

foreach ($asset in $assets) {
    $isLockScreen = $asset.ContainsKey('LockScreen') -and $asset.LockScreen
    Save-Logo -FileName $asset.Name -Width $asset.Width -Height $asset.Height -LockScreen:$isLockScreen
}

Write-Host "Generated $($assets.Count) Metro logo assets in $logosDirectory."
