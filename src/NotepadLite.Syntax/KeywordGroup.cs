namespace NotepadLite.Syntax;

/// <summary>
/// Represents a named keyword group in a language definition.
/// </summary>
public sealed record KeywordGroup(string Name, IReadOnlyList<string> Keywords);