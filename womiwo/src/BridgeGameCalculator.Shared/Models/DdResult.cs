namespace BridgeGameCalculator.Shared.Models;

/// <summary>
/// Double-dummy trick count for one (declarer, strain) combination.
/// </summary>
public sealed record DdResult(Seat Declarer, Strain Strain, int Tricks)
{
    public int Tricks { get; init; } = Tricks is >= 0 and <= 13
        ? Tricks
        : throw new ArgumentOutOfRangeException(nameof(Tricks), Tricks,
              "Tricks must be 0–13.");
}
