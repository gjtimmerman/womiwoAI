namespace BridgeGameCalculator.Tests.Fakes;

using BridgeGameCalculator.Server.Services;
using BridgeGameCalculator.Shared.Models;

/// <summary>
/// Fake IDdsAnalysisService for unit tests — no DDS library needed.
/// </summary>
public sealed class FakeDdsAnalysisService : IDdsAnalysisService
{
    private readonly Dictionary<int, BoardAnalysisResult> _results = new();
    private readonly BoardAnalysisResult?                 _default;

    public FakeDdsAnalysisService(BoardAnalysisResult? defaultResult = null)
    {
        _default = defaultResult;
    }

    public void SetResult(int boardNumber, BoardAnalysisResult result)
        => _results[boardNumber] = result;

    public Task<IReadOnlyList<BoardAnalysisResult>> AnalyzeSessionAsync(
        IReadOnlyList<Board> boards,
        CancellationToken    cancellationToken = default)
    {
        IReadOnlyList<BoardAnalysisResult> results = boards
            .Select(b => _results.TryGetValue(b.BoardNumber, out var r) ? r
                         : _default ?? BoardAnalysisResult.Failure(b.BoardNumber, "no fake configured"))
            .ToList();
        return Task.FromResult(results);
    }

    public Task<BoardAnalysisResult> AnalyzeBoardAsync(
        Board             board,
        CancellationToken cancellationToken = default)
    {
        var r = _results.TryGetValue(board.BoardNumber, out var result) ? result
                : _default ?? BoardAnalysisResult.Failure(board.BoardNumber, "no fake configured");
        return Task.FromResult(r);
    }
}
