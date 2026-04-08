using BridgeGameCalculator.Shared.Models;
using BridgeGameCalculator.Shared.ViewModels;

namespace BridgeGameCalculator.Tests.ViewModels;

public sealed class BoardDetailViewModelFactoryTests
{
    // Minimal valid hands (3-3-4-3 for N/S, 4-4-3-2 for E/W — 52 cards total)
    private static readonly Hands SampleHands = new(
        North: "AKQ.AKQ.AKQJ.AKQ",
        East:  "JT98.JT98.T98.JT",
        South: "7654.7654.7654.98",
        West:  "32.32.32.76543 2".Replace(" ", string.Empty));  // remainder

    // Simpler fixed hands that are valid
    private static readonly Hands SimpleHands = new(
        North: "AKQ2.32.AKQ2.AK3",
        East:  "JT98.QJT9.J543.2",
        South: "7654.A876.T97.T9",
        West:  "3.K54.86.QJ87654");

    private static Board MakeBoard(
        int      boardNumber   = 1,
        Seat     dealer        = Seat.North,
        Vulnerability vul      = Vulnerability.None,
        Contract? contract     = null,
        Seat?    declarer      = null,
        int?     result        = null) =>
        new()
        {
            BoardNumber   = boardNumber,
            Dealer        = dealer,
            Vulnerability = vul,
            Hands         = SimpleHands,
            Contract      = contract,
            Declarer      = declarer,
            Result        = result,
        };

    private static BoardResult MakeBoardResult(
        string? parLabel  = "4S by N",
        int     parScore  = 420,
        int?    impDelta  = 2,
        int?    actualScore = 450) =>
        new(
            BoardNumber:       1,
            VulnerabilityLabel: "None",
            ContractPlayed:    "4S by N",
            TricksResult:      "+1",
            ActualScore:       actualScore,
            ParContractLabel:  parLabel,
            ParScore:          parScore,
            ImpDelta:          impDelta);

    [Fact]
    public void Create_NormalBoard_ContractDisplayContainsStrainSymbol()
    {
        var board = MakeBoard(
            contract:  new Contract(4, Strain.Spades, DoubleState.Undoubled),
            declarer:  Seat.South,
            result:    11);  // 4+6 = 10 bid, 11 made → +1

        var vm = BoardDetailViewModelFactory.Create(board, MakeBoardResult(), null, null, true);

        Assert.NotNull(vm.ContractDisplay);
        Assert.Contains("♠", vm.ContractDisplay);
        Assert.Contains("South", vm.ContractDisplay);
        Assert.Contains("+1", vm.ContractDisplay);
        Assert.Contains("+450 NS", vm.ContractDisplay);
    }

    [Fact]
    public void Create_PassedOutBoard_SetsIsPassedOutAndNullContractDisplay()
    {
        var board = MakeBoard();  // no contract → passed out

        var vm = BoardDetailViewModelFactory.Create(board, MakeBoardResult(), null, null, true);

        Assert.True(vm.IsPassedOut);
        Assert.Null(vm.ContractDisplay);
    }

    [Fact]
    public void Create_PassedOutBoard_ParDisplayIsPassZero()
    {
        var board = MakeBoard();
        var br    = MakeBoardResult(parLabel: "Pass", parScore: 0, impDelta: 0, actualScore: null);

        var vm = BoardDetailViewModelFactory.Create(board, br, null, null, true);

        Assert.Equal("Par: Pass (0)", vm.ParDisplay);
    }

    [Fact]
    public void Create_NullBoardResult_SetsAnalysisFailedAndNullPar()
    {
        var board = MakeBoard(
            contract: new Contract(3, Strain.NoTrump, DoubleState.Undoubled),
            declarer: Seat.North,
            result:   9);

        var vm = BoardDetailViewModelFactory.Create(board, null, null, null, true);

        Assert.True(vm.AnalysisFailed);
        Assert.Null(vm.ParDisplay);
        Assert.Null(vm.ImpDelta);
    }

    [Fact]
    public void Create_WithNavigation_SetsPrevAndNext()
    {
        var board = MakeBoard();

        var vm = BoardDetailViewModelFactory.Create(board, MakeBoardResult(), 3, 5, true);

        Assert.Equal(3, vm.PrevBoardNumber);
        Assert.Equal(5, vm.NextBoardNumber);
    }

    [Fact]
    public void Create_FirstBoard_PrevIsNull()
    {
        var board = MakeBoard();

        var vm = BoardDetailViewModelFactory.Create(board, MakeBoardResult(), null, 2, true);

        Assert.Null(vm.PrevBoardNumber);
        Assert.Equal(2, vm.NextBoardNumber);
    }

    [Fact]
    public void Create_NoSessionContext_HasSessionContextFalse()
    {
        var board = MakeBoard();

        var vm = BoardDetailViewModelFactory.Create(board, MakeBoardResult(), null, null, false);

        Assert.False(vm.HasSessionContext);
    }

    [Fact]
    public void Create_ParDisplayContainsStrainSymbol()
    {
        var board = MakeBoard(
            contract: new Contract(4, Strain.Spades, DoubleState.Undoubled),
            declarer: Seat.North,
            result:   10);

        var br = MakeBoardResult(parLabel: "4S by N", parScore: 420, impDelta: 0);
        var vm = BoardDetailViewModelFactory.Create(board, br, null, null, true);

        Assert.NotNull(vm.ParDisplay);
        Assert.Contains("♠", vm.ParDisplay);
        Assert.Contains("+420 NS", vm.ParDisplay);
    }

    [Fact]
    public void Create_BoardMetadata_IsPopulated()
    {
        var board = MakeBoard(boardNumber: 7, dealer: Seat.East, vul: Vulnerability.Both);
        var vm    = BoardDetailViewModelFactory.Create(board, MakeBoardResult(), null, null, true);

        Assert.Equal(7,     vm.BoardNumber);
        Assert.Equal("E",   vm.DealerLabel);
        Assert.Equal("Both", vm.VulnerabilityLabel);
    }

    [Fact]
    public void Create_HandsAreParsed()
    {
        var board = MakeBoard();
        var vm    = BoardDetailViewModelFactory.Create(board, MakeBoardResult(), null, null, true);

        Assert.True(vm.Hands.ContainsKey(Seat.North));
        Assert.True(vm.Hands.ContainsKey(Seat.East));
        Assert.True(vm.Hands.ContainsKey(Seat.South));
        Assert.True(vm.Hands.ContainsKey(Seat.West));
    }

    [Fact]
    public void Create_DoubledContract_ContractDisplayContainsX()
    {
        var board = MakeBoard(
            contract:  new Contract(4, Strain.Hearts, DoubleState.Doubled),
            declarer:  Seat.East,
            result:    10);

        var br = MakeBoardResult(actualScore: -300);
        var vm = BoardDetailViewModelFactory.Create(board, br, null, null, true);

        Assert.Contains("X", vm.ContractDisplay);
        Assert.Contains("♥", vm.ContractDisplay);
    }
}
