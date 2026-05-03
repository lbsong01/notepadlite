namespace NotepadLite.Core.Formatting;

/// <summary>
/// Orchestrates document formatting by selecting an appropriate <see cref="IDocumentFormatter"/>
/// for the active document's language or file extension. Falls back to <see cref="IndentDocumentFormatter"/>.
/// </summary>
public sealed class DocumentFormattingService
{
    private readonly IReadOnlyList<IDocumentFormatter> formatters;
    private readonly IDocumentFormatter fallback;

    /// <summary>
    /// Creates a service with the default JSON, XML, and indent-fallback formatters.
    /// </summary>
    public DocumentFormattingService()
        : this(
            [new JsonDocumentFormatter(), new XmlDocumentFormatter()],
            new IndentDocumentFormatter())
    {
    }

    /// <summary>
    /// Creates a service with explicit formatters. The first formatter whose
    /// <see cref="IDocumentFormatter.CanFormat"/> returns <see langword="true"/> is used;
    /// otherwise <paramref name="fallback"/> is invoked.
    /// </summary>
    public DocumentFormattingService(IReadOnlyList<IDocumentFormatter> formatters, IDocumentFormatter fallback)
    {
        ArgumentNullException.ThrowIfNull(formatters);
        ArgumentNullException.ThrowIfNull(fallback);

        this.formatters = formatters;
        this.fallback = fallback;
    }

    /// <summary>
    /// Formats <paramref name="text"/> using the formatter best matching the supplied language and extension.
    /// </summary>
    public FormatResult Format(string text, string? languageName, string? extension)
    {
        ArgumentNullException.ThrowIfNull(text);

        foreach (var formatter in formatters)
        {
            if (formatter.CanFormat(languageName, extension))
            {
                return formatter.Format(text);
            }
        }

        return fallback.Format(text);
    }
}
