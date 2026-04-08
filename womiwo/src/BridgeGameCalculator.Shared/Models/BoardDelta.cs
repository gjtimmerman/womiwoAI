namespace BridgeGameCalculator.Shared.Models;

/// <summary>
/// Per-board delta: how the actual result compares to the par score, in IMPs.
/// All scores are from NS perspective (positive = NS gains).
/// </summary>
public sealed record BoardDelta(
    int  BoardNumber,
    int? ActualScore,   // null when no result was recorded
    int  ParScore,
    int? ImpDelta       // positive = NS outperformed par; null when no result
);
