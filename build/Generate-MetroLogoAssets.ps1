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

    $designWidth = if ($Width -gt ($Height * 1.5)) {
        [single]($Height * 0.785)
    }
    else {
        [single]([Math]::Min($Width, $Height) * 0.785)
    }
    $bodyHeight = [single]($designWidth * 0.697)
    $designHeight = [single]($bodyHeight + ($designWidth * 0.084))
    $x = [single](($Width - $designWidth) / 2.0)
    $y = [single](($Height - $designHeight) / 2.0 + ($designWidth * 0.03))
    $body = [System.Drawing.RectangleF]::new($x, $y, $designWidth, $bodyHeight)
    [System.Drawing.PointF[]] $tail = @(
        [System.Drawing.PointF]::new($x + ($designWidth * 0.162), $y + $bodyHeight),
        [System.Drawing.PointF]::new($x + ($designWidth * 0.077), $y + $bodyHeight + ($designWidth * 0.107)),
        [System.Drawing.PointF]::new($x + ($designWidth * 0.321), $y + $bodyHeight))
    $bodyPath = New-RoundedRectanglePath -Bounds $body -Radius ($bodyHeight * 0.229)
    $whiteBrush = [System.Drawing.SolidBrush]::new($white)
    $blueBrush = [System.Drawing.SolidBrush]::new($metroBlue)
    $trailPen = [System.Drawing.Pen]::new($metroBlue, [Math]::Max(1.0, $designWidth * 0.042))
    try {
        $Graphics.FillPath($whiteBrush, $bodyPath)
        $Graphics.FillPolygon($whiteBrush, $tail)

        [System.Drawing.PointF[]] $upperWing = @(
            [System.Drawing.PointF]::new($x + ($designWidth * 0.361), $y + ($bodyHeight * 0.311)),
            [System.Drawing.PointF]::new($x + ($designWidth * 0.955), $y + ($bodyHeight * 0.493)),
            [System.Drawing.PointF]::new($x + ($designWidth * 0.291), $y + ($bodyHeight * 0.400)))
        [System.Drawing.PointF[]] $lowerWing = @(
            [System.Drawing.PointF]::new($x + ($designWidth * 0.269), $y + ($bodyHeight * 0.486)),
            [System.Drawing.PointF]::new($x + ($designWidth * 0.341), $y + ($bodyHeight * 0.636)),
            [System.Drawing.PointF]::new($x + ($designWidth * 0.955), $y + ($bodyHeight * 0.493)))
        $Graphics.FillPolygon($blueBrush, $upperWing)
        $Graphics.FillPolygon($blueBrush, $lowerWing)

        $trailPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $trailPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $Graphics.DrawLine($trailPen, $x + ($designWidth * 0.092), $y + ($bodyHeight * 0.368), $x + ($designWidth * 0.221), $y + ($bodyHeight * 0.368))
        $Graphics.DrawLine($trailPen, $x + ($designWidth * 0.077), $y + ($bodyHeight * 0.482), $x + ($designWidth * 0.209), $y + ($bodyHeight * 0.482))
        $Graphics.DrawLine($trailPen, $x + ($designWidth * 0.104), $y + ($bodyHeight * 0.596), $x + ($designWidth * 0.236), $y + ($bodyHeight * 0.596))
    }
    finally {
        $trailPen.Dispose()
        $blueBrush.Dispose()
        $whiteBrush.Dispose()
        $bodyPath.Dispose()
    }
}

function Draw-LockScreenBadge {
    param(
        [System.Drawing.Graphics] $Graphics,
        [int] $Width,
        [int] $Height
    )

    $designWidth = [single]([Math]::Min($Width, $Height) * 0.785)
    $bodyHeight = [single]($designWidth * 0.697)
    $designHeight = [single]($bodyHeight + ($designWidth * 0.084))
    $x = [single](($Width - $designWidth) / 2.0)
    $y = [single](($Height - $designHeight) / 2.0 + ($designWidth * 0.03))
    $lineWidth = [Math]::Max(1.0, $designWidth * 0.055)
    $outlinePen = [System.Drawing.Pen]::new($white, $lineWidth)
    $radius = [single]($bodyHeight * 0.229)
    $diameter = [single]($radius * 2.0)
    $bottom = [single]($y + $bodyHeight)
    $tailLeft = [single]($x + ($designWidth * 0.162))
    $tailRight = [single]($x + ($designWidth * 0.321))
    $tailTip = [System.Drawing.PointF]::new($x + ($designWidth * 0.077), $bottom + ($designWidth * 0.107))
    $bodyPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
    try {
        $outlinePen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $bodyPath.StartFigure()
        $bodyPath.AddArc($x, $y, $diameter, $diameter, 180, 90)
        $bodyPath.AddArc($x + $designWidth - $diameter, $y, $diameter, $diameter, 270, 90)
        $bodyPath.AddArc($x + $designWidth - $diameter, $bottom - $diameter, $diameter, $diameter, 0, 90)
        $bodyPath.AddLine($x + $designWidth - $radius, $bottom, $tailRight, $bottom)
        $bodyPath.AddLine($tailRight, $bottom, $tailTip.X, $tailTip.Y)
        $bodyPath.AddLine($tailTip.X, $tailTip.Y, $tailLeft, $bottom)
        $bodyPath.AddLine($tailLeft, $bottom, $x + $radius, $bottom)
        $bodyPath.AddArc($x, $bottom - $diameter, $diameter, $diameter, 90, 90)
        $bodyPath.CloseFigure()
        $Graphics.DrawPath($outlinePen, $bodyPath)
        [System.Drawing.PointF[]] $upperWing = @(
            [System.Drawing.PointF]::new($x + ($designWidth * 0.361), $y + ($bodyHeight * 0.311)),
            [System.Drawing.PointF]::new($x + ($designWidth * 0.955), $y + ($bodyHeight * 0.493)),
            [System.Drawing.PointF]::new($x + ($designWidth * 0.291), $y + ($bodyHeight * 0.400)))
        [System.Drawing.PointF[]] $lowerWing = @(
            [System.Drawing.PointF]::new($x + ($designWidth * 0.269), $y + ($bodyHeight * 0.486)),
            [System.Drawing.PointF]::new($x + ($designWidth * 0.341), $y + ($bodyHeight * 0.636)),
            [System.Drawing.PointF]::new($x + ($designWidth * 0.955), $y + ($bodyHeight * 0.493)))
        $gliderBrush = [System.Drawing.SolidBrush]::new($white)
        try {
            $Graphics.FillPolygon($gliderBrush, $upperWing)
            $Graphics.FillPolygon($gliderBrush, $lowerWing)
        }
        finally {
            $gliderBrush.Dispose()
        }

        $Graphics.DrawLine($outlinePen, $x + ($designWidth * 0.092), $y + ($bodyHeight * 0.368), $x + ($designWidth * 0.221), $y + ($bodyHeight * 0.368))
        $Graphics.DrawLine($outlinePen, $x + ($designWidth * 0.077), $y + ($bodyHeight * 0.482), $x + ($designWidth * 0.209), $y + ($bodyHeight * 0.482))
        $Graphics.DrawLine($outlinePen, $x + ($designWidth * 0.104), $y + ($bodyHeight * 0.596), $x + ($designWidth * 0.236), $y + ($bodyHeight * 0.596))
    }
    finally {
        $bodyPath.Dispose()
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
