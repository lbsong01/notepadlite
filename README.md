# NotepadLite

NotepadLite is a lightweight Windows desktop text editor written in C#. The current implementation starts with a fast WPF shell, a UI-independent document core, and a syntax-definition pipeline that loads a practical subset of Notepad++ user-defined language XML files from disk.

## Current Scope

- WPF desktop shell targeting `net10.0-windows`
- Open, edit, save, and save-as workflows
- AvalonEdit-based text surface with line numbers
- Runtime language-definition loading from built-in and user folders
- Import of a documented subset of Notepad++ UDL XML

## Project Layout

- `src/NotepadLite.App` contains the WPF shell and editor integration.
- `src/NotepadLite.Core` contains document and file services.
- `src/NotepadLite.Syntax` contains the language-definition model and UDL importer.
- `tests/NotepadLite.Syntax.Tests` contains automated tests for the importer.
- `assets/languages` contains built-in sample language definitions copied into the app output.

## Build And Run

```powershell
dotnet restore NotepadLite.slnx
dotnet build NotepadLite.slnx
dotnet run --project src/NotepadLite.App/NotepadLite.App.csproj
```

To open a file directly at startup:

```powershell
dotnet run --project src/NotepadLite.App/NotepadLite.App.csproj -- "C:\path\to\file.json"
```

## Test

```powershell
dotnet test NotepadLite.slnx
```

## Build Installer

The repository includes a self-contained Windows installer package. Running the packaged installer copies the app to `C:\Program Files\NotepadLite`, registers an uninstall entry, creates a Start menu shortcut, and adds a Windows Explorer right-click command named `Open with NotepadLite` for all files.

Build the installer package with:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
```

Outputs:

- Published application files: `artifacts\publish\NotepadLite\win-x64`
- Installer package folder: `artifacts\installer\NotepadLite`
- Installer archive: `artifacts\installer\NotepadLite-Installer.zip`

To install on a machine, open `artifacts\installer\NotepadLite` and run `Install-NotepadLite.cmd`. The installer will request elevation because it installs machine-wide.

## Adding Language Definitions

Built-in language definitions are copied from `assets/languages` into the app output under `Assets/Languages`.

User-added definitions can be dropped into:

```text
%LocalAppData%\NotepadLite\Languages
```

The current importer supports:

- `name` and `ext` on `UserLang`
- keyword groups under `Keywords`
- operators from a `Keywords` entry named `Operators`
- line comments and block comments from `Comments`
- symmetric string delimiters from `Delimiter` elements

The current importer does not attempt full Notepad++ compatibility. Unsupported constructs are reported in the diagnostics area instead of being silently approximated.

## Windows Context Menu

Windows Explorer can launch the editor from a file right-click menu once the app accepts a file path argument. The installer package now creates this integration automatically.

If you need a manual fallback after copying the app to a stable path, use `scripts/notepadlite-context-menu.reg`, which points to the default installer location.

Example `.reg` content:

```reg
Windows Registry Editor Version 5.00

[HKEY_CURRENT_USER\Software\Classes\*\shell\OpenWithNotepadLite]
@="Open with NotepadLite"
"Icon"="C:\\Program Files\\NotepadLite\\NotepadLite.App.exe"

[HKEY_CURRENT_USER\Software\Classes\*\shell\OpenWithNotepadLite\command]
@="\"C:\\Program Files\\NotepadLite\\NotepadLite.App.exe\" \"%1\""
```

Notes:

- Replace the executable path if you install the app elsewhere.
- The installer package uses a machine-wide registration under `HKEY_LOCAL_MACHINE`, while the `.reg` file uses `HKEY_CURRENT_USER` as a no-installer fallback.
