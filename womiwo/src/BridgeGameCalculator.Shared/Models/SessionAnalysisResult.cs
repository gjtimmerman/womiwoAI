namespace BridgeGameCalculator.Shared.Models;

/// <summary>
/// Fully assembled analysis result for a session, ready for the dashboard.
/// </summary>
public sealed class SessionAnalysisResult
{
    public required string                    SourceFile    { get; init; }
    public required int                       BoardCount    { get; init; }
    public required IReadOnlyList<BoardResult> BoardResults  { get; init; }

    /// <summary>Sum of all non-null IMP deltas.</summary>
    public required int TotalImps     { get; init; }
    /// <summary>Number of boards where ImpDelta > 0.</summary>
    public required int PositiveCount { get; init; }
    /// <summary>Number of boards where ImpDelta &lt; 0.</summary>
    public required int NegativeCount { get; init; }
    /// <summary>Number of boards where ImpDelta == 0 (result exists).</summary>
    public required int ParCount      { get; init; }

    public static SessionAnalysisResult Build(
        string sourceFile, IReadOnlyList<BoardResult> boardResults)
    {
        int total = 0, pos = 0, neg = 0, par = 0;
        foreach (var b in boardResults)
        {
            if (b.ImpDelta is null) continue;
            total += b.ImpDelta.Value;
            if      (b.ImpDelta > 0) pos++;
            else if (b.ImpDelta < 0) neg++;
            else                     par++;
        }
        return new SessionAnalysisResult
        {
            SourceFile    = sourceFile,
            BoardCount    = boardResults.Count,
            BoardResults  = boardResults,
            TotalImps     = total,
            PositiveCount = pos,
            NegativeCount = neg,
            ParCount      = par
        };
    }
}
