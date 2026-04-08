using Bunit;
using BridgeGameCalculator.Client.Pages;
using BridgeGameCalculator.Client.Services;
using BridgeGameCalculator.Shared.Models;
using BridgeGameCalculator.Shared.ViewModels;
using BridgeGameCalculator.Tests.Fakes;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace BridgeGameCalculator.Tests.Components;

public sealed class BoardDetailTests : TestContext
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

    private static BoardDetailViewModel MakeViewModel(
        int  boardNumber       = 5,
        int? impDelta          = null,
        bool isPassedOut       = false,
        bool analysisFailed    = false,
        bool hasSessionContext = true,
        int? prevBoardNumber   = null,
        int? nextBoardNumber   = null,
        string? contractDisplay = "3NT by North, =, +400 NS",
        string? parDisplay      = "Par: 3NT by N = +400 NS") =>
        new()
        {
            BoardNumber        = boardNumber,
            DealerLabel        = "N",
            VulnerabilityLabel = "None",
            Hands              = FourEmptyHands(),
            ContractDisplay    = contractDisplay,
            ParDisplay         = parDisplay,
            ImpDelta           = impDelta,
            IsPassedOut        = isPassedOut,
            AnalysisFailed     = analysisFailed,
            PrevBoardNumber    = prevBoardNumber,
            NextBoardNumber    = nextBoardNumber,
            HasSessionContext  = hasSessionContext,
        };

    private FakeSessionStateService SetupFake(BoardDetailViewModel? vm = null)
    {
        var fake = new FakeSessionStateService();
        if (vm is not null) fake.SetBoard(vm);
        Services.AddSingleton<ISessionStateService>(fake);
        return fake;
    }

    [Fact]
    public void NoSession_ShowsNotFoundMessage()
    {
        Services.AddSingleton<ISessionStateService>(new FakeSessionStateService());

        var cut = RenderComponent<BoardDetail>(p => p.Add(x => x.BoardNumber, 99));

        Assert.Contains("not found", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WithSessionContext_ShowsNavigationControls()
    {
        SetupFake(MakeViewModel(prevBoardNumber: 4, nextBoardNumber: 6));

        var cut = RenderComponent<BoardDetail>(p => p.Add(x => x.BoardNumber, 5));

        Assert.NotNull(cut.Find(".board-nav"));
        Assert.Contains("Back to session", cut.Markup);
    }

    [Fact]
    public void WithoutSessionContext_HidesNavigationControls()
    {
        SetupFake(MakeViewModel(hasSessionContext: false));

        var cut = RenderComponent<BoardDetail>(p => p.Add(x => x.BoardNumber, 5));

        Assert.Throws<Bunit.ElementNotFoundException>(() => cut.Find(".board-nav"));
    }

    [Fact]
    public void WithSessionContext_FirstBoard_PrevButtonDisabled()
    {
        SetupFake(MakeViewModel(prevBoardNumber: null, nextBoardNumber: 6));

        var cut     = RenderComponent<BoardDetail>(p => p.Add(x => x.BoardNumber, 5));
        var buttons = cut.FindAll(".nav-arrows button");

        Assert.True(buttons[0].HasAttribute("disabled"));   // ← Prev
        Assert.False(buttons[1].HasAttribute("disabled"));  // Next →
    }

    [Fact]
    public void WithSessionContext_LastBoard_NextButtonDisabled()
    {
        SetupFake(MakeViewModel(prevBoardNumber: 4, nextBoardNumber: null));

        var cut     = RenderComponent<BoardDetail>(p => p.Add(x => x.BoardNumber, 5));
        var buttons = cut.FindAll(".nav-arrows button");

        Assert.False(buttons[0].HasAttribute("disabled"));  // ← Prev
        Assert.True(buttons[1].HasAttribute("disabled"));   // Next →
    }

    [Fact]
    public void PassedOutBoard_ShowsPassedOut()
    {
        SetupFake(MakeViewModel(isPassedOut: true, contractDisplay: null, parDisplay: "Par: Pass (0)"));

        var cut = RenderComponent<BoardDetail>(p => p.Add(x => x.BoardNumber, 5));

        Assert.Contains("Passed out", cut.Markup);
    }

    [Fact]
    public void NormalBoard_ShowsContractAndPar()
    {
        SetupFake(MakeViewModel(impDelta: 0));

        var cut = RenderComponent<BoardDetail>(p => p.Add(x => x.BoardNumber, 5));

        Assert.Contains("3NT by North", cut.Markup);
        Assert.Contains("Par:", cut.Markup);
    }

    [Fact]
    public void ImpDelta_Positive_HasGreenClass()
    {
        SetupFake(MakeViewModel(impDelta: 3));

        var cut = RenderComponent<BoardDetail>(p => p.Add(x => x.BoardNumber, 5));

        var delta = cut.Find(".delta-positive");
        Assert.Contains("+3", delta.TextContent);
    }

    [Fact]
    public void ImpDelta_Negative_HasRedClass()
    {
        SetupFake(MakeViewModel(impDelta: -5));

        var cut = RenderComponent<BoardDetail>(p => p.Add(x => x.BoardNumber, 5));

        var delta = cut.Find(".delta-negative");
        Assert.Contains("-5", delta.TextContent);
    }

    [Fact]
    public void ImpDelta_Zero_HasNeutralClass()
    {
        SetupFake(MakeViewModel(impDelta: 0));

        var cut = RenderComponent<BoardDetail>(p => p.Add(x => x.BoardNumber, 5));

        Assert.NotNull(cut.Find(".delta-neutral"));
    }

    [Fact]
    public void ImpDelta_Null_ShowsNA()
    {
        SetupFake(MakeViewModel(impDelta: null));

        var cut = RenderComponent<BoardDetail>(p => p.Add(x => x.BoardNumber, 5));

        Assert.Contains("N/A", cut.Markup);
    }

    [Fact]
    public void AnalysisFailed_ShowsUnavailable()
    {
        SetupFake(MakeViewModel(analysisFailed: true, parDisplay: null, impDelta: null));

        var cut = RenderComponent<BoardDetail>(p => p.Add(x => x.BoardNumber, 5));

        Assert.Contains("Analysis unavailable", cut.Markup);
    }

    [Fact]
    public void NextButton_Click_NavigatesToNextBoard()
    {
        SetupFake(MakeViewModel(prevBoardNumber: 4, nextBoardNumber: 6));
        var navMan = Services.GetRequiredService<NavigationManager>();

        var cut = RenderComponent<BoardDetail>(p => p.Add(x => x.BoardNumber, 5));
        var buttons = cut.FindAll(".nav-arrows button");
        buttons[1].Click();  // Next →

        Assert.EndsWith("/boards/6", navMan.Uri);
    }

    [Fact]
    public void PrevButton_Click_NavigatesToPrevBoard()
    {
        SetupFake(MakeViewModel(prevBoardNumber: 4, nextBoardNumber: 6));
        var navMan = Services.GetRequiredService<NavigationManager>();

        var cut = RenderComponent<BoardDetail>(p => p.Add(x => x.BoardNumber, 5));
        var buttons = cut.FindAll(".nav-arrows button");
        buttons[0].Click();  // ← Prev

        Assert.EndsWith("/boards/4", navMan.Uri);
    }
}
