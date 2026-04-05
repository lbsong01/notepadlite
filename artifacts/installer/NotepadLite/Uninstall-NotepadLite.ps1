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

Ensure-Administrator

$installedExecutable = Join-Path $InstallDirectory "NotepadLite.App.exe"
$startMenuShortcut = Join-Path $env:ProgramData "Microsoft\Windows\Start Menu\Programs\NotepadLite.lnk"
$contextMenuKey = "HKLM:\Software\Classes\*\shell\OpenWithNotepadLite"
$uninstallKey = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\NotepadLite"

$runningProcess = Get-Process -Name "NotepadLite.App" -ErrorAction SilentlyContinue
if ($runningProcess)
{
    throw "Close NotepadLite before uninstalling it."
}

if (Test-Path $contextMenuKey)
{
    Remove-Item -Path $contextMenuKey -Recurse -Force
}

if (Test-Path $uninstallKey)
{
    Remove-Item -Path $uninstallKey -Recurse -Force
}

if (Test-Path $startMenuShortcut)
{
    Remove-Item -Path $startMenuShortcut -Force
}

if (Test-Path $installedExecutable)
{
    $cleanupCommand = 'ping 127.0.0.1 -n 3 > nul & rmdir /s /q "{0}"' -f $InstallDirectory
    Start-Process -FilePath "cmd.exe" -WindowStyle Hidden -ArgumentList '/c', $cleanupCommand | Out-Null
}

Write-Host "NotepadLite uninstall cleanup has been scheduled."