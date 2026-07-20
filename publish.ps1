<#
.SYNOPSIS
  Builds self-contained, single-file executables of CoffinTranslate for release.

.DESCRIPTION
  Produces one standalone file per platform in .\publish\ that users can download from
  GitHub Releases and run directly. No .NET runtime install required. Everything (the
  .NET runtime + Avalonia's native libraries) is bundled into the single file.

.EXAMPLE
  .\publish.ps1
  Builds win-x64 and linux-x64.

.EXAMPLE
  .\publish.ps1 -Runtimes win-x64, win-arm64, linux-x64, osx-arm64
  Builds a custom set of platforms.
#>
[CmdletBinding()]
param(
    [string[]] $Runtimes = @('win-x64', 'linux-x64'),
    [string]   $OutDir   = (Join-Path $PSScriptRoot 'publish')
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'src\CoffinTranslate\CoffinTranslate.csproj'

if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }
New-Item -ItemType Directory -Path $OutDir | Out-Null

foreach ($rid in $Runtimes) {
    Write-Host "==> $rid" -ForegroundColor Cyan
    $work = Join-Path $OutDir "_$rid"

    dotnet publish $project -c Release -r $rid --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=none -p:DebugSymbols=false `
        -o $work --nologo
    if ($LASTEXITCODE -ne 0) { throw "publish failed for $rid" }

    # single-file publish leaves the app binary (plus discardable native .pdb symbols) -> keep only the binary
    $isWin   = $rid -like 'win-*'
    $srcName = if ($isWin) { 'CoffinTranslate.exe' } else { 'CoffinTranslate' }
    $ext     = if ($isWin) { '.exe' } else { '' }
    $dest    = Join-Path $OutDir "CoffinTranslate-$rid$ext"

    Copy-Item (Join-Path $work $srcName) $dest -Force
    Remove-Item $work -Recurse -Force
}

Write-Host "`nFertig -> $OutDir" -ForegroundColor Green
Get-ChildItem $OutDir | Select-Object Name, @{ N = 'MB'; E = { [math]::Round($_.Length / 1MB, 1) } } | Format-Table -AutoSize
