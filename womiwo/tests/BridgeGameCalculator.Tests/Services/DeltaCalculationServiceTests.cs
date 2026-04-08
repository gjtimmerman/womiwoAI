namespace BridgeGameCalculator.Tests.Services;

using BridgeGameCalculator.Server.Services;
using BridgeGameCalculator.Shared.Models;

public sealed class DeltaCalculationServiceTests
{
    private readonly DeltaCalculationService _svc = new();

    // Shared deal (valid, not used by scorer)
    private static readonly Hands AnyHands =
        new("AKQ2.32.AKQ2.AK3", "JT98.QJT9.J543.2", "7654.A876.T97.T9", "3.K54.86.QJ87654");

    private static Board MakeBoard(
        int boardNumber, Seat dealer, Vulnerability vul,
        Contract? contract = null, Seat? declarer = null, int? result = null)
        => new()
        {
            BoardNumber   = boardNumber,
            Dealer        = dealer,
            Vulnerability = vul,
            Hands         = AnyHands,
            Contract      = contract,
            Declarer      = declarer,
            Result        = result
        };

    private static ParResult MakePar(int boardNumber, int parScore,
        IReadOnlyList<ParContract>? contracts = null)
        => new() { BoardNumber = boardNumber, ParScore = parScore, ParContracts = contracts ?? [] };

    // ---- SC-001: NS overtrick above par ----
    // 4S by N NV, making 11 = +450. Par = +420. Delta = +1 IMP (30-pt diff).
    [Fact]
    public void SC001_NS_Overtrick_AbovePar_Plus1Imp()
    {
        var board = MakeBoard(1, Seat.North, Vulnerability.None,
            new Contract(4, Strain.Spades, DoubleState.Undoubled), Seat.North, 11);
        var par   = MakePar(1, 420);

        var delta = _svc.CalculateDelta(board, par);

        Assert.Equal(1,   delta.BoardNumber);
        Assert.Equal(450, delta.ActualScore);
        Assert.Equal(420, delta.ParScore);
        Assert.Equal(1,   delta.ImpDelta);
    }

    // ---- SC-002: NS goes down, loses to par ----
    // 3NT by N V, making 8 (down 1) = -100. Par = +600. Delta = -12 IMPs (700-pt diff).
    [Fact]
    public void SC002_NS_GoesDown_LosesToPar_Minus12Imp()
    {
        var board = MakeBoard(2, Seat.North, Vulnerability.NorthSouth,
            new Contract(3, Strain.NoTrump, DoubleState.Undoubled), Seat.North, 8);
        var par   = MakePar(2, 600);

        var delta = _svc.CalculateDelta(board, par);

        Assert.Equal(-100, delta.ActualScore);
        Assert.Equal(-12,  delta.ImpDelta);
    }

    // ---- SC-003: EW declaring, matches par ----
    // 4H by E NV, making 10 = EW +420 = NS -420. Par = -420. Delta = 0 IMPs.
    [Fact]
    public void SC003_EW_Declarer_MatchesPar_ZeroImp()
    {
        var board = MakeBoard(3, Seat.East, Vulnerability.None,
            new Contract(4, Strain.Hearts, DoubleState.Undoubled), Seat.East, 10);
        var par   = MakePar(3, -420);

        var delta = _svc.CalculateDelta(board, par);

        Assert.Equal(-420, delta.ActualScore);
        Assert.Equal(0,    delta.ImpDelta);
    }

    // ---- SC-004: Passed-out board ----
    [Fact]
    public void SC004_PassedOut_ActualScoreZero_ImpBasedOnParDiff()
    {
        var board = MakeBoard(4, Seat.South, Vulnerability.Both); // no contract
        var par   = MakePar(4, 0);

        var delta = _svc.CalculateDelta(board, par);

        Assert.Equal(0, delta.ActualScore);
        Assert.Equal(0, delta.ParScore);
        Assert.Equal(0, delta.ImpDelta);
    }

    // ---- SC-005: Contract bid, no result ----
    [Fact]
    public void SC005_NoResult_ActualScoreAndImpDeltaNull()
    {
        var board = MakeBoard(5, Seat.North, Vulnerability.None,
            new Contract(3, Strain.NoTrump, DoubleState.Undoubled), Seat.North, null);
        var par   = MakePar(5, 400);

        var delta = _svc.CalculateDelta(board, par);

        Assert.Null(delta.ActualScore);
        Assert.Null(delta.ImpDelta);
    }

    // ---- Batch calculation ----
    [Fact]
    public void CalculateDeltas_MatchesByBoardNumber()
    {
        var boards = new List<Board>
        {
            MakeBoard(1, Seat.North, Vulnerability.None,
                new Contract(4, Strain.Spades, DoubleState.Undoubled), Seat.North, 10),
            MakeBoard(2, Seat.North, Vulnerability.None,
                new Contract(3, Strain.NoTrump, DoubleState.Undoubled), Seat.North, 8),
            MakeBoard(3, Seat.North, Vulnerability.None) // passed out
        };

        var parResults = new List<ParResult>
        {
            MakePar(1, 420),
            MakePar(2, 400),
            MakePar(3, 0)
        };

        var deltas = _svc.CalculateDeltas(boards, parResults);

        Assert.Equal(3, deltas.Count);
        Assert.Equal(1, deltas[0].BoardNumber);
        Assert.Equal(2, deltas[1].BoardNumber);
        Assert.Equal(3, deltas[2].BoardNumber);
        Assert.Equal(0,   deltas[0].ImpDelta); // 420 vs par 420 = 0 IMPs
        Assert.Equal(-10, deltas[1].ImpDelta); // -50 vs par 400 → diff -450 → 10 IMPs = -10
        Assert.Equal(0,   deltas[2].ImpDelta); // passed out, par 0 → 0 IMPs
    }
}
