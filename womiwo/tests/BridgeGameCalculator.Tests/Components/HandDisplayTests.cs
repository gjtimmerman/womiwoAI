using Bunit;
using BridgeGameCalculator.Client.Components;
using BridgeGameCalculator.Shared.Models;

namespace BridgeGameCalculator.Tests.Components;

public sealed class HandDisplayTests : TestContext
{
    private static ParsedHand MakeHand(
        IReadOnlyList<Card>? spades   = null,
        IReadOnlyList<Card>? hearts   = null,
        IReadOnlyList<Card>? diamonds = null,
        IReadOnlyList<Card>? clubs    = null) =>
        new(
            spades   ?? [new Card(Suit.Spades,   Rank.Ace), new Card(Suit.Spades, Rank.King)],
            hearts   ?? [new Card(Suit.Hearts,   Rank.Queen)],
            diamonds ?? [new Card(Suit.Diamonds, Rank.Jack), new Card(Suit.Diamonds, Rank.Ten)],
            clubs    ?? [new Card(Suit.Clubs,    Rank.Nine)]);

    [Fact]
    public void Renders_four_suit_rows()
    {
        var cut = RenderComponent<HandDisplay>(p => p.Add(x => x.Hand, MakeHand()));

        Assert.Equal(4, cut.FindAll(".suit-row").Count);
    }

    [Fact]
    public void Spades_and_clubs_have_suit_black_class()
    {
        var cut = RenderComponent<HandDisplay>(p => p.Add(x => x.Hand, MakeHand()));

        var rows = cut.FindAll(".suit-row");
        Assert.Contains("suit-black", rows[0].InnerHtml);  // ♠ row
        Assert.Contains("suit-black", rows[3].InnerHtml);  // ♣ row
    }

    [Fact]
    public void Hearts_and_diamonds_have_suit_red_class()
    {
        var cut = RenderComponent<HandDisplay>(p => p.Add(x => x.Hand, MakeHand()));

        var rows = cut.FindAll(".suit-row");
        Assert.Contains("suit-red", rows[1].InnerHtml);  // ♥ row
        Assert.Contains("suit-red", rows[2].InnerHtml);  // ♦ row
    }

    [Fact]
    public void Void_suit_renders_dashes()
    {
        var hand = MakeHand(diamonds: Array.Empty<Card>());
        var cut  = RenderComponent<HandDisplay>(p => p.Add(x => x.Hand, hand));

        var rows = cut.FindAll(".suit-row");
        Assert.Contains("---", rows[2].TextContent);
    }

    [Fact]
    public void Cards_render_in_descending_rank_order()
    {
        IReadOnlyList<Card> spades =
        [
            new Card(Suit.Spades, Rank.Ace),
            new Card(Suit.Spades, Rank.King),
            new Card(Suit.Spades, Rank.Two),
        ];
        var hand = MakeHand(spades: spades);
        var cut  = RenderComponent<HandDisplay>(p => p.Add(x => x.Hand, hand));

        var spadesRow = cut.FindAll(".suit-row")[0].TextContent;
        int idxA = spadesRow.IndexOf('A');
        int idxK = spadesRow.IndexOf('K');
        int idx2 = spadesRow.IndexOf('2');

        Assert.True(idxA < idxK && idxK < idx2);
    }

    [Fact]
    public void Ten_renders_as_T()
    {
        IReadOnlyList<Card> spades = [new Card(Suit.Spades, Rank.Ten)];
        var hand = MakeHand(spades: spades);
        var cut  = RenderComponent<HandDisplay>(p => p.Add(x => x.Hand, hand));

        var spadesRow = cut.FindAll(".suit-row")[0].TextContent;
        Assert.Contains("T", spadesRow);
        Assert.DoesNotContain("10", spadesRow);
    }
}
