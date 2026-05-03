using NotepadLite.Core.Formatting;

namespace NotepadLite.Core.Tests.Formatting;

public class DocumentFormattingServiceTests
{
    private readonly DocumentFormattingService service = new();

    [Fact]
    public void Format_JsonExtension_RoutesToJsonFormatter()
    {
        var result = service.Format("{\"a\":1}", languageName: null, extension: ".json");

        Assert.True(result.Success);
        Assert.Contains("\"a\": 1", result.FormattedText);
    }

    [Fact]
    public void Format_XmlExtension_RoutesToXmlFormatter()
    {
        var result = service.Format("<r><c/></r>", languageName: null, extension: ".xml");

        Assert.True(result.Success);
        Assert.Contains("    <c", result.FormattedText);
    }

    [Fact]
    public void Format_UnknownExtension_FallsBackToIndentFormatter()
    {
        var input = "{\nfoo\n}";

        var result = service.Format(input, languageName: null, extension: ".unknown");

        Assert.True(result.Success);
        var lines = result.FormattedText.Split('\n');
        Assert.Equal("{", lines[0]);
        Assert.Equal("    foo", lines[1]);
        Assert.Equal("}", lines[2]);
    }

    [Fact]
    public void Format_LanguageNameTakesPrecedenceOverExtension()
    {
        // Language=JSON should route to JSON formatter even with no extension.
        var result = service.Format("{\"a\":1}", languageName: "JSON", extension: null);

        Assert.True(result.Success);
        Assert.Contains("\"a\": 1", result.FormattedText);
    }

    [Fact]
    public void Format_InvalidJson_ReturnsFailureFromJsonFormatter()
    {
        var input = "{ not json";

        var result = service.Format(input, languageName: "JSON", extension: ".json");

        Assert.False(result.Success);
        Assert.Equal(input, result.FormattedText);
    }
}
