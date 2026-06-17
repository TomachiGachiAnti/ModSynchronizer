param(
    [string]$ProfileFile = "industrial-1.21.1.json",
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$AppVersion = "1.0.0"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $repoRoot "src\\ModSynchronizer.App\\ModSynchronizer.App.csproj"
$profilesRoot = Join-Path $repoRoot "profiles"
$assetsRoot = Join-Path $repoRoot "assets"
$publishRoot = Join-Path $repoRoot "publish"

$profilePath = Join-Path $profilesRoot $ProfileFile
if (-not (Test-Path -LiteralPath $profilePath)) {
    throw "対象 profile が見つかりません: $profilePath"
}

$profileBaseName = [System.IO.Path]::GetFileNameWithoutExtension($ProfileFile)
$publishDir = Join-Path $publishRoot $profileBaseName

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

$legacyManifestPath = Join-Path $publishDir "update-manifest.json"
if (Test-Path -LiteralPath $legacyManifestPath) {
    Remove-Item -LiteralPath $legacyManifestPath -Force
}

dotnet publish $appProject `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained true `
    /p:EmbeddedProfileName=$profileBaseName `
    /p:Version=$AppVersion `
    /p:AssemblyVersion=$AppVersion `
    /p:FileVersion=$AppVersion `
    /p:InformationalVersion=$AppVersion `
    /p:PublishSingleFile=true `
    /p:EnableCompressionInSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:PublishTrimmed=false `
    -o $publishDir

$publishProfilesDir = Join-Path $publishDir "profiles"
if (Test-Path -LiteralPath $publishProfilesDir) {
    Remove-Item -LiteralPath $publishProfilesDir -Recurse -Force
}

$profileAssetDir = Join-Path $assetsRoot $profileBaseName
$publishAssetsDir = Join-Path $publishDir "assets"
if (Test-Path -LiteralPath $publishAssetsDir) {
    Remove-Item -LiteralPath $publishAssetsDir -Recurse -Force
}

$defaultExe = Join-Path $publishDir "ModSynchronizer.App.exe"
$targetExe = Join-Path $publishDir ($profileBaseName + "-Setup.exe")
if (Test-Path -LiteralPath $defaultExe) {
    Move-Item -LiteralPath $defaultExe -Destination $targetExe
}

Get-ChildItem -LiteralPath $publishDir -Filter *.pdb -File | Remove-Item -Force

if (Test-Path -LiteralPath $publishAssetsDir) {
    $remainingAssetFiles = Get-ChildItem -LiteralPath $publishAssetsDir -Recurse -File |
        Where-Object { $_.Name -ne '.gitkeep' }
    if ($remainingAssetFiles.Count -eq 0) {
        Remove-Item -LiteralPath $publishAssetsDir -Recurse -Force
    }
}

Write-Output "publish_dir=$publishDir"
Write-Output "exe_path=$targetExe"
