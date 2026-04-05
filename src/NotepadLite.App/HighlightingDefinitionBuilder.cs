using ICSharpCode.AvalonEdit.Highlighting;
using NotepadLite.Syntax;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

namespace NotepadLite.App;

/// <summary>
/// Builds AvalonEdit highlighting definitions from normalized language definitions.
/// </summary>
internal static partial class HighlightingDefinitionBuilder
{
    private const string KeywordColorName = "Keyword";
    private const string CommentColorName = "Comment";
    private const string StringColorName = "String";
    private const string OperatorColorName = "Operator";

    /// <summary>
    /// Creates an AvalonEdit highlighting definition for the supplied language.
    /// </summary>
    internal static IHighlightingDefinition Build(LanguageDefinition definition)
    {
        var highlightingColors = CreateColors();
        var mainRuleSet = new HighlightingRuleSet();

        AddKeywordRules(definition, highlightingColors, mainRuleSet);
        AddOperatorRules(definition, highlightingColors, mainRuleSet);
        AddSpanRules(definition, highlightingColors, mainRuleSet);

        return new SimpleHighlightingDefinition(definition.Name, mainRuleSet, highlightingColors);
    }

    /// <summary>
    /// Returns the configured highlighting colors used by the editor.
    /// </summary>
    private static Dictionary<string, HighlightingColor> CreateColors()
    {
        return new Dictionary<string, HighlightingColor>(StringComparer.Ordinal)
        {
            [KeywordColorName] = new HighlightingColor { Name = KeywordColorName, Foreground = new SimpleHighlightingBrush(Color.FromRgb(0x00, 0x5C, 0xB9)), FontWeight = FontWeights.SemiBold },
            [CommentColorName] = new HighlightingColor { Name = CommentColorName, Foreground = new SimpleHighlightingBrush(Color.FromRgb(0x1E, 0x7A, 0x1E)) },
            [StringColorName] = new HighlightingColor { Name = StringColorName, Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xA3, 0x15, 0x15)) },
            [OperatorColorName] = new HighlightingColor { Name = OperatorColorName, Foreground = new SimpleHighlightingBrush(Color.FromRgb(0x7A, 0x2C, 0xB8)) },
        };
    }

    /// <summary>
    /// Adds keyword highlighting rules to the main rule set.
    /// </summary>
    private static void AddKeywordRules(LanguageDefinition definition, IReadOnlyDictionary<string, HighlightingColor> colors, HighlightingRuleSet ruleSet)
    {
        foreach (var keywordGroup in definition.KeywordGroups)
        {
            if (keywordGroup.Keywords.Count == 0)
            {
                continue;
            }

            var escapedKeywords = keywordGroup.Keywords.Select(Regex.Escape);
            var pattern = $@"\b(?:{string.Join("|", escapedKeywords)})\b";
            ruleSet.Rules.Add(new HighlightingRule
            {
                Regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant),
                Color = colors[KeywordColorName],
            });
        }
    }

    /// <summary>
    /// Adds operator highlighting rules to the main rule set.
    /// </summary>
    private static void AddOperatorRules(LanguageDefinition definition, IReadOnlyDictionary<string, HighlightingColor> colors, HighlightingRuleSet ruleSet)
    {
        if (definition.Operators.Count == 0)
        {
            return;
        }

        var escapedOperators = definition.Operators.Select(Regex.Escape);
        var pattern = string.Join("|", escapedOperators.OrderByDescending(static value => value.Length));
        ruleSet.Rules.Add(new HighlightingRule
        {
            Regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant),
            Color = colors[OperatorColorName],
        });
    }

    /// <summary>
    /// Adds comment and string span rules to the main rule set.
    /// </summary>
    private static void AddSpanRules(LanguageDefinition definition, IReadOnlyDictionary<string, HighlightingColor> colors, HighlightingRuleSet ruleSet)
    {
        foreach (var lineComment in definition.LineComments)
        {
            ruleSet.Spans.Add(new HighlightingSpan
            {
                StartExpression = new Regex(Regex.Escape(lineComment), RegexOptions.Compiled | RegexOptions.CultureInvariant),
                EndExpression = EndOfLineRegex(),
                SpanColor = colors[CommentColorName],
                SpanColorIncludesStart = true,
                SpanColorIncludesEnd = true,
            });
        }

        foreach (var blockComment in definition.BlockComments)
        {
            ruleSet.Spans.Add(new HighlightingSpan
            {
                StartExpression = new Regex(Regex.Escape(blockComment.Start), RegexOptions.Compiled | RegexOptions.CultureInvariant),
                EndExpression = new Regex(Regex.Escape(blockComment.End), RegexOptions.Compiled | RegexOptions.CultureInvariant),
                SpanColor = colors[CommentColorName],
                SpanColorIncludesStart = true,
                SpanColorIncludesEnd = true,
            });
        }

        foreach (var stringDelimiter in definition.StringDelimiters)
        {
            ruleSet.Spans.Add(new HighlightingSpan
            {
                StartExpression = new Regex(Regex.Escape(stringDelimiter), RegexOptions.Compiled | RegexOptions.CultureInvariant),
                EndExpression = new Regex(Regex.Escape(stringDelimiter), RegexOptions.Compiled | RegexOptions.CultureInvariant),
                SpanColor = colors[StringColorName],
                SpanColorIncludesStart = true,
                SpanColorIncludesEnd = true,
            });
        }
    }

    [GeneratedRegex("$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex EndOfLineRegex();
}