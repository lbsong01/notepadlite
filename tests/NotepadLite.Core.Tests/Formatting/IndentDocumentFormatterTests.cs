using NotepadLite.Core.Formatting;

namespace NotepadLite.Core.Tests.Formatting;

public class IndentDocumentFormatterTests
{
    private readonly IndentDocumentFormatter formatter = new();

    [Fact]
    public void CanFormat_AlwaysReturnsTrue()
    {
        Assert.True(formatter.CanFormat(null, null));
        Assert.True(formatter.CanFormat("Anything", ".whatever"));
    }

    [Fact]
    public void Format_NestedBraces_IndentedConsistently()
    {
        var input = "class Foo {\nvoid Bar() {\nif (x) {\nDoStuff();\n}\n}\n}";

        var result = formatter.Format(input);

        Assert.True(result.Success);
        var lines = result.FormattedText.Split('\n');
        Assert.Equal("class Foo {", lines[0]);
        Assert.Equal("    void Bar() {", lines[1]);
        Assert.Equal("        if (x) {", lines[2]);
        Assert.Equal("            DoStuff();", lines[3]);
        Assert.Equal("        }", lines[4]);
        Assert.Equal("    }", lines[5]);
        Assert.Equal("}", lines[6]);
    }

    [Fact]
    public void Format_MixedBraceAndBracket_DedentOnClose()
    {
        var input = "[\n{\n\"a\":1\n}\n]";

        var result = formatter.Format(input);

        var lines = result.FormattedText.Split('\n');
        Assert.Equal("[", lines[0]);
        Assert.Equal("    {", lines[1]);
        Assert.Equal("        \"a\":1", lines[2]);
        Assert.Equal("    }", lines[3]);
        Assert.Equal("]", lines[4]);
    }

    [Fact]
    public void Format_TrimsTrailingWhitespace()
    {
        var input = "hello   \nworld\t";

        var result = formatter.Format(input);

        Assert.Equal("hello\nworld", result.FormattedText);
    }

    [Fact]
    public void Format_PreservesBlankLines()
    {
        var input = "a\n\nb";

        var result = formatter.Format(input);

        Assert.Equal("a\n\nb", result.FormattedText);
    }

    [Fact]
    public void Format_CommentLinesDoNotAffectDepth()
    {
        var input = "{\n# a powershell-style comment\nkey = value\n}";

        var result = formatter.Format(input);

        var lines = result.FormattedText.Split('\n');
        Assert.Equal("{", lines[0]);
        Assert.Equal("    # a powershell-style comment", lines[1]);
        Assert.Equal("    key = value", lines[2]);
        Assert.Equal("}", lines[3]);
    }

    [Fact]
    public void Format_PreservesCrlfNewlines()
    {
        var input = "a\r\nb";

        var result = formatter.Format(input);

        Assert.Equal("a\r\nb", result.FormattedText);
    }

    [Fact]
    public void Format_EmptyText_ReturnsOriginal()
    {
        var result = formatter.Format(string.Empty);

        Assert.True(result.Success);
        Assert.Equal(string.Empty, result.FormattedText);
    }
}
