namespace NotepadLite.Syntax;

/// <summary>
/// Represents the result of importing a language definition file.
/// </summary>
public sealed record LanguageDefinitionImportResult(
    LanguageDefinition? Definition,
    IReadOnlyList<string> Diagnostics);