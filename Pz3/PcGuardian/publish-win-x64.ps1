$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishDir = Join-Path $projectRoot "dist\PcGuardian-win-x64"

dotnet publish $projectRoot `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

if (!(Test-Path (Join-Path $publishDir "PcGuardian.exe"))) {
    throw "Publish finished, but PcGuardian.exe was not found."
}

Write-Host "Published: $publishDir\PcGuardian.exe"
