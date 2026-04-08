using System.Text.Json.Serialization;

namespace NotepadLite.Core;

/// <summary>
/// Serializable snapshot of all open tabs, used to persist editor state across sessions.
/// </summary>
public sealed class SessionState
{
    /// <summary>
    /// Gets or sets the collection of tab snapshots in display order.
    /// </summary>
    [JsonPropertyName("tabs")]
    public List<SessionTab> Tabs { get; set; } = [];

    /// <summary>
    /// Gets or sets the identifier of the tab that was active when the session was saved.
    /// </summary>
    [JsonPropertyName("activeTabId")]
    public Guid? ActiveTabId { get; set; }
}

/// <summary>
/// Serializable snapshot of a single tab.
/// </summary>
public sealed class SessionTab
{
    /// <summary>
    /// Gets or sets the stable tab identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the file path, or <see langword="null"/> for unsaved documents.
    /// </summary>
    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }

    /// <summary>
    /// Gets or sets the document text at the time the session was saved.
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the document had unsaved changes.
    /// </summary>
    [JsonPropertyName("isDirty")]
    public bool IsDirty { get; set; }

    /// <summary>
    /// Gets or sets the language definition name applied to this tab.
    /// </summary>
    [JsonPropertyName("languageName")]
    public string? LanguageName { get; set; }
}
