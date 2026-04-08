namespace BridgeGameCalculator.Shared.Parsing;

using BridgeGameCalculator.Shared.Models;

/// <summary>
/// Result of parsing a single hand string via <see cref="HandParser"/>.
/// </summary>
public sealed class HandParseResult
{
    public bool    IsSuccess { get; init; }
    public string? Error     { get; init; }

    // Set on success:
    public IReadOnlyList<Card>? Spades   { get; init; }
    public IReadOnlyList<Card>? Hearts   { get; init; }
    public IReadOnlyList<Card>? Diamonds { get; init; }
    public IReadOnlyList<Card>? Clubs    { get; init; }

    /// <summary>All 13 cards across all suits. Set on success only.</summary>
    public IReadOnlyList<Card>? AllCards { get; init; }

    /// <summary>
    /// Normalized PBN dot-format string (e.g. "AKQ.JT9.87.654"). Set on success only.
    /// </summary>
    public string? PbnHand { get; init; }

    public static HandParseResult Failure(string error) =>
        new() { IsSuccess = false, Error = error };
}
