namespace BridgeGameCalculator.Shared.Models;

/// <summary>
/// One par contract (there may be multiple equivalent par contracts for a board).
/// </summary>
public sealed record ParContract(
    int         Level,
    Strain      Strain,
    Seat        Declarer,
    DoubleState DoubleState);
