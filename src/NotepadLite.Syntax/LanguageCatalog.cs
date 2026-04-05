namespace NotepadLite.Syntax;

/// <summary>
/// Loads language definitions from a directory on disk.
/// </summary>
public static class LanguageCatalog
{
    /// <summary>
    /// Loads all XML language definitions from the supplied directory.
    /// </summary>
    public static LanguageCatalogLoadResult LoadFromDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return new LanguageCatalogLoadResult([], []);
        }

        var definitions = new List<LanguageDefinition>();
        var diagnostics = new List<string>();

        foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*.xml", SearchOption.TopDirectoryOnly))
        {
            var importResult = UserDefinedLanguageImporter.Import(filePath);
            if (importResult.Definition is not null)
            {
                definitions.Add(importResult.Definition with { SourcePath = filePath });
            }

            diagnostics.AddRange(importResult.Diagnostics.Select(message => $"{Path.GetFileName(filePath)}: {message}"));
        }

        return new LanguageCatalogLoadResult(definitions, diagnostics);
    }
}