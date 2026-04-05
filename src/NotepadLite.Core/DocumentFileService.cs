using System.Text;

namespace NotepadLite.Core;

/// <summary>
/// Provides file-system operations for editor documents.
/// </summary>
public sealed class DocumentFileService
{
    /// <summary>
    /// Loads a document from disk.
    /// </summary>
    public EditorDocument Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var text = File.ReadAllText(filePath, Encoding.UTF8);
        return EditorDocument.FromFile(filePath, text);
    }

    /// <summary>
    /// Saves a document to its existing path.
    /// </summary>
    public EditorDocument Save(EditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.FilePath is null)
        {
            throw new InvalidOperationException("A file path is required to save the document.");
        }

        return Save(document, document.FilePath);
    }

    /// <summary>
    /// Saves a document to a specific path.
    /// </summary>
    public EditorDocument Save(EditorDocument document, string filePath)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(filePath, document.Text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return document.MarkSaved(filePath);
    }
}