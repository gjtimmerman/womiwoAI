namespace BridgeGameCalculator.Tests.Services.Analysis;

using BridgeGameCalculator.Server.Dds;
using BridgeGameCalculator.Server.Services;
using BridgeGameCalculator.Shared.Models;

/// <summary>
/// Tests the pure mapping helpers in DdsAnalysisService without calling the
/// real DDS library. Uses InternalsVisibleTo to access internal methods.
/// </summary>
public sealed class DdsAnalysisServiceMappingTests
{
    // ---- ToDdsDeal ----

    [Fact]
    public void ToDdsDeal_FormatsPbnDealString()
    {
        var board = new Board
        {
            BoardNumber   = 1,
            Dealer        = Seat.North,
            Vulnerability = Vulnerability.None,
            Hands         = new Hands(
                "AKQ2.32.AKQ2.AK3",
                "JT98.QJT9.J543.2",
                "7654.A876.T97.T9",
                "3.K54.86.QJ87654")
        };

        var deal = DdsAnalysisService.ToDdsDeal(board);

        Assert.Equal(
            "N:AKQ2.32.AKQ2.AK3 JT98.QJT9.J543.2 7654.A876.T97.T9 3.K54.86.QJ87654",
            deal.Cards);
    }

    // ---- MapDdTable ----

    [Fact]
    public void MapDdTable_MapsAllTwentyCells_Correctly()
    {
        // ResTable[i] = i % 10, keeping all values in valid 0-9 range.
        // This still lets us verify the indexing formula strain*4+hand.
        var ddsRes = new DdTableResults
        {
            ResTable = Enumerable.Range(0, 20).Select(i => i % 10).ToArray()
        };

        var table = DdsAnalysisService.MapDdTable(3, ddsRes);

        Assert.Equal(3,  table.BoardNumber);
        Assert.Equal(20, table.Results.Count);

        // Spot-check: ResTable[strain*4+hand] % 10
        Assert.Equal(0, table.GetTricks(Seat.North, Strain.Spades));  // [0*4+0]=0  %10=0
        Assert.Equal(1, table.GetTricks(Seat.East,  Strain.Spades));  // [0*4+1]=1  %10=1
        Assert.Equal(4, table.GetTricks(Seat.North, Strain.Hearts));  // [1*4+0]=4  %10=4
        Assert.Equal(6, table.GetTricks(Seat.North, Strain.NoTrump)); // [4*4+0]=16 %10=6
        Assert.Equal(9, table.GetTricks(Seat.West,  Strain.NoTrump)); // [4*4+3]=19 %10=9
    }

    // ---- MapVulnerability ----

    [Theory]
    [InlineData(Vulnerability.None,       0)]
    [InlineData(Vulnerability.Both,       1)]
    [InlineData(Vulnerability.NorthSouth, 2)]
    [InlineData(Vulnerability.EastWest,   3)]
    public void MapVulnerability_MapsToCorrectDdsInt(Vulnerability vul, int expected)
    {
        Assert.Equal(expected, DdsAnalysisService.MapVulnerability(vul));
    }

    // ---- MapParResult ----

    [Fact]
    public void MapParResult_SetsScoreAndContracts()
    {
        var parRes = new ParResultsDealer
        {
            Score      = [420, -420],
            Contracts0 = "4S-N",
            Contracts1 = "4S*-E"
        };

        var result = DdsAnalysisService.MapParResult(2, parRes);

        Assert.Equal(2,   result.BoardNumber);
        Assert.Equal(420, result.ParScore);      // NS score
        Assert.Single(result.ParContracts);
        Assert.Equal(Seat.North, result.ParContracts[0].Declarer);
    }

    [Fact]
    public void MapParResult_PassedOut_ReturnsEmptyContracts()
    {
        var parRes = new ParResultsDealer
        {
            Score      = [0, 0],
            Contracts0 = "pass",
            Contracts1 = "pass"
        };

        var result = DdsAnalysisService.MapParResult(5, parRes);

        Assert.Equal(0, result.ParScore);
        Assert.Empty(result.ParContracts);
    }
}
