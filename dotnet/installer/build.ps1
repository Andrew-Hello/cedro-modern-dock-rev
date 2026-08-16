param(
    [string]$Version = "1.2.0"
)

$ErrorActionPreference = "Stop"

$installerDir = Split-Path $PSScriptRoot -Parent
$solutionDir = Split-Path $installerDir -Parent

Write-Host "Publishing CedroModernDock (Release, win-x64, self-contained single-file)..."
dotnet publish "$solutionDir\src\CedroModernDock\CedroModernDock.csproj" `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false `
    -o "$installerDir\publish"
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

Write-Host "Building MSI $Version..."
$msi = Join-Path $installerDir "CedroModernDock-$Version-x64.msi"
wix build "$installerDir\Installer.wxs" -o $msi -arch x64 `
    -ext WixToolset.UI.wixext -ext WixToolset.Util.wixext -d "ProductVersion=$Version"
if ($LASTEXITCODE -ne 0) { throw "wix build failed." }

Write-Host "Built: $msi"
