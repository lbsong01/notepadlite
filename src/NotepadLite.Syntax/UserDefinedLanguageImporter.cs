using System.Xml.Linq;

namespace NotepadLite.Syntax;

/// <summary>
/// Imports a practical subset of Notepad++ user-defined language XML files.
/// </summary>
public static class UserDefinedLanguageImporter
{
    /// <summary>
    /// Imports a language definition from the supplied XML file.
    /// </summary>
    public static LanguageDefinitionImportResult Import(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var document = XDocument.Load(filePath, LoadOptions.PreserveWhitespace);
        return Import(document, filePath);
    }

    /// <summary>
    /// Imports a language definition from an XML string.
    /// </summary>
    public static LanguageDefinitionImportResult ImportFromXml(string xmlContent, string? sourcePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xmlContent);
        var document = XDocument.Parse(xmlContent, LoadOptions.PreserveWhitespace);
        return Import(document, sourcePath);
    }

    /// <summary>
    /// Imports the supported subset of the UDL document model.
    /// </summary>
    private static LanguageDefinitionImportResult Import(XDocument document, string? sourcePath)
    {
        ArgumentNullException.ThrowIfNull(document);

        var diagnostics = new List<string>();
        var userLanguage = document.Root?.Element("UserLang") ?? document.Root?.Element("UserLanguage");
        if (userLanguage is null)
        {
            return new LanguageDefinitionImportResult(null, ["The XML does not contain a UserLang or UserLanguage element."]);
        }

        var name = (string?)userLanguage.Attribute("name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return new LanguageDefinitionImportResult(null, ["The language definition does not declare a name."]);
        }

        var extensions = ParseExtensions((string?)userLanguage.Attribute("ext"));
        var keywordGroups = ParseKeywordGroups(userLanguage);
        var operators = ParseDelimitedValues(userLanguage.Element("Keywords")?.Elements("Keywords")
            .Where(static element => string.Equals((string?)element.Attribute("name"), "Operators", StringComparison.OrdinalIgnoreCase))
            .Select(static element => element.Value)
            .FirstOrDefault());

        var comments = ParseCommentDefinitions(userLanguage, diagnostics);
        var stringDelimiters = ParseStringDelimiters(userLanguage, diagnostics);

        if (document.Descendants("Folder").Any())
        {
            diagnostics.Add("Folder and code-folding definitions are ignored in the current importer.");
        }

        var definition = new LanguageDefinition(
            Name: name,
            Extensions: extensions,
            KeywordGroups: keywordGroups,
            Operators: operators,
            LineComments: comments.LineComments,
            BlockComments: comments.BlockComments,
            StringDelimiters: stringDelimiters,
            SourcePath: sourcePath);

        return new LanguageDefinitionImportResult(definition, diagnostics);
    }

    /// <summary>
    /// Parses the extension attribute into normalized extension values.
    /// </summary>
    private static IReadOnlyList<string> ParseExtensions(string? extensionValue)
    {
        if (string.IsNullOrWhiteSpace(extensionValue))
        {
            return [];
        }

        return extensionValue
            .Split([' ', ';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static extension => extension.StartsWith('.') ? extension : $".{extension}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Parses supported keyword groups from the UDL keywords section.
    /// </summary>
    private static IReadOnlyList<KeywordGroup> ParseKeywordGroups(XElement userLanguage)
    {
        var keywordsRoot = userLanguage.Element("Keywords");
        if (keywordsRoot is null)
        {
            return [];
        }

        var groups = new List<KeywordGroup>();
        foreach (var element in keywordsRoot.Elements("Keywords"))
        {
            var groupName = (string?)element.Attribute("name");
            if (string.IsNullOrWhiteSpace(groupName))
            {
                continue;
            }

            if (string.Equals(groupName, "Operators", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var values = ParseDelimitedValues(element.Value);
            if (values.Count == 0)
            {
                continue;
            }

            groups.Add(new KeywordGroup(groupName, values));
        }

        return groups;
    }

    /// <summary>
    /// Parses comment definitions from supported UDL delimiter nodes.
    /// </summary>
    private static (IReadOnlyList<string> LineComments, IReadOnlyList<BlockCommentDefinition> BlockComments) ParseCommentDefinitions(XElement userLanguage, List<string> diagnostics)
    {
        var lineComments = new List<string>();
        var blockComments = new List<BlockCommentDefinition>();

        foreach (var commentElement in userLanguage.Descendants("Comments").Elements())
        {
            var name = commentElement.Name.LocalName;
            var value = (string?)commentElement.Attribute("value") ?? commentElement.Value;
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (name.Contains("lineComment", StringComparison.OrdinalIgnoreCase))
            {
                lineComments.Add(value);
                continue;
            }

            if (name.Contains("commentStart", StringComparison.OrdinalIgnoreCase))
            {
                var endName = name.Replace("Start", "End", StringComparison.OrdinalIgnoreCase);
                var endElement = commentElement.Parent?.Element(endName);
                var endValue = (string?)endElement?.Attribute("value") ?? endElement?.Value;
                if (!string.IsNullOrWhiteSpace(endValue))
                {
                    blockComments.Add(new BlockCommentDefinition(value, endValue));
                }
            }
        }

        if (lineComments.Count == 0 && blockComments.Count == 0)
        {
            foreach (var delimiter in userLanguage.Descendants("Delimiter"))
            {
                var open = (string?)delimiter.Attribute("open");
                var close = (string?)delimiter.Attribute("close");
                var delimiterName = (string?)delimiter.Attribute("name") ?? string.Empty;
                if (!delimiterName.Contains("comment", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(open) && string.IsNullOrWhiteSpace(close))
                {
                    lineComments.Add(open);
                }
                else if (!string.IsNullOrWhiteSpace(open) && !string.IsNullOrWhiteSpace(close))
                {
                    blockComments.Add(new BlockCommentDefinition(open, close));
                }
            }
        }

        if (userLanguage.Descendants("Prefix").Any())
        {
            diagnostics.Add("Prefix-based operators or delimiters are ignored in the current importer.");
        }

        return (lineComments.Distinct(StringComparer.Ordinal).ToArray(), blockComments.Distinct().ToArray());
    }

    /// <summary>
    /// Parses supported string delimiters from UDL delimiter elements.
    /// </summary>
    private static IReadOnlyList<string> ParseStringDelimiters(XElement userLanguage, List<string> diagnostics)
    {
        var delimiters = new List<string>();

        foreach (var delimiter in userLanguage.Descendants("Delimiter"))
        {
            var name = (string?)delimiter.Attribute("name") ?? string.Empty;
            if (!name.Contains("string", StringComparison.OrdinalIgnoreCase) &&
                !name.Contains("quote", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var open = (string?)delimiter.Attribute("open");
            var close = (string?)delimiter.Attribute("close");
            if (!string.IsNullOrWhiteSpace(open) && string.Equals(open, close, StringComparison.Ordinal))
            {
                delimiters.Add(open);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(open) && !string.IsNullOrWhiteSpace(close))
            {
                diagnostics.Add($"String delimiter '{name}' uses different open and close values and is not supported in v1.");
            }
        }

        return delimiters.Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Splits a Notepad++ keyword or operator list into trimmed values.
    /// </summary>
    private static IReadOnlyList<string> ParseDelimitedValues(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}