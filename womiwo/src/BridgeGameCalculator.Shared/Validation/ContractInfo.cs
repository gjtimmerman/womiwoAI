namespace BridgeGameCalculator.Shared.Validation;

using BridgeGameCalculator.Shared.Models;

/// <summary>
/// Optional contract fields submitted alongside a hand for validation.
/// All properties may be null when the user did not enter a contract.
/// </summary>
public sealed record ContractInfo(
    int?        Level,
    Strain?     Strain,
    DoubleState? Doubled,
    Seat?       Declarer,
    int?        Result);
