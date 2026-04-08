namespace BridgeGameCalculator.Shared.ViewModels;

using System.Text;
using BridgeGameCalculator.Shared.Models;
using BridgeGameCalculator.Shared.Parsing;

/// <summary>
/// Assembles a <see cref="BoardDetailViewModel"/> from domain entities.
/// Static — no dependencies; works in both session and single-hand contexts.
/// </summary>
public static class BoardDetailViewModelFactory
{
    public static BoardDetailViewModel Create(
        Board       board,
        BoardResult? boardResult,
        int?        prevBoardNumber,
        int?        nextBoardNumber,
        bool        hasSessionContext)
    {
        var hands = PbnDealParser.ParseAllHands(board.Hands);

        return new BoardDetailViewModel
        {
            BoardNumber        = board.BoardNumber,
            DealerLabel        = FormatSeatAbbr(board.Dealer),
            VulnerabilityLabel = FormatVulnerability(board.Vulnerability),
            Hands              = hands,
            ContractDisplay    = FormatContractDisplay(board, boardResult),
            ParDisplay         = FormatParDisplay(board, boardResult),
            ImpDelta           = boardResult?.ImpDelta,
            IsPassedOut        = board.IsPassedOut,
            AnalysisFailed     = boardResult is null,
            PrevBoardNumber    = prevBoardNumber,
            NextBoardNumber    = nextBoardNumber,
            HasSessionContext  = hasSessionContext,
        };
    }

    // -------------------------------------------------------------------------

    private static string? FormatContractDisplay(Board board, BoardResult? boardResult)
    {
        if (board.IsPassedOut || board.Contract is null) return null;

        var sb = new StringBuilder();
        sb.Append(board.Contract.Level);
        sb.Append(StrainSymbol(board.Contract.Strain));
        if (board.Contract.DoubleState == DoubleState.Doubled)    sb.Append('X');
        if (board.Contract.DoubleState == DoubleState.Redoubled)  sb.Append("XX");

        if (board.Declarer.HasValue)
            sb.Append($" by {FormatSeatFull(board.Declarer.Value)}");

        if (board.Result.HasValue)
        {
            int over = board.Result.Value - (6 + board.Contract.Level);
            sb.Append(over switch { 0 => ", =", > 0 => $", +{over}", _ => $", {over}" });
        }

        if (boardResult?.ActualScore is int score)
            sb.Append(score >= 0 ? $", +{score} NS" : $", {score} NS");

        return sb.ToString();
    }

    private static string? FormatParDisplay(Board board, BoardResult? boardResult)
    {
        if (boardResult is null) return null;

        if (board.IsPassedOut || boardResult.ParContractLabel is null
                              || boardResult.ParContractLabel == "Pass")
            return "Par: Pass (0)";

        string label    = EnhanceParLabel(boardResult.ParContractLabel);
        int    parScore = boardResult.ParScore;
        string scoreStr = parScore >= 0 ? $"+{parScore}" : $"{parScore}";
        return $"Par: {label} = {scoreStr} NS";
    }

    /// <summary>
    /// Converts abbreviated par label ("4S by N") to the display form ("4♠ by N").
    /// Only the strain letter is replaced; seat abbreviations are left as-is.
    /// </summary>
    private static string EnhanceParLabel(string label)
    {
        var parts = label.Split(" by ", 2, StringSplitOptions.None);
        if (parts.Length != 2) return label;

        string contractPart = ReplaceStrainAbbr(parts[0]);
        return $"{contractPart} by {parts[1]}";
    }

    private static string ReplaceStrainAbbr(string contractStr)
    {
        // Format: digit + strainChar + optional X/XX, e.g. "4S", "3NT", "4HX"
        if (contractStr.Length < 2) return contractStr;
        return contractStr[1] switch
        {
            'S' => contractStr[0] + "\u2660" + contractStr[2..],
            'H' => contractStr[0] + "\u2665" + contractStr[2..],
            'D' => contractStr[0] + "\u2666" + contractStr[2..],
            'C' => contractStr[0] + "\u2663" + contractStr[2..],
            _   => contractStr  // NT — leave unchanged
        };
    }

    // -------------------------------------------------------------------------
    // Formatting helpers
    // -------------------------------------------------------------------------

    private static string StrainSymbol(Strain s) => s switch
    {
        Strain.Spades   => "\u2660",
        Strain.Hearts   => "\u2665",
        Strain.Diamonds => "\u2666",
        Strain.Clubs    => "\u2663",
        Strain.NoTrump  => "NT",
        _               => "?"
    };

    private static string FormatSeatAbbr(Seat s) => s switch
    {
        Seat.North => "N",
        Seat.East  => "E",
        Seat.South => "S",
        Seat.West  => "W",
        _          => "?"
    };

    private static string FormatSeatFull(Seat s) => s switch
    {
        Seat.North => "North",
        Seat.East  => "East",
        Seat.South => "South",
        Seat.West  => "West",
        _          => "?"
    };

    private static string FormatVulnerability(Vulnerability v) => v switch
    {
        Vulnerability.NorthSouth => "NS",
        Vulnerability.EastWest   => "EW",
        Vulnerability.Both       => "Both",
        _                        => "None"
    };
}
