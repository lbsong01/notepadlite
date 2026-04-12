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

function Start-ElevatedScript
{
    if (Test-Administrator)
    {
        return $true
    }

    $argumentList = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", ('"{0}"' -f $PSCommandPath),
        "-InstallDirectory", ('"{0}"' -f $InstallDirectory)
    )

    Write-Host "Requesting administrator approval..."
    Start-Process -FilePath "powershell.exe" -Verb RunAs -ArgumentList $argumentList | Out-Null
    return $false
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

function Set-ClassesStarContextMenuRegistration
{
    param(
        [Parameter(Mandatory = $true)]
        [Microsoft.Win32.RegistryHive]$Hive,

        [Parameter(Mandatory = $true)]
        [string]$MenuText,

        [Parameter(Mandatory = $true)]
        [string]$IconPath,

        [Parameter(Mandatory = $true)]
        [string]$CommandText
    )

    $baseKey = [Microsoft.Win32.RegistryKey]::OpenBaseKey($Hive, [Microsoft.Win32.RegistryView]::Registry64)
    $menuKey = $baseKey.CreateSubKey("Software\\Classes\\*\\shell\\OpenWithNotepadLite")
    $commandKey = $menuKey.CreateSubKey("command")

    try
    {
        $menuKey.SetValue($null, $MenuText, [Microsoft.Win32.RegistryValueKind]::String)
        $menuKey.SetValue("Icon", $IconPath, [Microsoft.Win32.RegistryValueKind]::String)
        $commandKey.SetValue($null, $CommandText, [Microsoft.Win32.RegistryValueKind]::String)
    }
    finally
    {
        if ($commandKey)
        {
            $commandKey.Dispose()
        }

        if ($menuKey)
        {
            $menuKey.Dispose()
        }

        $baseKey.Dispose()
    }
}

function Remove-ClassesStarContextMenuRegistration
{
    param(
        [Parameter(Mandatory = $true)]
        [Microsoft.Win32.RegistryHive]$Hive
    )

    $baseKey = [Microsoft.Win32.RegistryKey]::OpenBaseKey($Hive, [Microsoft.Win32.RegistryView]::Registry64)

    try
    {
        $baseKey.DeleteSubKeyTree("Software\\Classes\\*\\shell\\OpenWithNotepadLite", $false)
    }
    finally
    {
        $baseKey.Dispose()
    }
}

function Set-UninstallRegistration
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$KeyName,

        [Parameter(Mandatory = $true)]
        [string]$DisplayName,

        [Parameter(Mandatory = $true)]
        [string]$DisplayVersion,

        [Parameter(Mandatory = $true)]
        [string]$Publisher,

        [Parameter(Mandatory = $true)]
        [string]$InstallLocation,

        [Parameter(Mandatory = $true)]
        [string]$DisplayIcon,

        [Parameter(Mandatory = $true)]
        [string]$UninstallString,

        [Parameter(Mandatory = $true)]
        [string]$QuietUninstallString
    )

    $baseKey = [Microsoft.Win32.RegistryKey]::OpenBaseKey(
        [Microsoft.Win32.RegistryHive]::LocalMachine,
        [Microsoft.Win32.RegistryView]::Registry64)
    $subKey = $baseKey.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\$KeyName")

    try
    {
        $subKey.SetValue("DisplayName", $DisplayName, [Microsoft.Win32.RegistryValueKind]::String)
        $subKey.SetValue("DisplayVersion", $DisplayVersion, [Microsoft.Win32.RegistryValueKind]::String)
        $subKey.SetValue("Publisher", $Publisher, [Microsoft.Win32.RegistryValueKind]::String)
        $subKey.SetValue("InstallLocation", $InstallLocation, [Microsoft.Win32.RegistryValueKind]::String)
        $subKey.SetValue("DisplayIcon", $DisplayIcon, [Microsoft.Win32.RegistryValueKind]::String)
        $subKey.SetValue("UninstallString", $UninstallString, [Microsoft.Win32.RegistryValueKind]::String)
        $subKey.SetValue("QuietUninstallString", $QuietUninstallString, [Microsoft.Win32.RegistryValueKind]::String)
        $subKey.SetValue("NoModify", 1, [Microsoft.Win32.RegistryValueKind]::DWord)
        $subKey.SetValue("NoRepair", 1, [Microsoft.Win32.RegistryValueKind]::DWord)
    }
    finally
    {
        if ($subKey)
        {
            $subKey.Dispose()
        }

        $baseKey.Dispose()
    }
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

if (-not (Start-ElevatedScript))
{
    return
}

$packageRoot = $PSScriptRoot
$payloadDirectory = Join-Path $packageRoot "App"
$mainExecutable = Join-Path $payloadDirectory "NotepadLite.App.exe"
$installedExecutable = Join-Path $InstallDirectory "NotepadLite.App.exe"
$startMenuShortcut = Join-Path $env:ProgramData "Microsoft\Windows\Start Menu\Programs\NotepadLite.lnk"
$uninstallScriptSource = Join-Path $packageRoot "Uninstall-NotepadLite.ps1"
$uninstallScriptTarget = Join-Path $InstallDirectory "Uninstall-NotepadLite.ps1"
$uninstallKey = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\NotepadLite"

if (-not (Test-Path $mainExecutable))
{
    throw "Installer payload is missing. Expected to find $mainExecutable."
}

$runningProcess = Get-Process -Name "NotepadLite.App" -ErrorAction SilentlyContinue
if ($runningProcess)
{
    throw "Close NotepadLite before installing an update."
}

Write-Host "Installing NotepadLite to $InstallDirectory"

if (Test-Path $InstallDirectory)
{
    Write-Host "Removing previous installation files..."
    Get-ChildItem -Path $InstallDirectory -Force | Remove-Item -Recurse -Force
}
else
{
    New-Item -ItemType Directory -Path $InstallDirectory -Force | Out-Null
}

Write-Host "Copying application files..."
Copy-Item -Path (Join-Path $payloadDirectory "*") -Destination $InstallDirectory -Recurse -Force
Copy-Item -Path $uninstallScriptSource -Destination $uninstallScriptTarget -Force

$displayVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($installedExecutable).ProductVersion

Remove-ClassesStarContextMenuRegistration -Hive CurrentUser
Set-ClassesStarContextMenuRegistration `
    -Hive LocalMachine `
    -MenuText "Open with NotepadLite" `
    -IconPath $installedExecutable `
    -CommandText ('"{0}" "%1"' -f $installedExecutable)

Write-Host "Registering Windows uninstall entry..."
$uninstallCommand = 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "{0}"' -f $uninstallScriptTarget
Set-UninstallRegistration `
    -KeyName "NotepadLite" `
    -DisplayName "NotepadLite" `
    -DisplayVersion $displayVersion `
    -Publisher "NotepadLite" `
    -InstallLocation $InstallDirectory `
    -DisplayIcon $installedExecutable `
    -UninstallString $uninstallCommand `
    -QuietUninstallString $uninstallCommand

New-Shortcut -ShortcutPath $startMenuShortcut -TargetPath $installedExecutable -Description "Launch NotepadLite"

Write-Host "NotepadLite installed to $InstallDirectory"
Write-Host "The Windows Explorer context menu entry 'Open with NotepadLite' is now available for files."