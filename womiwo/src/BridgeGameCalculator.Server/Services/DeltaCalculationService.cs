namespace BridgeGameCalculator.Server.Services;

using BridgeGameCalculator.Shared.Models;
using BridgeGameCalculator.Shared.Scoring;

/// <summary>
/// Orchestrates actual-vs-par delta calculation for bridge boards.
/// Stateless — safe to register as singleton.
/// </summary>
public sealed class DeltaCalculationService
{
    /// <summary>
    /// Compute the <see cref="BoardDelta"/> for a single board.
    /// </summary>
    public BoardDelta CalculateDelta(Board board, ParResult parResult)
    {
        if (board.Contract is null)
        {
            // Passed out: actual score = 0
            int? impDelta = BridgeScorer.CalculateImpDelta(0, parResult.ParScore);
            return new BoardDelta(board.BoardNumber, 0, parResult.ParScore, impDelta);
        }

        if (board.Result is null)
        {
            // Contract bid but no result recorded — cannot compute delta
            return new BoardDelta(board.BoardNumber, null, parResult.ParScore, null);
        }

        int actualScore = BridgeScorer.CalculateScore(
            board.Contract,
            board.Declarer!.Value,
            board.Vulnerability,
            board.Result.Value);

        int? imp = BridgeScorer.CalculateImpDelta(actualScore, parResult.ParScore);
        return new BoardDelta(board.BoardNumber, actualScore, parResult.ParScore, imp);
    }

    /// <summary>
    /// Compute deltas for all boards, matching by <see cref="Board.BoardNumber"/>.
    /// </summary>
    public IReadOnlyList<BoardDelta> CalculateDeltas(
        IReadOnlyList<Board>      boards,
        IReadOnlyList<ParResult>  parResults)
    {
        var parByBoard = parResults.ToDictionary(p => p.BoardNumber);
        var deltas     = new List<BoardDelta>(boards.Count);

        foreach (var board in boards)
        {
            if (!parByBoard.TryGetValue(board.BoardNumber, out var parResult))
                parResult = ParResult.PassedOut(board.BoardNumber); // safe fallback

            deltas.Add(CalculateDelta(board, parResult));
        }

        return deltas;
    }
}
