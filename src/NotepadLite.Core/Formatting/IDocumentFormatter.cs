namespace NotepadLite.Core.Formatting;

/// <summary>
/// Strategy interface for pretty-printing the contents of an editor document.
/// </summary>
public interface IDocumentFormatter
{
    /// <summary>
    /// Returns <see langword="true"/> when this formatter handles the supplied language identity.
    /// </summary>
    /// <param name="languageName">The language name from the active tab, or <see langword="null"/>.</param>
    /// <param name="extension">The file extension including the leading dot (for example <c>.json</c>), or <see langword="null"/>.</param>
    bool CanFormat(string? languageName, string? extension);

    /// <summary>
    /// Formats the supplied text. Implementations must never throw for malformed input;
    /// instead they should return a failure result that preserves the original text.
    /// </summary>
    FormatResult Format(string text);
}
