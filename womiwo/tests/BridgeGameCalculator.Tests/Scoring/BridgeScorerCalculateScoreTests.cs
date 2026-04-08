namespace BridgeGameCalculator.Tests.Scoring;

using BridgeGameCalculator.Shared.Models;
using BridgeGameCalculator.Shared.Scoring;

/// <summary>
/// Tests for BridgeScorer.CalculateScore covering WBF duplicate scoring rules.
/// All scores from NS perspective (positive = NS gains).
/// Declarer is North unless otherwise noted.
/// </summary>
public sealed class BridgeScorerCalculateScoreTests
{
    private static int Score(
        int level, Strain strain, DoubleState doubled,
        int tricks, Vulnerability vul = Vulnerability.None,
        Seat declarer = Seat.North)
        => BridgeScorer.CalculateScore(
               new Contract(level, strain, doubled), declarer, vul, tricks);

    // ---- Part scores (undoubled, NV) ----

    [Theory]
    [InlineData(1, Strain.Clubs,    7, 70)]    // 20×1 trick + 50 partial
    [InlineData(1, Strain.Diamonds, 7, 70)]
    [InlineData(2, Strain.Hearts,   8, 110)]   // 30×2 + 50 partial
    [InlineData(2, Strain.Spades,   8, 110)]
    [InlineData(1, Strain.NoTrump,  7, 90)]    // 40 + 50 partial
    public void PartScore_Undoubled_NV(int level, Strain strain, int tricks, int expected)
        => Assert.Equal(expected, Score(level, strain, DoubleState.Undoubled, tricks));

    // ---- Game contracts (undoubled, NV) ----

    [Theory]
    [InlineData(3, Strain.NoTrump, 9,  400)]  // (40+30+30)=100 + 300
    [InlineData(4, Strain.Spades,  10, 420)]  // 30×4=120 + 300
    [InlineData(4, Strain.Hearts,  10, 420)]
    [InlineData(5, Strain.Diamonds,11, 400)]  // 20×5=100 + 300
    [InlineData(5, Strain.Clubs,   11, 400)]
    public void Game_Undoubled_NV(int level, Strain strain, int tricks, int expected)
        => Assert.Equal(expected, Score(level, strain, DoubleState.Undoubled, tricks));

    // ---- Game contracts (undoubled, V) ----

    [Theory]
    [InlineData(3, Strain.NoTrump, 9,  600)]  // 100 + 500
    [InlineData(4, Strain.Spades,  10, 620)]  // 120 + 500
    [InlineData(4, Strain.Hearts,  10, 620)]
    [InlineData(5, Strain.Clubs,   11, 600)]  // 100 + 500
    public void Game_Undoubled_V(int level, Strain strain, int tricks, int expected)
        => Assert.Equal(expected, Score(level, strain, DoubleState.Undoubled, tricks,
                                        Vulnerability.NorthSouth));

    // ---- Slam bonuses (undoubled) ----

    [Theory]
    [InlineData(6, Strain.Hearts,   12, Vulnerability.None,        980)]   // 180+300+500
    [InlineData(7, Strain.NoTrump,  13, Vulnerability.None,       1520)]   // 220+300+1000
    [InlineData(6, Strain.Hearts,   12, Vulnerability.NorthSouth, 1430)]   // 180+500+750
    [InlineData(7, Strain.NoTrump,  13, Vulnerability.NorthSouth, 2220)]   // 220+500+1500
    public void Slam_Undoubled(int level, Strain strain, int tricks, Vulnerability vul, int expected)
        => Assert.Equal(expected, Score(level, strain, DoubleState.Undoubled, tricks, vul));

    // ---- Overtricks (undoubled) ----

    [Theory]
    [InlineData(3, Strain.NoTrump, 10, Vulnerability.None,       430)]  // 400 + 30 overtrick
    [InlineData(4, Strain.Spades,  11, Vulnerability.None,       450)]  // 420 + 30 overtrick
    [InlineData(4, Strain.Clubs,   12, Vulnerability.None,       170)]  // 80 part + 50 + 2×20 OT
    [InlineData(3, Strain.NoTrump, 10, Vulnerability.NorthSouth, 630)]  // 600 + 30 overtrick
    public void Overtricks_Undoubled(int level, Strain strain, int tricks, Vulnerability vul, int expected)
        => Assert.Equal(expected, Score(level, strain, DoubleState.Undoubled, tricks, vul));

    // ---- Going down (undoubled) ----

    [Theory]
    [InlineData(3, Strain.NoTrump, 8,  Vulnerability.None,       -50)]   // down 1 NV
    [InlineData(3, Strain.NoTrump, 8,  Vulnerability.NorthSouth, -100)]  // down 1 V
    [InlineData(3, Strain.NoTrump, 6,  Vulnerability.None,       -150)]  // down 3 NV = 3×50
    [InlineData(3, Strain.NoTrump, 6,  Vulnerability.NorthSouth, -300)]  // down 3 V  = 3×100
    public void GoingDown_Undoubled(int level, Strain strain, int tricks, Vulnerability vul, int expected)
        => Assert.Equal(expected, Score(level, strain, DoubleState.Undoubled, tricks, vul));

    // ---- Doubled making ----

    [Theory]
    [InlineData(2, Strain.Spades, 8,  Vulnerability.None,       470)]  // 120+300+50+0 overtricks
    [InlineData(4, Strain.Hearts, 10, Vulnerability.NorthSouth, 790)]  // 240+500+50
    [InlineData(4, Strain.Hearts, 11, Vulnerability.None,       690)]  // 240+300+50+100 NV OT
    [InlineData(4, Strain.Hearts, 11, Vulnerability.NorthSouth, 990)]  // 240+500+50+200 V OT
    public void Doubled_Making(int level, Strain strain, int tricks, Vulnerability vul, int expected)
        => Assert.Equal(expected, Score(level, strain, DoubleState.Doubled, tricks, vul));

    // ---- Doubled going down ----

    [Theory]
    [InlineData(3, Strain.NoTrump, 8,  Vulnerability.None,        -100)]  // down 1 NV
    [InlineData(3, Strain.NoTrump, 8,  Vulnerability.NorthSouth,  -200)]  // down 1 V
    [InlineData(3, Strain.NoTrump, 7,  Vulnerability.None,        -300)]  // down 2 NV: 100+200
    [InlineData(3, Strain.NoTrump, 6,  Vulnerability.None,        -500)]  // down 3 NV: 100+200+200
    [InlineData(3, Strain.NoTrump, 5,  Vulnerability.None,        -800)]  // down 4 NV: 100+200+200+300
    [InlineData(3, Strain.NoTrump, 7,  Vulnerability.NorthSouth,  -500)]  // down 2 V: 200+300
    [InlineData(3, Strain.NoTrump, 6,  Vulnerability.NorthSouth,  -800)]  // down 3 V: 200+300+300
    public void Doubled_GoingDown(int level, Strain strain, int tricks, Vulnerability vul, int expected)
        => Assert.Equal(expected, Score(level, strain, DoubleState.Doubled, tricks, vul));

    // ---- Redoubled making ----

    [Fact]
    public void Redoubled_2S_NV_MakingExactly_Returns640()
        // 2×30×4=240 (game), NV bonus 300, insult 100, 0 OT
        => Assert.Equal(640, Score(2, Strain.Spades, DoubleState.Redoubled, 8));

    [Fact]
    public void Redoubled_GoingDown1_NV_Returns200()
        // 200 (redoubled down 1 NV)
        => Assert.Equal(-200, Score(3, Strain.Spades, DoubleState.Redoubled, 8));

    // ---- EW declaring (NS-perspective negation) ----

    [Fact]
    public void EWDeclarer_4S_NV_Making10_ReturnsNegative420()
        // Raw = +420; NS perspective = -420
        => Assert.Equal(-420,
               Score(4, Strain.Spades, DoubleState.Undoubled, 10,
                     Vulnerability.None, Seat.East));

    [Fact]
    public void EWDeclarer_3NT_V_Down1_ReturnsPositive100()
        // Raw EW penalty = -100; NS perspective = +100
        => Assert.Equal(100,
               Score(3, Strain.NoTrump, DoubleState.Undoubled, 8,
                     Vulnerability.EastWest, Seat.West));

    // ---- Vulnerability only applies to declaring side ----

    [Fact]
    public void NSVulnerable_EWDeclarer_IsNotVulnerable()
    {
        // EW are NOT vul when Vulnerability=NS. Down 1 undoubled = -50 (NV), NS gets +50.
        int score = Score(3, Strain.NoTrump, DoubleState.Undoubled, 8,
                          Vulnerability.NorthSouth, Seat.East);
        Assert.Equal(50, score);
    }
}
