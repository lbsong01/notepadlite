namespace NotepadLite.Core;

/// <summary>
/// Options controlling how <see cref="TextSearchEngine"/> matches text.
/// </summary>
/// <param name="Pattern">The text to search for.</param>
/// <param name="MatchCase">Whether matching is case-sensitive.</param>
/// <param name="WholeWord">Whether matches must be bounded by non-word characters.</param>
public readonly record struct SearchOptions(string Pattern, bool MatchCase, bool WholeWord);

/// <summary>
/// Represents a single match within a text buffer.
/// </summary>
/// <param name="Offset">Zero-based offset of the match.</param>
/// <param name="Length">Length of the match in characters.</param>
public readonly record struct SearchMatch(int Offset, int Length);

/// <summary>
/// Pure text search helpers used by the editor's Find &amp; Replace UI.
/// </summary>
public static class TextSearchEngine
{
    /// <summary>
    /// Finds the next match at or after <paramref name="startIndex"/>.
    /// </summary>
    public static SearchMatch? FindNext(string text, int startIndex, SearchOptions options)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrEmpty(options.Pattern))
        {
            return null;
        }

        var clampedStart = Math.Clamp(startIndex, 0, text.Length);
        var comparison = GetComparison(options);

        var index = clampedStart;
        while (index <= text.Length - options.Pattern.Length)
        {
            var found = text.IndexOf(options.Pattern, index, comparison);
            if (found < 0)
            {
                return null;
            }

            if (!options.WholeWord || IsWholeWordAt(text, found, options.Pattern.Length))
            {
                return new SearchMatch(found, options.Pattern.Length);
            }

            index = found + 1;
        }

        return null;
    }

    /// <summary>
    /// Finds the previous match strictly before <paramref name="startIndex"/>.
    /// </summary>
    public static SearchMatch? FindPrevious(string text, int startIndex, SearchOptions options)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrEmpty(options.Pattern))
        {
            return null;
        }

        var comparison = GetComparison(options);
        // Search range is [0, startIndex - 1]; LastIndexOf needs a starting position
        // that represents the highest index included in the search.
        var searchEnd = Math.Min(text.Length - 1, startIndex - 1);
        if (searchEnd < 0)
        {
            return null;
        }

        var cursor = searchEnd;
        while (cursor >= 0)
        {
            var found = text.LastIndexOf(options.Pattern, cursor, cursor + 1, comparison);
            if (found < 0)
            {
                return null;
            }

            if (!options.WholeWord || IsWholeWordAt(text, found, options.Pattern.Length))
            {
                return new SearchMatch(found, options.Pattern.Length);
            }

            cursor = found - 1;
        }

        return null;
    }

    /// <summary>
    /// Returns all non-overlapping matches in <paramref name="text"/>.
    /// </summary>
    public static IReadOnlyList<SearchMatch> FindAll(string text, SearchOptions options)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrEmpty(options.Pattern))
        {
            return Array.Empty<SearchMatch>();
        }

        var comparison = GetComparison(options);
        var results = new List<SearchMatch>();

        var index = 0;
        while (index <= text.Length - options.Pattern.Length)
        {
            var found = text.IndexOf(options.Pattern, index, comparison);
            if (found < 0)
            {
                break;
            }

            if (!options.WholeWord || IsWholeWordAt(text, found, options.Pattern.Length))
            {
                results.Add(new SearchMatch(found, options.Pattern.Length));
                index = found + Math.Max(1, options.Pattern.Length);
            }
            else
            {
                index = found + 1;
            }
        }

        return results;
    }

    private static StringComparison GetComparison(SearchOptions options) =>
        options.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    private static bool IsWholeWordAt(string text, int offset, int length)
    {
        var leftOk = offset == 0 || !IsWordChar(text[offset - 1]);
        var rightIndex = offset + length;
        var rightOk = rightIndex >= text.Length || !IsWordChar(text[rightIndex]);
        return leftOk && rightOk;
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
