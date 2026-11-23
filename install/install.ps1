#Requires -Version 5.1
<#
.SYNOPSIS
    Install Loadout (formerly Startup Profiles) for the current user (no elevation required).

.DESCRIPTION
    Publishes Loadout, installs it to %LOCALAPPDATA%\Programs\Loadout, adds a Start
    Menu shortcut, registers the launcher to run at login, registers the loadout:// and startupprofiles:// protocols,
    and installs the Claude agent skill to %USERPROFILE%\.claude\skills. All state is per-user.

    Unless -KeepStartupApps (or -NoStartup) is given, it then takes over Windows startup: every startup
    app that is currently on and can be switched per-user is added to the Everything profile and switched
    off in Windows (reversibly, exactly like Task Manager), so Loadout is the one app that starts
    at login. All-users entries (which need admin) are left on. uninstall.ps1 switches them back on.

    Login, protocol, and startup changes are performed by the app itself (Loadout.exe
    --register-login / --register-protocol / --take-over-startup) so the registry formats stay owned by
    the app's seams.

.PARAMETER Port
    Loopback API port to persist in config.json (default 8790). Only written when non-default.

.PARAMETER FrameworkDependent
    Publish a smaller framework-dependent build (requires the .NET 10 Desktop Runtime installed).
    The default is a self-contained build that bundles the runtime.

.PARAMETER NoStartup
    Do not register the launcher to run at login. Implies -KeepStartupApps.

.PARAMETER KeepStartupApps
    Leave existing Windows startup apps as they are instead of moving them into the Everything profile.

.PARAMETER NoProtocol
    Do not register the loadout:// (and old startupprofiles://) protocol handler.

.PARAMETER NoSkill
    Do not install the Claude agent skill.

.EXAMPLE
    .\install\install.ps1
.EXAMPLE
    .\install\install.ps1 -FrameworkDependent -NoStartup
#>
[CmdletBinding()]
param(
    [int]    $Port = 8790,
    [switch] $FrameworkDependent,
    [switch] $NoStartup,
    [switch] $KeepStartupApps,
    [switch] $NoProtocol,
    [switch] $NoSkill
)

$ErrorActionPreference = 'Stop'

$RepoRoot   = Split-Path -Parent $PSScriptRoot
$AppProject = Join-Path $RepoRoot 'src\Loadout.App\Loadout.App.csproj'
$InstallDir = Join-Path $env:LOCALAPPDATA 'Programs\Loadout'
$ExePath    = Join-Path $InstallDir 'Loadout.exe'
# The old name (Startup Profiles): removed on install, so an upgrade leaves one copy.
$OldInstallDir = Join-Path $env:LOCALAPPDATA 'Programs\StartupProfiles'

function Write-Step($message) { Write-Host "==> $message" -ForegroundColor Cyan }

function Stop-RunningInstance {
    Get-Process -Name 'Loadout', 'StartupProfiles' -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Step "Stopping running instance (pid $($_.Id))"
        $_ | Stop-Process -Force
    }
    Start-Sleep -Milliseconds 400
}

function Publish-App {
    $selfContained = (-not $FrameworkDependent).ToString().ToLower()
    Write-Step "Publishing (self-contained=$selfContained) to $InstallDir"

    if (Test-Path $InstallDir) { Remove-Item -Recurse -Force $InstallDir }
    New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null

    & dotnet publish $AppProject -c Release -r win-x64 --self-contained $selfContained `
        -p:PublishSingleFile=false -o $InstallDir --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }
    if (-not (Test-Path $ExePath)) { throw "Publish did not produce $ExePath." }
}

function Add-StartMenuShortcut {
    $startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
    $lnk = Join-Path $startMenu 'Loadout.lnk'
    Write-Step "Creating Start Menu shortcut"
    $oldLnk = Join-Path $startMenu 'Startup Profiles.lnk'
    if (Test-Path $oldLnk) { Remove-Item -Path $oldLnk -Force }
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($lnk)
    $shortcut.TargetPath = $ExePath
    $shortcut.WorkingDirectory = $InstallDir
    $shortcut.Description = 'Loadout'
    $shortcut.Save()
}

function Set-ConfigPort {
    if ($Port -eq 8790) { return }
    Write-Step "Persisting API port $Port"
    # Before the first start under the new name, the data still lives in the old folder and the app copies
    # it over; creating the new folder now would stop that copy, so write the port to the old one then.
    $new = Join-Path $env:APPDATA 'Loadout'
    $old = Join-Path $env:APPDATA 'StartupProfiles'
    $dir = if ((-not (Test-Path $new)) -and (Test-Path $old)) { $old } else { $new }
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $path = Join-Path $dir 'config.json'
    $config = if (Test-Path $path) { Get-Content $path -Raw | ConvertFrom-Json } else { $null }
    if ($null -eq $config) { $config = [pscustomobject]@{} }
    $config | Add-Member -NotePropertyName 'port' -NotePropertyValue "$Port" -Force
    $config | ConvertTo-Json | Set-Content -Path $path -Encoding utf8
}

function Invoke-Maintenance($argument) {
    # The app is a GUI-subsystem exe, so capture its stdout report through a file and echo it.
    $report = [System.IO.Path]::GetTempFileName()
    try {
        $proc = Start-Process -FilePath $ExePath -ArgumentList $argument -Wait -PassThru -NoNewWindow `
            -RedirectStandardOutput $report
        Get-Content $report | ForEach-Object { Write-Host "    $_" }
        if ($proc.ExitCode -ne 0) { throw "$ExePath $argument exited with $($proc.ExitCode)." }
    }
    finally {
        Remove-Item $report -ErrorAction SilentlyContinue
    }
}

function Install-Skill {
    $source = Join-Path $RepoRoot 'skills\loadout'
    if (-not (Test-Path $source)) { Write-Warning "Agent skill not found at $source; skipping."; return }
    $dest = Join-Path $env:USERPROFILE '.claude\skills\loadout'
    $oldSkill = Join-Path $env:USERPROFILE '.claude\skills\startup-profiles'
    if (Test-Path $oldSkill) { Remove-Item -Recurse -Force $oldSkill }
    Write-Step "Installing agent skill to $dest"
    if (Test-Path $dest) { Remove-Item -Recurse -Force $dest }
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Copy-Item -Path (Join-Path $source '*') -Destination $dest -Recurse -Force
}

Write-Host "Installing Loadout..." -ForegroundColor Green
Stop-RunningInstance
if (Test-Path $OldInstallDir) { Write-Step "Removing the old Startup Profiles program folder"; Remove-Item -Recurse -Force $OldInstallDir }
Publish-App
Add-StartMenuShortcut
Set-ConfigPort

if (-not $NoStartup) { Write-Step "Registering launcher at login"; Invoke-Maintenance '--register-login' }
if (-not $NoStartup -and -not $KeepStartupApps) {
    Write-Step "Moving Windows startup apps into the Everything profile"
    Invoke-Maintenance '--take-over-startup'
}
if (-not $NoProtocol) { Write-Step "Registering loadout:// and startupprofiles:// protocols"; Invoke-Maintenance '--register-protocol' }
if (-not $NoSkill) { Install-Skill }

Write-Host ""
Write-Host "Installed to $InstallDir" -ForegroundColor Green
Write-Host "Launch it now:  `"$ExePath`""
if (-not $NoStartup) { Write-Host "It will also appear at your next login." }
if (-not $NoStartup -and -not $KeepStartupApps) {
    Write-Host "Your previous startup apps now run from the Everything profile; uninstall.ps1 switches them back on."
}
