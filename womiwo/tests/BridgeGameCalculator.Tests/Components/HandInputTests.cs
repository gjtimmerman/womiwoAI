using Bunit;
using BridgeGameCalculator.Client.Components;
using BridgeGameCalculator.Shared.Models;

namespace BridgeGameCalculator.Tests.Components;

public sealed class HandInputTests : TestContext
{
    [Fact]
    public void Renders_Label_ForSeat()
    {
        var cut = RenderComponent<HandInput>(p =>
        {
            p.Add(x => x.Seat,  Seat.North);
            p.Add(x => x.Value, "");
        });

        Assert.Contains("North", cut.Markup);
    }

    [Theory]
    [InlineData(Seat.North, "North")]
    [InlineData(Seat.East,  "East")]
    [InlineData(Seat.South, "South")]
    [InlineData(Seat.West,  "West")]
    public void Renders_Correct_Label_For_Each_Seat(Seat seat, string expectedLabel)
    {
        var cut = RenderComponent<HandInput>(p =>
        {
            p.Add(x => x.Seat,  seat);
            p.Add(x => x.Value, "");
        });

        Assert.Contains(expectedLabel, cut.Markup);
    }

    [Fact]
    public void Shows_ErrorMessage_When_Set()
    {
        var cut = RenderComponent<HandInput>(p =>
        {
            p.Add(x => x.Seat,         Seat.South);
            p.Add(x => x.Value,        "");
            p.Add(x => x.ErrorMessage, "Hand must have 13 cards.");
        });

        Assert.Contains("Hand must have 13 cards.", cut.Markup);
        Assert.NotNull(cut.Find(".validation-error"));
    }

    [Fact]
    public void Does_Not_Show_ErrorMessage_When_Null()
    {
        var cut = RenderComponent<HandInput>(p =>
        {
            p.Add(x => x.Seat,         Seat.West);
            p.Add(x => x.Value,        "");
            p.Add(x => x.ErrorMessage, (string?)null);
        });

        Assert.Throws<Bunit.ElementNotFoundException>(() => cut.Find(".validation-error"));
    }

    [Fact]
    public void Input_ErrorClass_When_ErrorMessage_Set()
    {
        var cut = RenderComponent<HandInput>(p =>
        {
            p.Add(x => x.Seat,         Seat.East);
            p.Add(x => x.Value,        "");
            p.Add(x => x.ErrorMessage, "Error");
        });

        var input = cut.Find("input");
        Assert.Contains("input-error", input.ClassName ?? "");
    }

    [Fact]
    public void ValueChanged_Fires_On_Change()
    {
        string? received = null;
        var cut = RenderComponent<HandInput>(p =>
        {
            p.Add(x => x.Seat,         Seat.North);
            p.Add(x => x.Value,        "");
            p.Add(x => x.ValueChanged, s => received = s);
        });

        cut.Find("input").Change("AKQ.JT9.87.65432");

        Assert.Equal("AKQ.JT9.87.65432", received);
    }

    [Fact]
    public void Disabled_Input_Has_Disabled_Attribute()
    {
        var cut = RenderComponent<HandInput>(p =>
        {
            p.Add(x => x.Seat,     Seat.North);
            p.Add(x => x.Value,    "");
            p.Add(x => x.Disabled, true);
        });

        Assert.True(cut.Find("input").HasAttribute("disabled"));
    }
}
