using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace NotepadLite.Core.Formatting;

/// <summary>
/// Formats JSON content using <see cref="JsonDocument"/> and <see cref="Utf8JsonWriter"/>.
/// </summary>
public sealed class JsonDocumentFormatter : IDocumentFormatter
{
    private static readonly string[] SupportedExtensions = [".json", ".jsonc"];
    private static readonly JsonDocumentOptions ParseOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <inheritdoc />
    public bool CanFormat(string? languageName, string? extension)
    {
        if (!string.IsNullOrEmpty(languageName)
            && string.Equals(languageName, "JSON", StringComparison.OrdinalIgnoreCase))
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
            using var document = JsonDocument.Parse(text, ParseOptions);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, WriterOptions))
            {
                document.WriteTo(writer);
            }

            var formatted = Encoding.UTF8.GetString(stream.ToArray());
            return FormatResult.Ok(formatted);
        }
        catch (JsonException ex)
        {
            return FormatResult.Fail(text, ex.Message);
        }
    }
}
