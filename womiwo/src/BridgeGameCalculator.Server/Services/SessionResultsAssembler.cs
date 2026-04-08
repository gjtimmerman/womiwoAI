namespace BridgeGameCalculator.Server.Services;

using BridgeGameCalculator.Shared.Models;

/// <summary>
/// Assembles a <see cref="SessionAnalysisResult"/> from domain objects produced by
/// the DDS analysis service, the delta calculation service, and the parsed session.
/// </summary>
public static class SessionResultsAssembler
{
    public static SessionAnalysisResult Assemble(
        Session                          session,
        IReadOnlyList<BoardAnalysisResult> analysisResults,
        IReadOnlyList<BoardDelta>          deltas)
    {
        var analysisByBoard = analysisResults.ToDictionary(r => r.BoardNumber);
        var deltaByBoard    = deltas.ToDictionary(d => d.BoardNumber);

        var boardResults = session.Boards
            .Select(board =>
            {
                analysisByBoard.TryGetValue(board.BoardNumber, out var analysis);
                deltaByBoard.TryGetValue(board.BoardNumber, out var delta);

                var parResult = analysis?.ParResult;
                return new BoardResult(
                    BoardNumber:       board.BoardNumber,
                    VulnerabilityLabel: FormatVulnerability(board.Vulnerability),
                    ContractPlayed:    FormatContract(board),
                    TricksResult:      FormatTricksResult(board),
                    ActualScore:       delta?.ActualScore,
                    ParContractLabel:  FormatParContract(parResult),
                    ParScore:          parResult?.ParScore ?? 0,
                    ImpDelta:          delta?.ImpDelta);
            })
            .ToList();

        return SessionAnalysisResult.Build(session.SourceFile, boardResults);
    }

    // -------------------------------------------------------------------------
    // Formatting helpers
    // -------------------------------------------------------------------------

    internal static string FormatVulnerability(Vulnerability v) => v switch
    {
        Vulnerability.None        => "None",
        Vulnerability.NorthSouth  => "NS",
        Vulnerability.EastWest    => "EW",
        Vulnerability.Both        => "Both",
        _                         => "None"
    };

    internal static string? FormatContract(Board board)
    {
        if (board.Contract is null) return "Pass";
        if (board.Declarer is null) return FormatContractOnly(board.Contract);
        return $"{FormatContractOnly(board.Contract)} by {FormatSeat(board.Declarer.Value)}";
    }

    internal static string? FormatTricksResult(Board board)
    {
        if (board.Contract is null || board.Result is null) return null;
        int over = board.Result.Value - (6 + board.Contract.Level);
        return over switch { 0 => "=", > 0 => $"+{over}", _ => $"{over}" };
    }

    internal static string? FormatParContract(ParResult? parResult)
    {
        if (parResult is null || parResult.ParContracts.Count == 0) return "Pass";
        var c = parResult.ParContracts[0];
        return $"{FormatContractOnly(new Contract(c.Level, c.Strain, c.DoubleState))} by {FormatSeat(c.Declarer)}";
    }

    private static string FormatContractOnly(Contract contract)
    {
        var doubled = contract.DoubleState switch
        {
            DoubleState.Doubled    => "X",
            DoubleState.Redoubled  => "XX",
            _                      => string.Empty
        };
        return $"{contract.Level}{FormatStrain(contract.Strain)}{doubled}";
    }

    private static string FormatStrain(Strain strain) => strain switch
    {
        Strain.Spades    => "S",
        Strain.Hearts    => "H",
        Strain.Diamonds  => "D",
        Strain.Clubs     => "C",
        Strain.NoTrump   => "NT",
        _                => "?"
    };

    private static string FormatSeat(Seat seat) => seat switch
    {
        Seat.North => "N",
        Seat.East  => "E",
        Seat.South => "S",
        Seat.West  => "W",
        _          => "?"
    };
}
