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

Windows Explorer can launch the editor from a file right-click menu once the app accepts a file path argument. After publishing or installing the app to a stable path, add a registry entry that passes the selected file path as `%1`.

Example `.reg` content:

```reg
Windows Registry Editor Version 5.00

[HKEY_CURRENT_USER\Software\Classes\*\shell\OpenWithNotepadLite]
@="Open with NotepadLite"

[HKEY_CURRENT_USER\Software\Classes\*\shell\OpenWithNotepadLite\command]
@="\"C:\\Apps\\NotepadLite\\NotepadLite.App.exe\" \"%1\""
```

Notes:

- Replace the executable path with the actual published app path.
- Use `HKEY_CURRENT_USER` for a per-user context menu without admin rights.
- For a standard `Open with` experience, you can also register file associations through an installer, but the command above is the minimal shell-integration path.
