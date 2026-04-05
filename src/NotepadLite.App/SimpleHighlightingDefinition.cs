using ICSharpCode.AvalonEdit.Highlighting;

namespace NotepadLite.App;

/// <summary>
/// Provides a lightweight highlighting-definition implementation for dynamically loaded grammars.
/// </summary>
internal sealed class SimpleHighlightingDefinition : IHighlightingDefinition
{
    private readonly IReadOnlyDictionary<string, HighlightingColor> namedColors;
    private readonly IDictionary<string, string> properties;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleHighlightingDefinition"/> class.
    /// </summary>
    internal SimpleHighlightingDefinition(string name, HighlightingRuleSet mainRuleSet, IReadOnlyDictionary<string, HighlightingColor> namedColors)
    {
        Name = name;
        MainRuleSet = mainRuleSet;
        this.namedColors = namedColors;
        properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Name"] = name,
        };
    }

    /// <summary>
    /// Gets the definition name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the main highlighting rule set.
    /// </summary>
    public HighlightingRuleSet MainRuleSet { get; }

    /// <summary>
    /// Gets the named highlighting colors.
    /// </summary>
    public IEnumerable<HighlightingColor> NamedHighlightingColors => namedColors.Values;

    /// <summary>
    /// Gets arbitrary metadata associated with the highlighting definition.
    /// </summary>
    public IDictionary<string, string> Properties => properties;

    /// <summary>
    /// Retrieves a named color when available.
    /// </summary>
    public HighlightingColor? GetNamedColor(string name)
    {
        return namedColors.TryGetValue(name, out var color) ? color : null;
    }

    /// <summary>
    /// Retrieves a named rule set when available.
    /// </summary>
    public HighlightingRuleSet? GetNamedRuleSet(string name)
    {
        return string.Equals(name, MainRuleSet.Name, StringComparison.Ordinal) ? MainRuleSet : null;
    }
}