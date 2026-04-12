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

if (-not (Start-ElevatedScript))
{
    return
}

$installedExecutable = Join-Path $InstallDirectory "NotepadLite.App.exe"
$startMenuShortcut = Join-Path $env:ProgramData "Microsoft\Windows\Start Menu\Programs\NotepadLite.lnk"
$uninstallKey = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\NotepadLite"

$runningProcess = Get-Process -Name "NotepadLite.App" -ErrorAction SilentlyContinue
if ($runningProcess)
{
    throw "Close NotepadLite before uninstalling it."
}

Write-Host "Removing NotepadLite integration..."

Remove-ClassesStarContextMenuRegistration -Hive LocalMachine
Remove-ClassesStarContextMenuRegistration -Hive CurrentUser

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
    Write-Host "Scheduling removal of $InstallDirectory after this process exits..."
    $cleanupCommand = 'ping 127.0.0.1 -n 3 > nul & rmdir /s /q "{0}"' -f $InstallDirectory
    Start-Process -FilePath "cmd.exe" -WindowStyle Hidden -ArgumentList '/c', $cleanupCommand | Out-Null
}

Write-Host "NotepadLite uninstall cleanup has been scheduled."