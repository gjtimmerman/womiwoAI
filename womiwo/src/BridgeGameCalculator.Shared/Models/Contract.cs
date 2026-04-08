namespace BridgeGameCalculator.Shared.Models;

/// <summary>
/// A bridge contract. Level is 1–7, Strain is the trump suit or NoTrump,
/// DoubleState indicates whether the contract was doubled or redoubled.
/// </summary>
public sealed record Contract(int Level, Strain Strain, DoubleState DoubleState);
