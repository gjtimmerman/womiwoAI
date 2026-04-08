using Bunit;
using BridgeGameCalculator.Client.Pages;
using BridgeGameCalculator.Client.Services;
using BridgeGameCalculator.Shared.Models;
using Microsoft.Extensions.DependencyInjection;

namespace BridgeGameCalculator.Tests.Components;

public sealed class SessionDashboardTests : TestContext
{
    public SessionDashboardTests()
    {
        Services.AddSingleton<SessionState>();
    }

    [Fact]
    public void Shows_empty_state_when_no_session()
    {
        var cut = RenderComponent<SessionDashboard>();

        Assert.Contains("No session loaded", cut.Markup);
        Assert.Contains("/", cut.Markup);
    }

    [Fact]
    public void Shows_table_when_analysis_loaded()
    {
        var board = new BoardResult(1, "None", "3NT by N", "=", 400, "3NT by N", 400, 0);
        var analysis = new SessionAnalysisResult
        {
            SourceFile    = "session.pbn",
            BoardCount    = 1,
            BoardResults  = [board],
            TotalImps     = 0,
            PositiveCount = 0,
            NegativeCount = 0,
            ParCount      = 1
        };

        Services.GetRequiredService<SessionState>().CurrentAnalysis = analysis;

        var cut = RenderComponent<SessionDashboard>();

        Assert.Contains("session.pbn", cut.Markup);
        cut.FindAll("tbody tr").Count.Equals(1);
    }

    [Fact]
    public void Shows_one_row_per_board()
    {
        var boards = Enumerable.Range(1, 3).Select(i =>
            new BoardResult(i, "None", null, null, null, null, 0, null)).ToList();

        var analysis = new SessionAnalysisResult
        {
            SourceFile    = "test.pbn",
            BoardCount    = 3,
            BoardResults  = boards,
            TotalImps     = 0,
            PositiveCount = 0,
            NegativeCount = 0,
            ParCount      = 3
        };

        Services.GetRequiredService<SessionState>().CurrentAnalysis = analysis;

        var cut = RenderComponent<SessionDashboard>();

        Assert.Equal(3, cut.FindAll("tbody tr").Count);
    }
}
