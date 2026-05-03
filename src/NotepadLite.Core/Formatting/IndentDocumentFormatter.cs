using System.Text;

namespace NotepadLite.Core.Formatting;

/// <summary>
/// Best-effort fallback formatter: trims trailing whitespace and re-indents lines based on
/// the running depth of brace and bracket characters. Lines whose first non-whitespace character
/// is a recognised comment marker do not contribute to the depth count.
/// </summary>
public sealed class IndentDocumentFormatter : IDocumentFormatter
{
    private const string IndentUnit = "    ";

    /// <inheritdoc />
    public bool CanFormat(string? languageName, string? extension) => true;

    /// <inheritdoc />
    public FormatResult Format(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length == 0)
        {
            return FormatResult.Ok(text);
        }

        var newline = DetectNewline(text);
        var lines = text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
        var builder = new StringBuilder(text.Length);
        var depth = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();

            if (trimmed.Length == 0)
            {
                if (i < lines.Length - 1)
                {
                    builder.Append(newline);
                }
                continue;
            }

            var (opens, closes, isComment) = AnalyseLine(trimmed);
            var lineDepth = isComment ? depth : Math.Max(0, depth - closes);

            for (var d = 0; d < lineDepth; d++)
            {
                builder.Append(IndentUnit);
            }
            builder.Append(trimmed);

            if (i < lines.Length - 1)
            {
                builder.Append(newline);
            }

            if (!isComment)
            {
                depth = Math.Max(0, depth + opens - closes);
            }
        }

        return FormatResult.Ok(builder.ToString());
    }

    private static (int Opens, int Closes, bool IsComment) AnalyseLine(string trimmedLine)
    {
        if (IsCommentLine(trimmedLine))
        {
            return (0, 0, true);
        }

        var opens = 0;
        var closes = 0;

        foreach (var c in trimmedLine)
        {
            switch (c)
            {
                case '{':
                case '[':
                    opens++;
                    break;
                case '}':
                case ']':
                    closes++;
                    break;
            }
        }

        return (opens, closes, false);
    }

    private static bool IsCommentLine(string trimmedLine)
    {
        if (trimmedLine.StartsWith("//", StringComparison.Ordinal)
            || trimmedLine.StartsWith("#", StringComparison.Ordinal)
            || trimmedLine.StartsWith("--", StringComparison.Ordinal))
        {
            return true;
        }

        if (trimmedLine.StartsWith("REM ", StringComparison.OrdinalIgnoreCase)
            || trimmedLine.Equals("REM", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (trimmedLine.StartsWith("<!--", StringComparison.Ordinal)
            || trimmedLine.StartsWith("/*", StringComparison.Ordinal)
            || trimmedLine.StartsWith("*", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static string DetectNewline(string text)
    {
        var crlf = text.IndexOf("\r\n", StringComparison.Ordinal);
        if (crlf >= 0)
        {
            return "\r\n";
        }

        if (text.Contains('\n'))
        {
            return "\n";
        }

        return Environment.NewLine;
    }
}
