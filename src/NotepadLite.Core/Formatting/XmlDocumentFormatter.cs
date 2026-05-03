using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace NotepadLite.Core.Formatting;

/// <summary>
/// Formats XML content using <see cref="XDocument"/>.
/// </summary>
public sealed class XmlDocumentFormatter : IDocumentFormatter
{
    private static readonly string[] SupportedExtensions =
    [
        ".xml", ".xaml", ".xsd", ".xslt", ".csproj", ".props", ".targets", ".config",
    ];

    /// <inheritdoc />
    public bool CanFormat(string? languageName, string? extension)
    {
        if (!string.IsNullOrEmpty(languageName)
            && string.Equals(languageName, "XML", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrEmpty(extension))
        {
            return false;
        }

        foreach (var supported in SupportedExtensions)
        {
            if (string.Equals(extension, supported, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public FormatResult Format(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (string.IsNullOrWhiteSpace(text))
        {
            return FormatResult.Ok(text);
        }

        try
        {
            var document = XDocument.Parse(text);

            var hasDeclaration = document.Declaration is not null
                || text.TrimStart().StartsWith("<?xml", StringComparison.OrdinalIgnoreCase);

            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "    ",
                NewLineHandling = NewLineHandling.Replace,
                OmitXmlDeclaration = !hasDeclaration,
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            };

            var builder = new StringBuilder();
            using (var writer = XmlWriter.Create(builder, settings))
            {
                document.Save(writer);
            }

            return FormatResult.Ok(builder.ToString());
        }
        catch (XmlException ex)
        {
            return FormatResult.Fail(text, ex.Message);
        }
    }
}
