namespace BridgeGameCalculator.Tests.Models;

using BridgeGameCalculator.Shared.Models;

public sealed class DdTableTests
{
    private static IReadOnlyList<DdResult> Make20Results(int tricks = 8)
    {
        var list = new List<DdResult>(20);
        for (int strain = 0; strain < 5; strain++)
            for (int hand = 0; hand < 4; hand++)
                list.Add(new DdResult((Seat)hand, (Strain)strain, tricks));
        return list;
    }

    [Fact]
    public void DdTable_With20Results_Constructs()
    {
        var table = new DdTable(1, Make20Results(9));
        Assert.Equal(1,  table.BoardNumber);
        Assert.Equal(20, table.Results.Count);
    }

    [Fact]
    public void DdTable_With19Results_Throws()
    {
        var results = Make20Results().Take(19).ToList();
        Assert.Throws<ArgumentException>(() => new DdTable(1, results));
    }

    [Fact]
    public void DdTable_With21Results_Throws()
    {
        var results = Make20Results().ToList();
        results.Add(new DdResult(Seat.North, Strain.Spades, 7));
        Assert.Throws<ArgumentException>(() => new DdTable(1, results));
    }

    [Fact]
    public void GetTricks_ReturnsCorrectValue()
    {
        // Assign unique values per (strain, hand) pair, capped at 13.
        // Index formula: strain * 4 + hand, mod 10 to stay in [0,9].
        var results = new List<DdResult>(20);
        for (int strain = 0; strain < 5; strain++)
            for (int hand = 0; hand < 4; hand++)
                results.Add(new DdResult((Seat)hand, (Strain)strain, (strain * 4 + hand) % 10));

        var table = new DdTable(5, results);

        Assert.Equal(0, table.GetTricks(Seat.North, Strain.Spades));  // 0*4+0=0  mod 10 = 0
        Assert.Equal(1, table.GetTricks(Seat.East,  Strain.Spades));  // 0*4+1=1  mod 10 = 1
        Assert.Equal(4, table.GetTricks(Seat.North, Strain.Hearts));  // 1*4+0=4  mod 10 = 4
        Assert.Equal(6, table.GetTricks(Seat.North, Strain.NoTrump)); // 4*4+0=16 mod 10 = 6
        Assert.Equal(9, table.GetTricks(Seat.West,  Strain.NoTrump)); // 4*4+3=19 mod 10 = 9
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(14)]
    public void DdResult_OutOfRangeTricks_Throws(int tricks)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DdResult(Seat.North, Strain.Spades, tricks));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(13)]
    public void DdResult_ValidTricks_Constructs(int tricks)
    {
        var r = new DdResult(Seat.South, Strain.Hearts, tricks);
        Assert.Equal(tricks, r.Tricks);
    }

    [Fact]
    public void ParResult_PassedOut_ZeroScoreAndEmptyContracts()
    {
        var par = ParResult.PassedOut(3);
        Assert.Equal(3, par.BoardNumber);
        Assert.Equal(0, par.ParScore);
        Assert.Empty(par.ParContracts);
    }

    [Fact]
    public void BoardAnalysisResult_Success_SetsProperties()
    {
        var table   = new DdTable(1, Make20Results(8));
        var parRes  = new ParResult { BoardNumber = 1, ParScore = 420, ParContracts = [] };
        var result  = BoardAnalysisResult.Success(table, parRes);

        Assert.True(result.IsSuccess);
        Assert.Equal(1,   result.BoardNumber);
        Assert.Equal(420, result.ParResult!.ParScore);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void BoardAnalysisResult_Failure_SetsProperties()
    {
        var result = BoardAnalysisResult.Failure(7, "DDS exploded");

        Assert.False(result.IsSuccess);
        Assert.Equal(7,            result.BoardNumber);
        Assert.Equal("DDS exploded", result.ErrorMessage);
        Assert.Null(result.DdTable);
    }
}
