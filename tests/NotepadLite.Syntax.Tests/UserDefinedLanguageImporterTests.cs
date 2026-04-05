using NotepadLite.Syntax;
using System.IO;

namespace NotepadLite.Syntax.Tests;

/// <summary>
/// Verifies the supported subset of the Notepad++ language importer.
/// </summary>
public sealed class UserDefinedLanguageImporterTests
{
    /// <summary>
    /// Verifies that the importer captures common language-definition features.
    /// </summary>
    [Fact]
    public void ImportFromXml_ParsesSupportedDefinitionSubset()
    {
        const string xml = """
            <NotepadPlus>
              <UserLang name="Batch Script" ext="bat cmd">
                <Keywords>
                  <Keywords name="Keywords1">echo if set call goto</Keywords>
                  <Keywords name="Operators">== EQU NEQ</Keywords>
                </Keywords>
                <Delimiters>
                  <Delimiter name="string-double" open='"' close='"' />
                </Delimiters>
                <Comments>
                  <LineComment value="::" />
                  <BlockCommentStart value="rem[" />
                  <BlockCommentEnd value="]" />
                </Comments>
              </UserLang>
            </NotepadPlus>
            """;

        var result = UserDefinedLanguageImporter.ImportFromXml(xml);

        Assert.NotNull(result.Definition);
        Assert.Equal("Batch Script", result.Definition!.Name);
        Assert.Contains(".bat", result.Definition.Extensions);
        Assert.Contains(".cmd", result.Definition.Extensions);
        Assert.Contains(result.Definition.KeywordGroups, group => group.Keywords.Contains("echo"));
        Assert.Contains("::", result.Definition.LineComments);
        Assert.Contains(result.Definition.BlockComments, blockComment => blockComment.Start == "rem[" && blockComment.End == "]");
        Assert.Contains("\"", result.Definition.StringDelimiters);
        Assert.Contains("==", result.Definition.Operators);
    }

    /// <summary>
    /// Verifies that unsupported string delimiters are surfaced as diagnostics.
    /// </summary>
    [Fact]
    public void ImportFromXml_AddsDiagnosticsForUnsupportedStringDelimiters()
    {
        const string xml = """
            <NotepadPlus>
              <UserLang name="Custom" ext="cst">
                <Delimiters>
                  <Delimiter name="string-angle" open="&lt;" close="&gt;" />
                </Delimiters>
              </UserLang>
            </NotepadPlus>
            """;

        var result = UserDefinedLanguageImporter.ImportFromXml(xml);

        Assert.NotNull(result.Definition);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Contains("not supported", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(result.Definition!.StringDelimiters);
    }

      /// <summary>
      /// Verifies that the built-in syntax definition assets can be imported successfully.
      /// </summary>
      [Theory]
      [InlineData("batch-script.xml", "Batch Script")]
      [InlineData("powershell.xml", "PowerShell")]
      [InlineData("csharp.xml", "C#")]
      [InlineData("json.xml", "JSON")]
      [InlineData("xml.xml", "XML")]
      [InlineData("markdown.xml", "Markdown")]
      public void ImportFromXml_BuiltInAssetsRemainLoadable(string fileName, string expectedLanguageName)
      {
        var assetPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "languages", fileName);
        var normalizedPath = Path.GetFullPath(assetPath);
        var result = UserDefinedLanguageImporter.Import(normalizedPath);

        Assert.NotNull(result.Definition);
        Assert.Equal(expectedLanguageName, result.Definition!.Name);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Contains("failed", StringComparison.OrdinalIgnoreCase));
      }
}