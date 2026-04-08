using Bunit;
using BridgeGameCalculator.Client.Components;
using BridgeGameCalculator.Shared.Models;

namespace BridgeGameCalculator.Tests.Components;

public sealed class HandDiagramTests : TestContext
{
    private static ParsedHand EmptyHand() => new([], [], [], []);

    private static Dictionary<Seat, ParsedHand> FourEmptyHands() =>
        new()
        {
            [Seat.North] = EmptyHand(),
            [Seat.East]  = EmptyHand(),
            [Seat.South] = EmptyHand(),
            [Seat.West]  = EmptyHand(),
        };

    [Fact]
    public void Renders_four_HandDisplay_components()
    {
        var cut = RenderComponent<HandDiagram>(p =>
        {
            p.Add(x => x.Hands,              FourEmptyHands());
            p.Add(x => x.BoardNumber,        3);
            p.Add(x => x.DealerLabel,        "N");
            p.Add(x => x.VulnerabilityLabel, "None");
        });

        Assert.Equal(4, cut.FindAll(".hand-display").Count);
    }

    [Fact]
    public void Renders_board_metadata_in_center()
    {
        var cut = RenderComponent<HandDiagram>(p =>
        {
            p.Add(x => x.Hands,              FourEmptyHands());
            p.Add(x => x.BoardNumber,        7);
            p.Add(x => x.DealerLabel,        "E");
            p.Add(x => x.VulnerabilityLabel, "Both");
        });

        var info = cut.Find(".board-info");
        Assert.Contains("7",    info.TextContent);
        Assert.Contains("E",    info.TextContent);
        Assert.Contains("Both", info.TextContent);
    }

    [Fact]
    public void Has_hand_diagram_css_class()
    {
        var cut = RenderComponent<HandDiagram>(p =>
        {
            p.Add(x => x.Hands,              FourEmptyHands());
            p.Add(x => x.BoardNumber,        1);
            p.Add(x => x.DealerLabel,        "S");
            p.Add(x => x.VulnerabilityLabel, "NS");
        });

        Assert.NotNull(cut.Find(".hand-diagram"));
    }
}
