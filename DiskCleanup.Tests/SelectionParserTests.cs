using DiskCleanup.Core;

namespace DiskCleanup.Tests;

public class SelectionParserTests
{
    // Three items: #1 SAFE, #2 REVIEW, #3 SAFE
    static readonly List<string> Risks = new() { "SAFE", "REVIEW", "SAFE" };

    [Fact]
    public void ParsesCommaSeparatedNumbers()
    {
        var result = SelectionParser.Parse("1,2", Risks);
        Assert.Equal(new[] { 1, 2 }, result);
    }

    [Fact]
    public void TrimsWhitespaceAroundNumbers()
    {
        var result = SelectionParser.Parse(" 1 , 2 ", Risks);
        Assert.Equal(new[] { 1, 2 }, result);
    }

    [Fact]
    public void AllSafeSelectsOnlySafeItems()
    {
        var result = SelectionParser.Parse("all-safe", Risks);
        Assert.Equal(new[] { 1, 3 }, result);
    }

    [Fact]
    public void AllSafeIsCaseInsensitive()
    {
        var result = SelectionParser.Parse("ALL-SAFE", Risks);
        Assert.Equal(new[] { 1, 3 }, result);
    }

    [Fact]
    public void EmptyInputReturnsNothing()
    {
        var result = SelectionParser.Parse("", Risks);
        Assert.Empty(result);
    }

    [Fact]
    public void WhitespaceOnlyInputReturnsNothing()
    {
        var result = SelectionParser.Parse("   ", Risks);
        Assert.Empty(result);
    }

    [Fact]
    public void OutOfRangeNumbersAreIgnored()
    {
        var result = SelectionParser.Parse("0,1,4,99", Risks);
        Assert.Equal(new[] { 1 }, result);
    }

    [Fact]
    public void NonNumericTokensAreIgnored()
    {
        var result = SelectionParser.Parse("1,abc,2", Risks);
        Assert.Equal(new[] { 1, 2 }, result);
    }

    [Fact]
    public void DuplicateNumbersAreCollapsed()
    {
        var result = SelectionParser.Parse("1,1,2,2", Risks);
        Assert.Equal(new[] { 1, 2 }, result);
    }

    [Fact]
    public void ResultIsSortedRegardlessOfInputOrder()
    {
        var result = SelectionParser.Parse("3,1,2", Risks);
        Assert.Equal(new[] { 1, 2, 3 }, result);
    }
}
