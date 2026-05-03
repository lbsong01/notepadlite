using NotepadLite.Core.Formatting;

namespace NotepadLite.Core.Tests.Formatting;

public class XmlDocumentFormatterTests
{
    private readonly XmlDocumentFormatter formatter = new();

    [Theory]
    [InlineData(null, ".xml", true)]
    [InlineData(null, ".csproj", true)]
    [InlineData(null, ".XAML", true)]
    [InlineData("XML", null, true)]
    [InlineData(null, ".json", false)]
    [InlineData(null, null, false)]
    public void CanFormat_MatchesXmlByLanguageOrExtension(string? language, string? extension, bool expected)
    {
        Assert.Equal(expected, formatter.CanFormat(language, extension));
    }

    [Fact]
    public void Format_PrettyPrintsMinifiedXml()
    {
        var result = formatter.Format("<root><child a=\"1\">hi</child></root>");

        Assert.True(result.Success);
        Assert.Contains("<root>", result.FormattedText);
        Assert.Contains("    <child", result.FormattedText);
        Assert.Contains("</root>", result.FormattedText);
    }

    [Fact]
    public void Format_PreservesXmlDeclarationWhenPresent()
    {
        var input = "<?xml version=\"1.0\" encoding=\"utf-8\"?><root />";

        var result = formatter.Format(input);

        Assert.True(result.Success);
        Assert.Contains("<?xml", result.FormattedText);
    }

    [Fact]
    public void Format_OmitsDeclarationWhenInputHasNone()
    {
        var result = formatter.Format("<root><a /></root>");

        Assert.True(result.Success);
        Assert.DoesNotContain("<?xml", result.FormattedText);
    }

    [Fact]
    public void Format_PreservesCData()
    {
        var result = formatter.Format("<root><![CDATA[some <data> here]]></root>");

        Assert.True(result.Success);
        Assert.Contains("<![CDATA[some <data> here]]>", result.FormattedText);
    }

    [Fact]
    public void Format_InvalidXml_ReturnsFailureAndPreservesText()
    {
        var input = "<root><child></root>";

        var result = formatter.Format(input);

        Assert.False(result.Success);
        Assert.Equal(input, result.FormattedText);
        Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
    }
}
