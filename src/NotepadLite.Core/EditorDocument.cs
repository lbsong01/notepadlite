namespace NotepadLite.Core;

/// <summary>
/// Represents the editable text document shown in the application.
/// </summary>
public sealed record EditorDocument
{
    private EditorDocument(string? filePath, string text, bool isDirty)
    {
        FilePath = filePath;
        Text = text;
        IsDirty = isDirty;
    }

    /// <summary>
    /// Gets the current file path when the document is backed by a file.
    /// </summary>
    public string? FilePath { get; }

    /// <summary>
    /// Gets the current document text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets a value indicating whether the document has unsaved changes.
    /// </summary>
    public bool IsDirty { get; }

    /// <summary>
    /// Gets the display name used by the window title.
    /// </summary>
    public string DisplayName => FilePath is null ? "Untitled" : Path.GetFileName(FilePath);

    /// <summary>
    /// Creates a new empty document.
    /// </summary>
    public static EditorDocument CreateEmpty()
    {
        return new EditorDocument(filePath: null, text: string.Empty, isDirty: false);
    }

    /// <summary>
    /// Creates a document loaded from disk.
    /// </summary>
    public static EditorDocument FromFile(string filePath, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(text);
        return new EditorDocument(filePath, text, isDirty: false);
    }

    /// <summary>
    /// Returns a new document state with updated text.
    /// </summary>
    public EditorDocument WithText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text == Text ? this : new EditorDocument(FilePath, text, isDirty: true);
    }

    /// <summary>
    /// Returns a saved document state.
    /// </summary>
    public EditorDocument MarkSaved(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return new EditorDocument(filePath, Text, isDirty: false);
    }

    /// <summary>
    /// Returns a sensible default file name when prompting to save.
    /// </summary>
    public string GetSuggestedFileName()
    {
        return FilePath is null ? "Untitled.txt" : Path.GetFileName(FilePath);
    }
}