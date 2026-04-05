[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [switch]$NoRestore,
    [switch]$SkipZip
)

$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$publishDir = Join-Path $repoRoot (Join-Path "artifacts\publish\NotepadLite" $RuntimeIdentifier)
$installerOutputDir = Join-Path $repoRoot "artifacts\installer"
$packageRoot = Join-Path $installerOutputDir "NotepadLite"
$packageAppDirectory = Join-Path $packageRoot "App"
$installerTemplateDirectory = Join-Path $repoRoot "installer"
$appProject = Join-Path $repoRoot "src\NotepadLite.App\NotepadLite.App.csproj"
$publishDirForMsBuild = ([System.IO.Path]::GetFullPath($publishDir)).TrimEnd('\\') + "\\"

if (Test-Path $publishDir)
{
    Remove-Item -Path (Join-Path $publishDir "*") -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path $installerOutputDir -Force | Out-Null

if (Test-Path $packageRoot)
{
    Remove-Item -Path $packageRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $packageAppDirectory -Force | Out-Null

$restoreArgument = if ($NoRestore) { "--no-restore" } else { "" }

Write-Host "Publishing NotepadLite to $publishDir"
$publishArguments = @(
    "publish",
    $appProject,
    "-c", $Configuration,
    "-r", $RuntimeIdentifier,
    "--self-contained", "true",
    "-p:PublishSingleFile=false",
    "-p:PublishDir=$publishDirForMsBuild"
)

if ($restoreArgument)
{
    $publishArguments += $restoreArgument
}

dotnet @publishArguments

if ($LASTEXITCODE -ne 0)
{
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Copy-Item -Path (Join-Path $publishDir "*") -Destination $packageAppDirectory -Recurse -Force
Copy-Item -Path (Join-Path $installerTemplateDirectory "Install-NotepadLite.cmd") -Destination $packageRoot -Force
Copy-Item -Path (Join-Path $installerTemplateDirectory "Install-NotepadLite.ps1") -Destination $packageRoot -Force
Copy-Item -Path (Join-Path $installerTemplateDirectory "Uninstall-NotepadLite.ps1") -Destination $packageRoot -Force

if (-not $SkipZip)
{
    $zipPath = Join-Path $installerOutputDir "NotepadLite-Installer.zip"

    if (Test-Path $zipPath)
    {
        Remove-Item -Path $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $packageRoot "*") -DestinationPath $zipPath
    Write-Host "Installer package archive created at $zipPath"
}

Write-Host "Installer package created in $packageRoot"