namespace BridgeGameCalculator.Shared.Models;

/// <summary>
/// Par score and par contract(s) for one board, from NS perspective.
/// </summary>
public sealed class ParResult
{
    public required int                     BoardNumber  { get; init; }
    /// <summary>Par score in points, from North-South perspective.</summary>
    public required int                     ParScore     { get; init; }
    public required IReadOnlyList<ParContract> ParContracts { get; init; }

    /// <summary>Creates a passed-out par result (score = 0, no contracts).</summary>
    public static ParResult PassedOut(int boardNumber) =>
        new() { BoardNumber = boardNumber, ParScore = 0, ParContracts = [] };
}
