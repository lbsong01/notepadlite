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

function Start-ElevatedInstaller
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

function Set-RegistryDefaultValue
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    New-Item -Path $Path -Force | Out-Null
    Set-Item -Path $Path -Value $Value
}

function Set-RegistryStringValue
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    New-Item -Path $Path -Force | Out-Null
    New-ItemProperty -Path $Path -Name $Name -Value $Value -PropertyType String -Force | Out-Null
}

function Set-RegistryDWordValue
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [int]$Value
    )

    New-Item -Path $Path -Force | Out-Null
    New-ItemProperty -Path $Path -Name $Name -Value $Value -PropertyType DWord -Force | Out-Null
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

Start-ElevatedInstaller

$packageRoot = $PSScriptRoot
$payloadDirectory = Join-Path $packageRoot "App"
$mainExecutable = Join-Path $payloadDirectory "NotepadLite.App.exe"
$installedExecutable = Join-Path $InstallDirectory "NotepadLite.App.exe"
$startMenuShortcut = Join-Path $env:ProgramData "Microsoft\Windows\Start Menu\Programs\NotepadLite.lnk"
$uninstallScriptSource = Join-Path $packageRoot "Uninstall-NotepadLite.ps1"
$uninstallScriptTarget = Join-Path $InstallDirectory "Uninstall-NotepadLite.ps1"
$contextMenuKey = "HKLM:\Software\Classes\*\shell\OpenWithNotepadLite"
$commandKey = Join-Path $contextMenuKey "command"
$uninstallKey = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\NotepadLite"
$perUserContextMenuKey = "HKCU:\Software\Classes\*\shell\OpenWithNotepadLite"
$perUserCommandKey = Join-Path $perUserContextMenuKey "command"

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

$displayVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($installedExecutable).ProductVersion

if (Test-Path $perUserContextMenuKey)
{
    Remove-Item -Path $perUserContextMenuKey -Recurse -Force
}

Set-RegistryDefaultValue -Path $contextMenuKey -Value "Open with NotepadLite"
Set-RegistryStringValue -Path $contextMenuKey -Name "Icon" -Value $installedExecutable
Set-RegistryDefaultValue -Path $commandKey -Value ('"{0}" "%1"' -f $installedExecutable)

Set-RegistryStringValue -Path $uninstallKey -Name "DisplayName" -Value "NotepadLite"
Set-RegistryStringValue -Path $uninstallKey -Name "DisplayVersion" -Value $displayVersion
Set-RegistryStringValue -Path $uninstallKey -Name "Publisher" -Value "NotepadLite"
Set-RegistryStringValue -Path $uninstallKey -Name "InstallLocation" -Value $InstallDirectory
Set-RegistryStringValue -Path $uninstallKey -Name "DisplayIcon" -Value $installedExecutable
Set-RegistryDWordValue -Path $uninstallKey -Name "NoModify" -Value 1
Set-RegistryDWordValue -Path $uninstallKey -Name "NoRepair" -Value 1
Set-RegistryStringValue -Path $uninstallKey -Name "UninstallString" -Value ('powershell.exe -NoProfile -ExecutionPolicy Bypass -File "{0}"' -f $uninstallScriptTarget)
Set-RegistryStringValue -Path $uninstallKey -Name "QuietUninstallString" -Value ('powershell.exe -NoProfile -ExecutionPolicy Bypass -File "{0}"' -f $uninstallScriptTarget)

New-Shortcut -ShortcutPath $startMenuShortcut -TargetPath $installedExecutable -Description "Launch NotepadLite"

Write-Host "NotepadLite installed to $InstallDirectory"
Write-Host "The Windows Explorer context menu entry 'Open with NotepadLite' is now available for files."