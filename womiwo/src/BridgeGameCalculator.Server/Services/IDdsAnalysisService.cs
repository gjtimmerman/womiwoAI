namespace BridgeGameCalculator.Server.Services;

using BridgeGameCalculator.Shared.Models;

/// <summary>
/// Calculates double-dummy analysis for bridge boards using the DDS library.
/// </summary>
public interface IDdsAnalysisService
{
    /// <summary>
    /// Analyze all boards in a session using the DDS batch API.
    /// Per-board failures are captured in the result — never thrown.
    /// </summary>
    Task<IReadOnlyList<BoardAnalysisResult>> AnalyzeSessionAsync(
        IReadOnlyList<Board> boards,
        CancellationToken    cancellationToken = default);

    /// <summary>
    /// Analyze a single board. Used for single-hand entry (FEAT-006).
    /// </summary>
    Task<BoardAnalysisResult> AnalyzeBoardAsync(
        Board             board,
        CancellationToken cancellationToken = default);
}
