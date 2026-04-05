namespace NotepadLite.Syntax;

/// <summary>
/// Represents the normalized syntax-definition model used by the application.
/// </summary>
public sealed record LanguageDefinition(
    string Name,
    IReadOnlyList<string> Extensions,
    IReadOnlyList<KeywordGroup> KeywordGroups,
    IReadOnlyList<string> Operators,
    IReadOnlyList<string> LineComments,
    IReadOnlyList<BlockCommentDefinition> BlockComments,
    IReadOnlyList<string> StringDelimiters,
    string? SourcePath = null)
{
    /// <summary>
    /// Creates a plain-text fallback language definition.
    /// </summary>
    public static LanguageDefinition CreatePlainText()
    {
        return new LanguageDefinition(
            Name: "Plain Text",
            Extensions: [],
            KeywordGroups: [],
            Operators: [],
            LineComments: [],
            BlockComments: [],
            StringDelimiters: []);
    }

    /// <summary>
    /// Returns whether the language supports the supplied file extension.
    /// </summary>
    public bool SupportsExtension(string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);

        return Extensions.Any(candidate => string.Equals(NormalizeExtension(candidate), NormalizeExtension(extension), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Normalizes a file extension value for matching.
    /// </summary>
    private static string NormalizeExtension(string extension)
    {
        return extension.StartsWith('.') ? extension : $".{extension}";
    }
}