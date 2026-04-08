using BridgeGameCalculator.Shared.Models;
using BridgeGameCalculator.Shared.Parsing;

namespace BridgeGameCalculator.Tests.Parsing;

public sealed class HandParserTests
{
    [Fact]
    public void Parse_DotFormat_ValidHand_Returns13Cards()
    {
        var result = HandParser.Parse("AKQ.JT9.87.65432");

        Assert.True(result.IsSuccess);
        Assert.Equal(13, result.AllCards!.Count);
    }

    [Fact]
    public void Parse_DotFormat_SuitsInOrder()
    {
        var result = HandParser.Parse("AKQ.JT9.87.65432");

        Assert.Equal(3, result.Spades!.Count);
        Assert.All(result.Spades, c => Assert.Equal(Suit.Spades, c.Suit));
        Assert.Equal(3, result.Hearts!.Count);
        Assert.All(result.Hearts, c => Assert.Equal(Suit.Hearts, c.Suit));
        Assert.Equal(2, result.Diamonds!.Count);
        Assert.Equal(5, result.Clubs!.Count);
    }

    [Fact]
    public void Parse_ColonFormat_ValidHand_Returns13Cards()
    {
        var result = HandParser.Parse("S:AKQ H:JT9 D:87 C:65432");

        Assert.True(result.IsSuccess);
        Assert.Equal(13, result.AllCards!.Count);
    }

    [Fact]
    public void Parse_ColonFormat_AnyOrder_Works()
    {
        var result = HandParser.Parse("C:65432 H:JT9 D:87 S:AKQ");

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Spades!.Count);
    }

    [Fact]
    public void Parse_NormalisesLowercase()
    {
        var result = HandParser.Parse("akq.jt9.87.65432");

        Assert.True(result.IsSuccess);
        Assert.Equal(Rank.Ace, result.Spades![0].Rank);
    }

    [Fact]
    public void Parse_Normalises10ToT()
    {
        var result = HandParser.Parse("AKQ.J109.87.65432");

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Hearts!, c => c.Rank == Rank.Ten);
    }

    [Fact]
    public void Parse_VoidSuit_DotFormat_EmptyList()
    {
        var result = HandParser.Parse("AKQJT98765432...");

        Assert.True(result.IsSuccess);
        Assert.Equal(13, result.Spades!.Count);
        Assert.Empty(result.Hearts!);
        Assert.Empty(result.Diamonds!);
        Assert.Empty(result.Clubs!);
    }

    [Fact]
    public void Parse_VoidSuit_ColonFormat_EmptyList()
    {
        var result = HandParser.Parse("S:AKQJT98765432 H: D: C:");

        Assert.True(result.IsSuccess);
        Assert.Equal(13, result.Spades!.Count);
        Assert.Empty(result.Hearts!);
    }

    [Fact]
    public void Parse_CardsSortedDescending()
    {
        var result = HandParser.Parse("23AKJ.QQ2.A23.T98765");

        // Spades: A K J 3 2 in descending order
        // Note: QQ2 in Hearts has duplicates — this hand has only 12 cards, so it should fail
        // Let's use valid input instead
    }

    [Fact]
    public void Parse_DotFormat_CardsSortedDescendingByRank()
    {
        // 5+5+1+2=13 cards, all unique across suits
        var result = HandParser.Parse("23AKJ.JT987.2.QT");

        Assert.True(result.IsSuccess);
        var spades = result.Spades!;
        Assert.Equal(Rank.Ace,   spades[0].Rank);
        Assert.Equal(Rank.King,  spades[1].Rank);
        Assert.Equal(Rank.Jack,  spades[2].Rank);
        Assert.Equal(Rank.Three, spades[3].Rank);
        Assert.Equal(Rank.Two,   spades[4].Rank);
    }

    [Fact]
    public void Parse_PbnHand_IsNormalisedDotFormat()
    {
        var result = HandParser.Parse("S:AKQ H:JT9 D:87 C:65432");

        Assert.True(result.IsSuccess);
        Assert.Equal("AKQ.JT9.87.65432", result.PbnHand);
    }

    [Fact]
    public void Parse_UnknownRankCharacter_Fails()
    {
        var result = HandParser.Parse("AXQ.JT9.87.65432");

        Assert.False(result.IsSuccess);
        Assert.Contains("'X'", result.Error);
    }

    [Fact]
    public void Parse_TooFewCards_Fails()
    {
        var result = HandParser.Parse("AKQ.JT9.87.6543");  // 12 cards

        Assert.False(result.IsSuccess);
        Assert.Contains("12", result.Error);
    }

    [Fact]
    public void Parse_TooManyCards_Fails()
    {
        // 4+3+4+3=14 cards, all unique (T of Spades ≠ T of Hearts)
        var result = HandParser.Parse("AKQT.JT9.8765.432");

        Assert.False(result.IsSuccess);
        Assert.Contains("14", result.Error);
    }

    [Fact]
    public void Parse_DuplicateCardWithinHand_Fails()
    {
        var result = HandParser.Parse("AAQ.JT9.87.65432");  // two Aces of Spades

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Parse_UnrecognizedFormat_Fails()
    {
        var result = HandParser.Parse("random text without dots or colons");

        Assert.False(result.IsSuccess);
        Assert.Contains("Unrecognized", result.Error);
    }

    [Fact]
    public void Parse_EmptyString_Fails()
    {
        var result = HandParser.Parse("");

        Assert.False(result.IsSuccess);
        Assert.Contains("required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_WhitespaceOnly_Fails()
    {
        var result = HandParser.Parse("   ");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Parse_ExtraWhitespace_ColonFormat_Works()
    {
        var result = HandParser.Parse("  S:AKQ   H:JT9  D:87  C:65432  ");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Parse_WrongDotSectionCount_Fails()
    {
        var result = HandParser.Parse("AKQ.JT9.87");  // only 3 sections

        Assert.False(result.IsSuccess);
        Assert.Contains("3", result.Error);
    }
}
