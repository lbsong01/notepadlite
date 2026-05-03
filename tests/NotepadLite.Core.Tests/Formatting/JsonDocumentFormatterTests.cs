using NotepadLite.Core.Formatting;

namespace NotepadLite.Core.Tests.Formatting;

public class JsonDocumentFormatterTests
{
    private readonly JsonDocumentFormatter formatter = new();

    [Theory]
    [InlineData(null, ".json", true)]
    [InlineData("JSON", null, true)]
    [InlineData("json", ".JSON", true)]
    [InlineData(null, ".jsonc", true)]
    [InlineData(null, ".xml", false)]
    [InlineData(null, null, false)]
    public void CanFormat_MatchesJsonByLanguageOrExtension(string? language, string? extension, bool expected)
    {
        Assert.Equal(expected, formatter.CanFormat(language, extension));
    }

    [Fact]
    public void Format_PrettyPrintsMinifiedJson()
    {
        var result = formatter.Format("{\"a\":1,\"b\":[1,2]}");

        Assert.True(result.Success);
        Assert.Contains("\n", result.FormattedText);
        Assert.Contains("\"a\": 1", result.FormattedText);
        Assert.Contains("\"b\": [", result.FormattedText);
    }

    [Fact]
    public void Format_AlreadyPrettyJson_RoundTripsToEquivalent()
    {
        var input = "{\n  \"a\": 1\n}";

        var first = formatter.Format(input);
        var second = formatter.Format(first.FormattedText);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(first.FormattedText, second.FormattedText);
    }

    [Fact]
    public void Format_InvalidJson_ReturnsFailureAndPreservesText()
    {
        var input = "{ not valid }";

        var result = formatter.Format(input);

        Assert.False(result.Success);
        Assert.Equal(input, result.FormattedText);
        Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
    }

    [Fact]
    public void Format_PreservesUnicodeAndNumbers()
    {
        var result = formatter.Format("{\"name\":\"caf\u00e9\",\"n\":3.14}");

        Assert.True(result.Success);
        Assert.Contains("caf\u00e9", result.FormattedText);
        Assert.Contains("3.14", result.FormattedText);
    }

    [Fact]
    public void Format_Empty_ReturnsOriginal()
    {
        var result = formatter.Format("   \n  ");

        Assert.True(result.Success);
        Assert.Equal("   \n  ", result.FormattedText);
    }
}
