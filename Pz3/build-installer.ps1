$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$appProject = Join-Path $root "PcGuardian"
$setupProject = Join-Path $root "PcGuardianSetup"
$setupOut = Join-Path $root "installer-build"
$finalInstaller = Join-Path $root "install.exe"

& (Join-Path $appProject "create-icon.ps1")
& (Join-Path $appProject "publish-win-x64.ps1")
if ($LASTEXITCODE -ne 0) {
    throw "App publish failed with exit code $LASTEXITCODE"
}

dotnet publish $setupProject `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $setupOut
if ($LASTEXITCODE -ne 0) {
    throw "Installer publish failed with exit code $LASTEXITCODE"
}

Copy-Item -Force (Join-Path $setupOut "PcGuardianSetup.exe") $finalInstaller
Write-Host "Installer created: $finalInstaller"
