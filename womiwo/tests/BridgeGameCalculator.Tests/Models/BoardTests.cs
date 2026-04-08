namespace BridgeGameCalculator.Tests.Models;

using BridgeGameCalculator.Shared.Models;

public sealed class BoardTests
{
    [Fact]
    public void Board_WithContract_AllPropertiesSet()
    {
        var hands    = new Hands("AKQ.32.AKQ.AK3", "JT98.QJT.J543.2", "7654.A876.T976.T9", "3.K54.8.QJ876");
        var contract = new Contract(3, Strain.NoTrump, DoubleState.Undoubled);

        var board = new Board
        {
            BoardNumber   = 1,
            Dealer        = Seat.North,
            Vulnerability = Vulnerability.None,
            Hands         = hands,
            Contract      = contract,
            Declarer      = Seat.North,
            Result        = 9
        };

        Assert.Equal(1,                    board.BoardNumber);
        Assert.Equal(Seat.North,           board.Dealer);
        Assert.Equal(Vulnerability.None,   board.Vulnerability);
        Assert.Equal(contract,             board.Contract);
        Assert.Equal(Seat.North,           board.Declarer);
        Assert.Equal(9,                    board.Result);
        Assert.False(board.IsPassedOut);
    }

    [Fact]
    public void Board_PassedOut_NullablePropertiesAreNull()
    {
        var board = new Board
        {
            BoardNumber   = 2,
            Dealer        = Seat.East,
            Vulnerability = Vulnerability.Both,
            Hands         = new Hands(".", ".", ".", ".")
        };

        Assert.Null(board.Contract);
        Assert.Null(board.Declarer);
        Assert.Null(board.Result);
        Assert.True(board.IsPassedOut);
    }
}
