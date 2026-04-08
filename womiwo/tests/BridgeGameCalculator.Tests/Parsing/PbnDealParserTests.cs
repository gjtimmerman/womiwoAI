using BridgeGameCalculator.Shared.Models;
using BridgeGameCalculator.Shared.Parsing;

namespace BridgeGameCalculator.Tests.Parsing;

public sealed class PbnDealParserTests
{
    [Fact]
    public void ParseHand_ValidFullHand_ReturnsCorrectSuits()
    {
        var hand = PbnDealParser.ParseHand("AKQ.JT9.87.654");

        Assert.Equal(3, hand.Spades.Count);
        Assert.Equal(Rank.Ace,   hand.Spades[0].Rank);
        Assert.Equal(Rank.King,  hand.Spades[1].Rank);
        Assert.Equal(Rank.Queen, hand.Spades[2].Rank);

        Assert.Equal(3, hand.Hearts.Count);
        Assert.Equal(Rank.Jack, hand.Hearts[0].Rank);
        Assert.Equal(Rank.Ten,  hand.Hearts[1].Rank);
        Assert.Equal(Rank.Nine, hand.Hearts[2].Rank);

        Assert.Equal(2, hand.Diamonds.Count);
        Assert.Equal(3, hand.Clubs.Count);
    }

    [Fact]
    public void ParseHand_VoidSuit_ReturnsEmptyList()
    {
        var hand = PbnDealParser.ParseHand("AKQ..87.654");

        Assert.Empty(hand.Hearts);
    }

    [Fact]
    public void ParseHand_AllCardsInOneSuit_Works()
    {
        var hand = PbnDealParser.ParseHand("AKQJT98765432...");

        Assert.Equal(13, hand.Spades.Count);
        Assert.Empty(hand.Hearts);
        Assert.Empty(hand.Diamonds);
        Assert.Empty(hand.Clubs);
    }

    [Fact]
    public void ParseHand_TenAsT_Parsed()
    {
        var hand = PbnDealParser.ParseHand("T...");

        Assert.Single(hand.Spades);
        Assert.Equal(Rank.Ten, hand.Spades[0].Rank);
    }

    [Fact]
    public void ParseHand_TenAs10_Parsed()
    {
        var hand = PbnDealParser.ParseHand("10...");

        Assert.Single(hand.Spades);
        Assert.Equal(Rank.Ten, hand.Spades[0].Rank);
    }

    [Fact]
    public void ParseHand_CardsAreSortedDescending()
    {
        var hand = PbnDealParser.ParseHand("23AKJ...");

        Assert.Equal(Rank.Ace,   hand.Spades[0].Rank);
        Assert.Equal(Rank.King,  hand.Spades[1].Rank);
        Assert.Equal(Rank.Jack,  hand.Spades[2].Rank);
        Assert.Equal(Rank.Three, hand.Spades[3].Rank);
        Assert.Equal(Rank.Two,   hand.Spades[4].Rank);
    }

    [Fact]
    public void ParseHand_CardsHaveCorrectSuit()
    {
        var hand = PbnDealParser.ParseHand("A.K.Q.J");

        Assert.All(hand.Spades,   c => Assert.Equal(Suit.Spades,   c.Suit));
        Assert.All(hand.Hearts,   c => Assert.Equal(Suit.Hearts,   c.Suit));
        Assert.All(hand.Diamonds, c => Assert.Equal(Suit.Diamonds, c.Suit));
        Assert.All(hand.Clubs,    c => Assert.Equal(Suit.Clubs,    c.Suit));
    }

    [Fact]
    public void ParseHand_UnknownRankCharacter_ThrowsPbnParseException()
    {
        var ex = Assert.Throws<PbnParseException>(() =>
            PbnDealParser.ParseHand("AXQ.JT9.87.654"));

        Assert.Contains("'X'", ex.Message);
    }

    [Fact]
    public void ParseHand_WrongSectionCount_ThrowsPbnParseException()
    {
        Assert.Throws<PbnParseException>(() =>
            PbnDealParser.ParseHand("AKQ.JT9.87"));
    }

    [Fact]
    public void ParseHand_EmptyString_ThrowsPbnParseException()
    {
        Assert.Throws<PbnParseException>(() =>
            PbnDealParser.ParseHand(string.Empty));
    }

    [Fact]
    public void ParseAllHands_ReturnsAllFourSeats()
    {
        var hands = new Hands(
            North: "AKQ2.32.AKQ2.AK3",
            East:  "JT98.QJT9.J543.2",
            South: "7654.A876.T97.T9",
            West:  "3.K54.86.QJ87654");

        var parsed = PbnDealParser.ParseAllHands(hands);

        Assert.True(parsed.ContainsKey(Seat.North));
        Assert.True(parsed.ContainsKey(Seat.East));
        Assert.True(parsed.ContainsKey(Seat.South));
        Assert.True(parsed.ContainsKey(Seat.West));

        Assert.Equal(Rank.Ace,  parsed[Seat.North].Spades[0].Rank);
        Assert.Equal(Rank.King, parsed[Seat.West].Hearts[0].Rank);
    }
}
