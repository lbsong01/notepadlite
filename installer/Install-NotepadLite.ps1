[CmdletBinding()]
param(
    [string]$InstallDirectory = "$env:ProgramFiles\NotepadLite"
)

$ErrorActionPreference = "Stop"

function Test-Administrator
{
    $currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($currentIdentity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Ensure-Administrator
{
    if (Test-Administrator)
    {
        return
    }

    $argumentList = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", ('"{0}"' -f $PSCommandPath),
        "-InstallDirectory", ('"{0}"' -f $InstallDirectory)
    )

    Start-Process -FilePath "powershell.exe" -Verb RunAs -ArgumentList $argumentList | Out-Null
    exit
}

function New-Shortcut
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$ShortcutPath,

        [Parameter(Mandatory = $true)]
        [string]$TargetPath,

        [string]$Description
    )

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($ShortcutPath)
    $shortcut.TargetPath = $TargetPath
    $shortcut.WorkingDirectory = Split-Path -Path $TargetPath -Parent
    $shortcut.IconLocation = $TargetPath
    $shortcut.Description = $Description
    $shortcut.Save()
}

Ensure-Administrator

$packageRoot = $PSScriptRoot
$payloadDirectory = Join-Path $packageRoot "App"
$mainExecutable = Join-Path $payloadDirectory "NotepadLite.App.exe"
$installedExecutable = Join-Path $InstallDirectory "NotepadLite.App.exe"
$startMenuShortcut = Join-Path $env:ProgramData "Microsoft\Windows\Start Menu\Programs\NotepadLite.lnk"
$uninstallScriptSource = Join-Path $packageRoot "Uninstall-NotepadLite.ps1"
$uninstallScriptTarget = Join-Path $InstallDirectory "Uninstall-NotepadLite.ps1"

if (-not (Test-Path $mainExecutable))
{
    throw "Installer payload is missing. Expected to find $mainExecutable."
}

$runningProcess = Get-Process -Name "NotepadLite.App" -ErrorAction SilentlyContinue
if ($runningProcess)
{
    throw "Close NotepadLite before installing an update."
}

if (Test-Path $InstallDirectory)
{
    Get-ChildItem -Path $InstallDirectory -Force | Remove-Item -Recurse -Force
}
else
{
    New-Item -ItemType Directory -Path $InstallDirectory -Force | Out-Null
}

Copy-Item -Path (Join-Path $payloadDirectory "*") -Destination $InstallDirectory -Recurse -Force
Copy-Item -Path $uninstallScriptSource -Destination $uninstallScriptTarget -Force

$contextMenuKey = "HKLM:\Software\Classes\*\shell\OpenWithNotepadLite"
$commandKey = Join-Path $contextMenuKey "command"
$uninstallKey = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\NotepadLite"
$contextMenuRegistryPath = 'HKLM\Software\Classes\*\shell\OpenWithNotepadLite'
$commandRegistryPath = 'HKLM\Software\Classes\*\shell\OpenWithNotepadLite\command'
$uninstallRegistryPath = 'HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall\NotepadLite'
$displayVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($installedExecutable).ProductVersion

& reg.exe add $contextMenuRegistryPath /ve /d "Open with NotepadLite" /f | Out-Null
& reg.exe add $contextMenuRegistryPath /v Icon /d $installedExecutable /f | Out-Null
& reg.exe add $commandRegistryPath /ve /d ('"{0}" "%1"' -f $installedExecutable) /f | Out-Null

& reg.exe add $uninstallRegistryPath /v DisplayName /d "NotepadLite" /f | Out-Null
& reg.exe add $uninstallRegistryPath /v DisplayVersion /d $displayVersion /f | Out-Null
& reg.exe add $uninstallRegistryPath /v Publisher /d "NotepadLite" /f | Out-Null
& reg.exe add $uninstallRegistryPath /v InstallLocation /d $InstallDirectory /f | Out-Null
& reg.exe add $uninstallRegistryPath /v DisplayIcon /d $installedExecutable /f | Out-Null
& reg.exe add $uninstallRegistryPath /v NoModify /t REG_DWORD /d 1 /f | Out-Null
& reg.exe add $uninstallRegistryPath /v NoRepair /t REG_DWORD /d 1 /f | Out-Null
& reg.exe add $uninstallRegistryPath /v UninstallString /d ('powershell.exe -NoProfile -ExecutionPolicy Bypass -File "{0}"' -f $uninstallScriptTarget) /f | Out-Null
& reg.exe add $uninstallRegistryPath /v QuietUninstallString /d ('powershell.exe -NoProfile -ExecutionPolicy Bypass -File "{0}"' -f $uninstallScriptTarget) /f | Out-Null

New-Shortcut -ShortcutPath $startMenuShortcut -TargetPath $installedExecutable -Description "Launch NotepadLite"

Write-Host "NotepadLite installed to $InstallDirectory"
Write-Host "The Windows Explorer context menu entry 'Open with NotepadLite' is now available for files."