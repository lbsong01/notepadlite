namespace NotepadLite.Syntax;

/// <summary>
/// Represents the results of loading a set of language definitions.
/// </summary>
public sealed record LanguageCatalogLoadResult(
    IReadOnlyList<LanguageDefinition> Definitions,
    IReadOnlyList<string> Diagnostics);