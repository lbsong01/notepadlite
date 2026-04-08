using System.Text.Json;

namespace NotepadLite.Core;

/// <summary>
/// Persists and restores editor session state (open tabs) as JSON.
/// </summary>
public sealed class SessionService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Saves the current session state to disk.
    /// </summary>
    /// <param name="sessionFilePath">The full path of the session JSON file.</param>
    /// <param name="tabs">The currently open tabs in display order.</param>
    /// <param name="activeTabId">The identifier of the active tab, if any.</param>
    public void Save(string sessionFilePath, IReadOnlyList<DocumentTab> tabs, Guid? activeTabId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionFilePath);
        ArgumentNullException.ThrowIfNull(tabs);

        var state = new SessionState
        {
            ActiveTabId = activeTabId,
            Tabs = tabs.Select((tab, index) => new SessionTab
            {
                Id = tab.Id,
                FilePath = tab.Document.FilePath,
                Text = tab.Document.Text,
                IsDirty = tab.Document.IsDirty,
                LanguageName = tab.LanguageName,
            }).ToList(),
        };

        var directory = Path.GetDirectoryName(sessionFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(state, SerializerOptions);
        File.WriteAllText(sessionFilePath, json);
    }

    /// <summary>
    /// Loads a previously saved session state from disk.
    /// </summary>
    /// <param name="sessionFilePath">The full path of the session JSON file.</param>
    /// <returns>
    /// A tuple containing the restored tabs and the previously active tab identifier.
    /// Returns an empty list when no session file exists or deserialization fails.
    /// </returns>
    public (IReadOnlyList<DocumentTab> Tabs, Guid? ActiveTabId) Load(string sessionFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionFilePath);

        if (!File.Exists(sessionFilePath))
        {
            return ([], null);
        }

        try
        {
            var json = File.ReadAllText(sessionFilePath);
            var state = JsonSerializer.Deserialize<SessionState>(json, SerializerOptions);

            if (state is null || state.Tabs.Count == 0)
            {
                return ([], null);
            }

            var tabs = new List<DocumentTab>(state.Tabs.Count);
            foreach (var sessionTab in state.Tabs)
            {
                var document = sessionTab.FilePath is not null && !sessionTab.IsDirty && File.Exists(sessionTab.FilePath)
                    ? EditorDocument.FromFile(sessionTab.FilePath, File.ReadAllText(sessionTab.FilePath))
                    : CreateDocumentFromSessionTab(sessionTab);

                tabs.Add(DocumentTab.Restore(sessionTab.Id, document, sessionTab.LanguageName));
            }

            return (tabs, state.ActiveTabId);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return ([], null);
        }
    }

    /// <summary>
    /// Re-creates a document from the inline session text when the file is missing or the tab was dirty.
    /// </summary>
    private static EditorDocument CreateDocumentFromSessionTab(SessionTab sessionTab)
    {
        if (sessionTab.FilePath is not null)
        {
            // File-backed document with unsaved edits: restore as dirty.
            var saved = EditorDocument.FromFile(sessionTab.FilePath, string.Empty);
            return saved.WithText(sessionTab.Text);
        }

        // Untitled document: restore content and mark as dirty so the user knows it's unsaved.
        var empty = EditorDocument.CreateEmpty();
        return string.IsNullOrEmpty(sessionTab.Text) ? empty : empty.WithText(sessionTab.Text);
    }
}
