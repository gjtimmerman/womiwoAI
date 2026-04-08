namespace BridgeGameCalculator.Tests.Scoring;

using BridgeGameCalculator.Shared.Scoring;

public sealed class BridgeScorerImpTests
{
    // ---- ImpFromDifference boundaries ----

    [Theory]
    [InlineData(0,    0)]
    [InlineData(10,   0)]
    [InlineData(11,   1)]   // just above 0-IMP boundary
    [InlineData(20,   1)]
    [InlineData(40,   1)]
    [InlineData(41,   2)]
    [InlineData(50,   2)]
    [InlineData(80,   2)]
    [InlineData(81,   3)]
    [InlineData(90,   3)]
    [InlineData(120,  3)]
    [InlineData(130,  4)]
    [InlineData(160,  4)]
    [InlineData(430,  10)]
    [InlineData(490,  10)]
    [InlineData(500,  11)]
    [InlineData(590,  11)]
    [InlineData(600,  12)]
    [InlineData(740,  12)]
    [InlineData(750,  13)]
    [InlineData(890,  13)]
    [InlineData(900,  14)]
    [InlineData(2490, 20)]
    [InlineData(2491, 24)]  // first value above 20-IMP bracket
    [InlineData(2500, 24)]
    [InlineData(5000, 24)]
    public void ImpFromDifference_ReturnsCorrectValue(int diff, int expected)
        => Assert.Equal(expected, BridgeScorer.ImpFromDifference(diff));

    // ---- CalculateImpDelta ----

    [Theory]
    [InlineData(450,  420,  1)]    // +30 = 1 IMP ahead of par
    [InlineData(420,  420,  0)]    // exact par
    [InlineData(400,  420, -1)]    // -20 = 1 IMP behind par (20-IMP bracket = 1)
    [InlineData(-100, 600, -12)]   // -700 diff = 12 IMPs below par (600-740 bracket)
    [InlineData(-420, -420, 0)]    // EW played to par, NS 0 IMPs
    [InlineData(0,    0,    0)]    // passed out = 0 IMPs when par also 0
    [InlineData(0,    70,  -2)]    // missed 1C par (70 pts) = -2 IMPs (50-80 bracket)
    public void CalculateImpDelta_ReturnsCorrectValue(int? actual, int par, int? expected)
        => Assert.Equal(expected, BridgeScorer.CalculateImpDelta(actual, par));

    [Fact]
    public void CalculateImpDelta_NullActual_ReturnsNull()
        => Assert.Null(BridgeScorer.CalculateImpDelta(null, 420));
}
