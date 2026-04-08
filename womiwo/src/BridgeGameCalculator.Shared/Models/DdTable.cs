namespace BridgeGameCalculator.Shared.Models;

/// <summary>
/// Full double-dummy trick table for one board: 5 strains × 4 declarers = 20 results.
/// </summary>
public sealed class DdTable
{
    public int BoardNumber { get; init; }

    /// <summary>Exactly 20 results — one per (declarer, strain) pair.</summary>
    public IReadOnlyList<DdResult> Results { get; init; }

    public DdTable(int boardNumber, IReadOnlyList<DdResult> results)
    {
        if (results.Count != 20)
            throw new ArgumentException(
                $"DdTable requires exactly 20 results, got {results.Count}.", nameof(results));

        BoardNumber = boardNumber;
        Results     = results;
    }

    /// <summary>Returns the number of tricks makeable by the given declarer in the given strain.</summary>
    public int GetTricks(Seat declarer, Strain strain) =>
        Results.First(r => r.Declarer == declarer && r.Strain == strain).Tricks;
}
