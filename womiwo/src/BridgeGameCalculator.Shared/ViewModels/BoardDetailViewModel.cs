namespace BridgeGameCalculator.Shared.ViewModels;

using BridgeGameCalculator.Shared.Models;

/// <summary>
/// Flattened, display-ready read model for the board detail page.
/// Assembled by <see cref="BoardDetailViewModelFactory"/> from domain entities.
/// </summary>
public sealed record BoardDetailViewModel
{
    public required int                        BoardNumber        { get; init; }
    public required string                     DealerLabel        { get; init; }
    public required string                     VulnerabilityLabel { get; init; }
    public required Dictionary<Seat, ParsedHand> Hands            { get; init; }

    /// <summary>Full contract line, e.g. "4♠ by South, +1, +650 NS". Null when passed out.</summary>
    public string? ContractDisplay { get; init; }

    /// <summary>Par line, e.g. "Par: 4♠ by N = +620 NS". Null when analysis failed.</summary>
    public string? ParDisplay      { get; init; }

    /// <summary>IMP delta, null when there is no result or analysis failed.</summary>
    public int?    ImpDelta        { get; init; }

    public bool IsPassedOut     { get; init; }
    public bool AnalysisFailed  { get; init; }

    public int?  PrevBoardNumber   { get; init; }
    public int?  NextBoardNumber   { get; init; }
    public bool  HasSessionContext { get; init; }
}
