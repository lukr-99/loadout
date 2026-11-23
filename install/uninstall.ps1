#Requires -Version 5.1
<#
.SYNOPSIS
    Uninstall Loadout (and the old Startup Profiles, if still installed) for the current user.

.DESCRIPTION
    Stops any running instance, switches back on the Windows startup apps the installer took over,
    removes the login registration and the loadout:// and startupprofiles:// protocols, deletes the installed program,
    the Start Menu shortcut, and the installed agent skill. With -PurgeData it also deletes
    %APPDATA%\Loadout and the old %APPDATA%\StartupProfiles (profiles, config, history).

    Re-enabling startup apps runs through the app (Loadout.exe --restore-startup) while it is
    still installed. Registry keys are removed directly (rather than via the app) so the rest of
    uninstall works even if the executable is already gone.

.PARAMETER PurgeData
    Also delete %APPDATA%\Loadout and %APPDATA%\StartupProfiles (your profiles, config, and history).

.EXAMPLE
    .\install\uninstall.ps1
.EXAMPLE
    .\install\uninstall.ps1 -PurgeData
#>
[CmdletBinding()]
param(
    [switch] $PurgeData
)

$ErrorActionPreference = 'Stop'

$InstallDir = Join-Path $env:LOCALAPPDATA 'Programs\Loadout'
$ExePath    = Join-Path $InstallDir 'Loadout.exe'
$OldInstallDir = Join-Path $env:LOCALAPPDATA 'Programs\StartupProfiles'
$StartMenu  = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$Shortcuts  = @((Join-Path $StartMenu 'Loadout.lnk'), (Join-Path $StartMenu 'Startup Profiles.lnk'))
$SkillDirs  = @((Join-Path $env:USERPROFILE '.claude\skills\loadout'), (Join-Path $env:USERPROFILE '.claude\skills\startup-profiles'))
$DataDirs   = @((Join-Path $env:APPDATA 'Loadout'), (Join-Path $env:APPDATA 'StartupProfiles'))
# The record lives in the new data folder; before the first start under the new name it is still in the old one.
$TakeoverRecord = @($DataDirs | ForEach-Object { Join-Path $_ 'startup-takeover.json' } | Where-Object { Test-Path $_ }) | Select-Object -First 1
$RunKey     = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$ApprovedRunKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run'
$ProtocolKeys = @('HKCU:\Software\Classes\loadout', 'HKCU:\Software\Classes\startupprofiles')
$RunValueNames = @('Loadout', 'StartupProfiles')

function Write-Step($message) { Write-Host "==> $message" -ForegroundColor Cyan }

function Restore-StartupApps {
    if (-not $TakeoverRecord) { return }
    Write-Step "Switching taken-over startup apps back on"

    if (Test-Path $ExePath) {
        $report = [System.IO.Path]::GetTempFileName()
        try {
            $proc = Start-Process -FilePath $ExePath -ArgumentList '--restore-startup' -Wait -PassThru -NoNewWindow `
                -RedirectStandardOutput $report
            Get-Content $report | ForEach-Object { Write-Host "    $_" }
            if ($proc.ExitCode -eq 0 -and -not (Test-Path $TakeoverRecord)) { return }
        }
        catch {
            Write-Warning "Loadout.exe --restore-startup failed: $_"
        }
        finally {
            Remove-Item $report -ErrorAction SilentlyContinue
        }
    }

    $names = (Get-Content $TakeoverRecord -Raw | ConvertFrom-Json | ForEach-Object { $_.Name }) -join ', '
    Write-Warning "Could not switch these startup apps back on; re-enable them in Task Manager > Startup apps: $names"
}

Write-Host "Uninstalling Loadout..." -ForegroundColor Green

Get-Process -Name 'Loadout', 'StartupProfiles' -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Step "Stopping running instance (pid $($_.Id))"
    $_ | Stop-Process -Force
}
Start-Sleep -Milliseconds 400

Restore-StartupApps

Write-Step "Removing login registration"
foreach ($name in $RunValueNames) {
    if (Get-ItemProperty -Path $RunKey -Name $name -ErrorAction SilentlyContinue) {
        Remove-ItemProperty -Path $RunKey -Name $name
    }
    if (Get-ItemProperty -Path $ApprovedRunKey -Name $name -ErrorAction SilentlyContinue) {
        Remove-ItemProperty -Path $ApprovedRunKey -Name $name
    }
}

Write-Step "Removing loadout:// and startupprofiles:// protocols"
foreach ($key in $ProtocolKeys) { if (Test-Path $key) { Remove-Item -Path $key -Recurse -Force } }

Write-Step "Removing Start Menu shortcut"
foreach ($shortcut in $Shortcuts) { if (Test-Path $shortcut) { Remove-Item -Path $shortcut -Force } }

Write-Step "Removing agent skill"
foreach ($dir in $SkillDirs) { if (Test-Path $dir) { Remove-Item -Path $dir -Recurse -Force } }

Write-Step "Removing installed program"
foreach ($dir in @($InstallDir, $OldInstallDir)) { if (Test-Path $dir) { Remove-Item -Path $dir -Recurse -Force } }

if ($PurgeData) {
    foreach ($dataDir in $DataDirs) {
        Write-Step "Purging data at $dataDir"
        if (Test-Path $dataDir) { Remove-Item -Path $dataDir -Recurse -Force }
    }
}

Write-Host ""
Write-Host "Uninstalled." -ForegroundColor Green
if (-not $PurgeData) { Write-Host "Your profiles remain in %APPDATA%\Loadout (use -PurgeData to remove them)." }
