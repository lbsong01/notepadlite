namespace NotepadLite.App;

/// <summary>
/// Represents a language definition entry displayed in the UI.
/// </summary>
internal sealed class LanguageCatalogEntryViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LanguageCatalogEntryViewModel"/> class.
    /// </summary>
    internal LanguageCatalogEntryViewModel(string name, string? sourcePath, string sourceKind)
    {
        Name = name;
        SourcePath = sourcePath;
        SourceKind = sourceKind;
    }

    /// <summary>
    /// Gets the language name.
    /// </summary>
    internal string Name { get; }

    /// <summary>
    /// Gets the source file path when available.
    /// </summary>
    internal string? SourcePath { get; }

    /// <summary>
    /// Gets the source classification.
    /// </summary>
    internal string SourceKind { get; }

    /// <summary>
    /// Gets the combined display label used in the definitions list.
    /// </summary>
    internal string DisplayLabel => SourcePath is null
        ? $"{Name} ({SourceKind})"
        : $"{Name} ({SourceKind}) - {SourcePath}";
}