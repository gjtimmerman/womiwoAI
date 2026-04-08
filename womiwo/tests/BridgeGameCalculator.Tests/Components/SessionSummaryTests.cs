using Bunit;
using BridgeGameCalculator.Client.Components;
using BridgeGameCalculator.Shared.Models;

namespace BridgeGameCalculator.Tests.Components;

public sealed class SessionSummaryTests : TestContext
{
    private static SessionAnalysisResult MakeAnalysis(int totalImps, int pos, int neg, int par) =>
        new()
        {
            SourceFile     = "test.pbn",
            BoardCount     = pos + neg + par,
            BoardResults   = [],
            TotalImps      = totalImps,
            PositiveCount  = pos,
            NegativeCount  = neg,
            ParCount       = par
        };

    [Fact]
    public void Renders_board_count()
    {
        var analysis = MakeAnalysis(totalImps: 5, pos: 3, neg: 1, par: 2);
        var cut = RenderComponent<SessionSummary>(p => p.Add(x => x.Analysis, analysis));

        Assert.Contains("6", cut.Markup);
    }

    [Theory]
    [InlineData(10,  "delta-positive", "+10")]
    [InlineData(-7,  "delta-negative", "-7")]
    [InlineData(0,   "delta-neutral",  "0")]
    public void Total_imp_value_has_correct_class_and_text(int totalImps, string expectedCss, string expectedText)
    {
        var analysis = MakeAnalysis(totalImps: totalImps, pos: 1, neg: 1, par: 0);
        var cut = RenderComponent<SessionSummary>(p => p.Add(x => x.Analysis, analysis));

        var valueSpans = cut.FindAll(".summary-value");
        var totalSpan  = valueSpans[0];  // first summary-value is Total IMPs

        Assert.Contains(expectedCss, totalSpan.ClassName ?? "");
        Assert.Equal(expectedText, totalSpan.TextContent);
    }

    [Fact]
    public void Renders_positive_negative_par_counts()
    {
        var analysis = MakeAnalysis(totalImps: 3, pos: 5, neg: 2, par: 1);
        var cut = RenderComponent<SessionSummary>(p => p.Add(x => x.Analysis, analysis));

        var valueSpans = cut.FindAll(".summary-value");
        // Order: TotalImps, BoardCount, Positive, Negative, Par
        Assert.Equal("5",  valueSpans[2].TextContent);
        Assert.Equal("2",  valueSpans[3].TextContent);
        Assert.Equal("1",  valueSpans[4].TextContent);
    }
}
