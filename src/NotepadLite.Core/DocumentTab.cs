namespace NotepadLite.Core;

/// <summary>
/// Represents a single open tab in the editor, pairing a unique identity with a document and optional language.
/// </summary>
public sealed record DocumentTab
{
    private DocumentTab(Guid id, EditorDocument document, string? languageName)
    {
        Id = id;
        Document = document;
        LanguageName = languageName;
    }

    /// <summary>
    /// Gets the stable identifier for this tab, used for session persistence.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the document state held by this tab.
    /// </summary>
    public EditorDocument Document { get; }

    /// <summary>
    /// Gets the name of the language definition applied to this tab, or <see langword="null"/> for auto-detect.
    /// </summary>
    public string? LanguageName { get; }

    /// <summary>
    /// Creates a new tab with a fresh empty document.
    /// </summary>
    public static DocumentTab CreateEmpty()
    {
        return new DocumentTab(Guid.NewGuid(), EditorDocument.CreateEmpty(), languageName: null);
    }

    /// <summary>
    /// Creates a tab for an existing document.
    /// </summary>
    public static DocumentTab FromDocument(EditorDocument document, string? languageName = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new DocumentTab(Guid.NewGuid(), document, languageName);
    }

    /// <summary>
    /// Returns a new tab state with the specified document.
    /// </summary>
    public DocumentTab WithDocument(EditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document == Document ? this : new DocumentTab(Id, document, LanguageName);
    }

    /// <summary>
    /// Returns a new tab state with the specified language name.
    /// </summary>
    public DocumentTab WithLanguage(string? languageName)
    {
        return string.Equals(languageName, LanguageName, StringComparison.OrdinalIgnoreCase)
            ? this
            : new DocumentTab(Id, Document, languageName);
    }

    /// <summary>
    /// Restores a tab from persisted session data.
    /// </summary>
    internal static DocumentTab Restore(Guid id, EditorDocument document, string? languageName)
    {
        return new DocumentTab(id, document, languageName);
    }
}
