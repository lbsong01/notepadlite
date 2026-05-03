namespace NotepadLite.Core.Formatting;

/// <summary>
/// Result of a formatting operation. On failure, <see cref="FormattedText"/> contains the original text unchanged.
/// </summary>
public sealed record FormatResult(bool Success, string FormattedText, string? ErrorMessage)
{
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static FormatResult Ok(string formattedText) => new(true, formattedText, null);

    /// <summary>
    /// Creates a failed result that preserves the original text.
    /// </summary>
    public static FormatResult Fail(string originalText, string errorMessage) =>
        new(false, originalText, errorMessage);
}
