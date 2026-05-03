using NotepadLite.Core;

namespace NotepadLite.Syntax.Tests;

public sealed class TextSearchEngineTests
{
    [Fact]
    public void FindNext_ReturnsFirstMatch_CaseSensitive()
    {
        var match = TextSearchEngine.FindNext("Hello hello", 0, new SearchOptions("hello", MatchCase: true, WholeWord: false));
        Assert.NotNull(match);
        Assert.Equal(6, match!.Value.Offset);
        Assert.Equal(5, match.Value.Length);
    }

    [Fact]
    public void FindNext_IsCaseInsensitive_WhenRequested()
    {
        var match = TextSearchEngine.FindNext("Hello hello", 0, new SearchOptions("hello", MatchCase: false, WholeWord: false));
        Assert.NotNull(match);
        Assert.Equal(0, match!.Value.Offset);
    }

    [Fact]
    public void FindNext_ReturnsNull_WhenNoMatch()
    {
        var match = TextSearchEngine.FindNext("abcdef", 0, new SearchOptions("zzz", MatchCase: false, WholeWord: false));
        Assert.Null(match);
    }

    [Fact]
    public void FindNext_WholeWord_RejectsSubstringInsideIdentifier()
    {
        var match = TextSearchEngine.FindNext("foobar foo bar", 0, new SearchOptions("foo", MatchCase: false, WholeWord: true));
        Assert.NotNull(match);
        Assert.Equal(7, match!.Value.Offset);
    }

    [Fact]
    public void FindNext_WholeWord_AllowsBoundariesAtPunctuation()
    {
        var match = TextSearchEngine.FindNext("(foo)", 0, new SearchOptions("foo", MatchCase: false, WholeWord: true));
        Assert.NotNull(match);
        Assert.Equal(1, match!.Value.Offset);
    }

    [Fact]
    public void FindPrevious_FindsMatchBeforeCaret()
    {
        var match = TextSearchEngine.FindPrevious("abc abc abc", startIndex: 8, new SearchOptions("abc", MatchCase: true, WholeWord: false));
        Assert.NotNull(match);
        Assert.Equal(4, match!.Value.Offset);
    }

    [Fact]
    public void FindPrevious_ReturnsNull_WhenNothingBefore()
    {
        var match = TextSearchEngine.FindPrevious("abc", startIndex: 0, new SearchOptions("abc", MatchCase: true, WholeWord: false));
        Assert.Null(match);
    }

    [Fact]
    public void FindAll_ReturnsAllMatches()
    {
        var matches = TextSearchEngine.FindAll("ab ab ab", new SearchOptions("ab", MatchCase: true, WholeWord: false));
        Assert.Equal(3, matches.Count);
        Assert.Equal(0, matches[0].Offset);
        Assert.Equal(3, matches[1].Offset);
        Assert.Equal(6, matches[2].Offset);
    }

    [Fact]
    public void FindAll_NonOverlapping()
    {
        var matches = TextSearchEngine.FindAll("aaaa", new SearchOptions("aa", MatchCase: true, WholeWord: false));
        Assert.Equal(2, matches.Count);
        Assert.Equal(0, matches[0].Offset);
        Assert.Equal(2, matches[1].Offset);
    }

    [Fact]
    public void FindAll_EmptyPattern_ReturnsEmpty()
    {
        var matches = TextSearchEngine.FindAll("hello", new SearchOptions(string.Empty, MatchCase: false, WholeWord: false));
        Assert.Empty(matches);
    }

    [Fact]
    public void FindAll_WholeWord_FiltersInternalSubstrings()
    {
        var matches = TextSearchEngine.FindAll("cat catalog cat.", new SearchOptions("cat", MatchCase: true, WholeWord: true));
        Assert.Equal(2, matches.Count);
        Assert.Equal(0, matches[0].Offset);
        Assert.Equal(12, matches[1].Offset);
    }

    [Fact]
    public void FindNext_EmptyPattern_ReturnsNull()
    {
        var match = TextSearchEngine.FindNext("hello", 0, new SearchOptions(string.Empty, MatchCase: false, WholeWord: false));
        Assert.Null(match);
    }
}
