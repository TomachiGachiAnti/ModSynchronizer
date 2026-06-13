param(
    [string]$ProfileFile = "industrial-1.21.1.json",
    [string]$WorkRoot = "",
    [switch]$SkipNeoForgeInstall,
    [switch]$SkipServerJar,
    [switch]$SkipConfigSync
)

$ErrorActionPreference = "Stop"

function Write-Log {
    param([string]$Message)
    Write-Output "[setup-server-local] $Message"
}

function Get-RepoRoot {
    return (Split-Path -Parent $PSScriptRoot)
}

function Get-ProfilePath {
    param(
        [string]$RepoRoot,
        [string]$ProfileFile
    )

    $profilesRoot = Join-Path $RepoRoot "profiles"
    $profilePath = Join-Path $profilesRoot $ProfileFile
    if (-not (Test-Path -LiteralPath $profilePath)) {
        throw "Profile not found: $profilePath"
    }

    return $profilePath
}

function Get-ResolvedWorkRoot {
    param(
        [string]$RepoRoot,
        [string]$ProfileName,
        [string]$RequestedWorkRoot
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedWorkRoot)) {
        return $RequestedWorkRoot
    }

    return (Join-Path $RepoRoot "local-server\$ProfileName")
}

function Ensure-Directory {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path | Out-Null
    }
}

function Resolve-JavaPath {
    $javaCommand = Get-Command java.exe -ErrorAction SilentlyContinue
    if ($null -ne $javaCommand) {
        return $javaCommand.Source
    }

    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "Packages\Microsoft.4297127D64EC6_8wekyb3d8bbwe\LocalCache\Local\runtime\java-runtime-delta\windows-x64\java-runtime-delta\bin\java.exe"),
        (Join-Path $env:LOCALAPPDATA "Packages\Microsoft.4297127D64EC6_8wekyb3d8bbwe\LocalCache\Local\runtime\jre-legacy\windows-x64\jre-legacy\bin\java.exe"),
        (Join-Path $env:LOCALAPPDATA "Packages\Microsoft.4297127D64EC6_8wekyb3d8bbwe\LocalCache\Local\MinecraftInstaller\runtime\java-runtime-delta\windows-x64\java-runtime-delta\bin\java.exe"),
        (Join-Path $env:ProgramFiles "Minecraft Launcher\runtime\java-runtime-delta\windows-x64\java-runtime-delta\bin\java.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Minecraft Launcher\runtime\java-runtime-delta\windows-x64\java-runtime-delta\bin\java.exe")
    )

    foreach ($candidate in $candidates) {
        if (-not [string]::IsNullOrWhiteSpace($candidate) -and (Test-Path -LiteralPath $candidate)) {
            return $candidate
        }
    }

    throw "java.exe was not found. Add Java 21+ to PATH or launch Minecraft Launcher first."
}

function Invoke-DownloadFile {
    param(
        [string]$Url,
        [string]$Destination
    )

    $destinationDirectory = Split-Path -Parent $Destination
    if (-not [string]::IsNullOrWhiteSpace($destinationDirectory)) {
        Ensure-Directory -Path $destinationDirectory
    }

    Invoke-WebRequest -Uri $Url -OutFile $Destination
}

function Test-Sha256 {
    param(
        [string]$Path,
        [string]$Expected
    )

    if ([string]::IsNullOrWhiteSpace($Expected) -or -not (Test-Path -LiteralPath $Path)) {
        return $false
    }

    return ((Get-FileHash -Path $Path -Algorithm SHA256).Hash.ToLowerInvariant() -eq $Expected.ToLowerInvariant())
}

function Test-Sha1 {
    param(
        [string]$Path,
        [string]$Expected
    )

    if ([string]::IsNullOrWhiteSpace($Expected) -or -not (Test-Path -LiteralPath $Path)) {
        return $false
    }

    return ((Get-FileHash -Path $Path -Algorithm SHA1).Hash.ToLowerInvariant() -eq $Expected.ToLowerInvariant())
}

function Get-ExcludeSet {
    param(
        [string]$RepoRoot,
        [string]$ProfileName
    )

    $excludeChildPath = 'profiles\' + $ProfileName + '.server-excludes.txt'
    $excludeFile = Join-Path -Path $RepoRoot -ChildPath $excludeChildPath
    $set = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

    if (-not (Test-Path -LiteralPath $excludeFile)) {
        return $set
    }

    foreach ($line in Get-Content -Path $excludeFile) {
        $trimmed = $line.Split('#')[0].Trim()
        if (-not [string]::IsNullOrWhiteSpace($trimmed)) {
            [void]$set.Add($trimmed)
        }
    }

    return $set
}

function Sync-Mods {
    param(
        $Profile,
        [System.Collections.Generic.HashSet[string]]$ExcludeSet,
        [string]$ModsRoot,
        [string]$StateRoot
    )

    Ensure-Directory -Path $ModsRoot
    Ensure-Directory -Path $StateRoot

    $managedModsPath = Join-Path $StateRoot "managed-mods.txt"
    $previousManagedMods = @()
    if (Test-Path -LiteralPath $managedModsPath) {
        $previousManagedMods = Get-Content -Path $managedModsPath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    }

    $currentManagedMods = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

    foreach ($mod in $Profile.mods) {
        $destination = Join-Path $ModsRoot $mod.filename

        if ($mod.deprecated) {
            if (Test-Path -LiteralPath $destination) {
                Remove-Item -LiteralPath $destination -Force
                Write-Log "Removed deprecated: $($mod.filename)"
            }
            continue
        }

        if (-not $mod.required) {
            continue
        }

        if ($ExcludeSet.Contains($mod.filename)) {
            Write-Log "Excluded: $($mod.filename)"
            continue
        }

        if ([string]::IsNullOrWhiteSpace($mod.url)) {
            Write-Log "Skipped due to missing URL: $($mod.filename)"
            continue
        }

        [void]$currentManagedMods.Add($mod.filename)

        if (Test-Sha256 -Path $destination -Expected $mod.sha256) {
            Write-Log "Kept: $($mod.filename)"
            continue
        }

        Write-Log "Synced: $($mod.filename)"
        Invoke-DownloadFile -Url $mod.url -Destination $destination

        if (-not [string]::IsNullOrWhiteSpace($mod.sha256) -and -not (Test-Sha256 -Path $destination -Expected $mod.sha256)) {
            throw "SHA-256 verification failed: $($mod.filename)"
        }
    }

    foreach ($managedMod in $previousManagedMods) {
        if (-not $currentManagedMods.Contains($managedMod)) {
            $stalePath = Join-Path $ModsRoot $managedMod
            if (Test-Path -LiteralPath $stalePath) {
                Remove-Item -LiteralPath $stalePath -Force
                Write-Log "Removed stale: $managedMod"
            }
        }
    }

    [System.IO.File]::WriteAllLines(
        $managedModsPath,
        [string[]]$currentManagedMods,
        [System.Text.UTF8Encoding]::new($false))
}

function Sync-ConfigDirectory {
    param(
        $Profile,
        [string]$RepoRoot,
        [string]$ConfigRoot
    )

    $configBundlePath = $Profile.server_setup.config_bundle_path
    if ([string]::IsNullOrWhiteSpace($configBundlePath)) {
        return
    }

    $sourceRoot = Join-Path $RepoRoot $configBundlePath
    if (-not (Test-Path -LiteralPath $sourceRoot)) {
        return
    }

    Ensure-Directory -Path $ConfigRoot
    $files = Get-ChildItem -Path $sourceRoot -Recurse -File
    foreach ($file in $files) {
        if ($file.Name -eq ".gitkeep") {
            continue
        }

        $relativePath = [System.IO.Path]::GetRelativePath($sourceRoot, $file.FullName)
        $destination = Join-Path $ConfigRoot $relativePath
        $destinationDirectory = Split-Path -Parent $destination
        if (-not [string]::IsNullOrWhiteSpace($destinationDirectory)) {
            Ensure-Directory -Path $destinationDirectory
        }

        Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
        Write-Log "Config synced: $relativePath"
    }
}

function Ensure-ServerJar {
    param(
        $Profile,
        [string]$ServerRoot
    )

    $serverJarUrl = $Profile.server_setup.server_jar_url
    $serverJarSha1 = $Profile.server_setup.server_jar_sha1
    $serverJarPath = Join-Path $ServerRoot "server.jar"

    if ([string]::IsNullOrWhiteSpace($serverJarUrl)) {
        throw "server_setup.server_jar_url is not set."
    }

    if (Test-Sha1 -Path $serverJarPath -Expected $serverJarSha1) {
        Write-Log "server.jar is up to date."
        return
    }

    Write-Log "Downloading server.jar."
    Invoke-DownloadFile -Url $serverJarUrl -Destination $serverJarPath

    if (-not [string]::IsNullOrWhiteSpace($serverJarSha1) -and -not (Test-Sha1 -Path $serverJarPath -Expected $serverJarSha1)) {
        throw "server.jar SHA-1 verification failed."
    }
}

function Ensure-NeoForgeServer {
    param(
        $Profile,
        [string]$ServerRoot,
        [string]$DownloadsRoot,
        [string]$JavaPath
    )

    $loaderVersion = $Profile.loader.version
    $installerUrl = $Profile.loader.installer_url
    $installerPath = Join-Path $DownloadsRoot "neoforge-installer.jar"
    $runBatPath = Join-Path $ServerRoot "run.bat"
    $libraryChildPath = 'libraries\net\neoforged\neoforge\' + $loaderVersion
    $libraryPath = Join-Path -Path $ServerRoot -ChildPath $libraryChildPath

    if ((Test-Path -LiteralPath $runBatPath) -and (Test-Path -LiteralPath $libraryPath)) {
        Write-Log "NeoForge server is already installed."
        return
    }

    if ([string]::IsNullOrWhiteSpace($installerUrl)) {
        throw "loader.installer_url is not set."
    }

    Write-Log "Downloading NeoForge installer."
    Invoke-DownloadFile -Url $installerUrl -Destination $installerPath

    Write-Log "Installing NeoForge server."
    Push-Location $ServerRoot
    try {
        & $JavaPath -jar $installerPath --installServer
    }
    finally {
        Pop-Location
    }

    if (-not (Test-Path -LiteralPath $runBatPath)) {
        throw "run.bat was not found after NeoForge server installation."
    }
}

function Ensure-Eula {
    param([string]$ServerRoot)

    $eulaPath = Join-Path $ServerRoot "eula.txt"
    [System.IO.File]::WriteAllText($eulaPath, "eula=true" + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
}

function Ensure-UserJvmArgs {
    param([string]$ServerRoot)

    $jvmArgsPath = Join-Path $ServerRoot "user_jvm_args.txt"
    [System.IO.File]::WriteAllLines(
        $jvmArgsPath,
        [string[]]@(
            "-Xms4G"
            "-Xmx8G"
        ),
        [System.Text.UTF8Encoding]::new($false))
}

function Ensure-StartScript {
    param([string]$ServerRoot)

    $startScriptPath = Join-Path $ServerRoot "Start-LocalServer.ps1"
    $scriptContent = @'
$ErrorActionPreference = "Stop"
$serverRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$runBatPath = Join-Path $serverRoot "run.bat"
if (-not (Test-Path -LiteralPath $runBatPath)) {
    throw "run.bat was not found: $runBatPath"
}

Push-Location $serverRoot
try {
    & $runBatPath nogui
}
finally {
    Pop-Location
}
'@
    Set-Content -Path $startScriptPath -Value $scriptContent -Encoding utf8
}

$repoRoot = Get-RepoRoot
$profilePath = Get-ProfilePath -RepoRoot $repoRoot -ProfileFile $ProfileFile
$profileName = [System.IO.Path]::GetFileNameWithoutExtension($ProfileFile)
$resolvedWorkRoot = Get-ResolvedWorkRoot -RepoRoot $repoRoot -ProfileName $profileName -RequestedWorkRoot $WorkRoot
$downloadsRoot = Join-Path $resolvedWorkRoot ".downloads"
$stateRoot = Join-Path $resolvedWorkRoot ".modsetup"
$modsRoot = Join-Path $resolvedWorkRoot "mods"
$configRoot = Join-Path $resolvedWorkRoot "config"
$javaPath = Resolve-JavaPath

Write-Log "profile: $profileName"
Write-Log "work_root: $resolvedWorkRoot"
Write-Log "java: $javaPath"

Ensure-Directory -Path $resolvedWorkRoot
Ensure-Directory -Path $downloadsRoot

$profile = Get-Content -Path $profilePath -Raw | ConvertFrom-Json
$excludeSet = Get-ExcludeSet -RepoRoot $repoRoot -ProfileName $profileName

Sync-Mods -Profile $profile -ExcludeSet $excludeSet -ModsRoot $modsRoot -StateRoot $stateRoot

if (-not $SkipConfigSync) {
    Sync-ConfigDirectory -Profile $profile -RepoRoot $repoRoot -ConfigRoot $configRoot
}

if (-not $SkipServerJar) {
    Ensure-ServerJar -Profile $profile -ServerRoot $resolvedWorkRoot
}

if (-not $SkipNeoForgeInstall) {
    Ensure-NeoForgeServer -Profile $profile -ServerRoot $resolvedWorkRoot -DownloadsRoot $downloadsRoot -JavaPath $javaPath
}

Ensure-Eula -ServerRoot $resolvedWorkRoot
Ensure-UserJvmArgs -ServerRoot $resolvedWorkRoot
Ensure-StartScript -ServerRoot $resolvedWorkRoot

Write-Log "Local server verification environment is ready."
$startScriptPath = Join-Path $resolvedWorkRoot "Start-LocalServer.ps1"
Write-Log ('Start with: powershell -ExecutionPolicy Bypass -File "{0}"' -f $startScriptPath)
