namespace BridgeGameCalculator.Client.Services;

using BridgeGameCalculator.Shared.Models;
using BridgeGameCalculator.Shared.ViewModels;

/// <summary>
/// Holds the currently-loaded session in memory and provides board detail view models.
/// In Blazor WASM, singleton = per-browser-tab lifetime.
/// </summary>
public sealed class SessionState : ISessionStateService
{
    public Session?               CurrentSession  { get; set; }
    public SessionAnalysisResult? CurrentAnalysis { get; set; }

    public bool HasSession => CurrentSession is not null && CurrentAnalysis is not null;

    /// <summary>
    /// True when the current session was created by single-hand entry (FEAT-006).
    /// Causes <see cref="GetBoardDetail"/> to set HasSessionContext=false so the
    /// board detail page hides "Back to session" and prev/next navigation.
    /// Reset to false whenever a PBN session is loaded.
    /// </summary>
    public bool IsSingleHandSession { get; set; }

    public IReadOnlyList<int> BoardNumbers =>
        CurrentSession?.Boards.Select(b => b.BoardNumber).ToList()
        ?? (IReadOnlyList<int>)Array.Empty<int>();

    public BoardResult? GetBoard(int boardNumber) =>
        CurrentAnalysis?.BoardResults.FirstOrDefault(b => b.BoardNumber == boardNumber);

    public BoardDetailViewModel? GetBoardDetail(int boardNumber)
    {
        if (CurrentSession is null || CurrentAnalysis is null) return null;

        var board = CurrentSession.Boards.FirstOrDefault(b => b.BoardNumber == boardNumber);
        if (board is null) return null;

        var boardResult = CurrentAnalysis.BoardResults
            .FirstOrDefault(b => b.BoardNumber == boardNumber);

        var numbers = CurrentSession.Boards.Select(b => b.BoardNumber).ToList();
        int idx     = numbers.IndexOf(boardNumber);
        int? prev   = idx > 0                  ? numbers[idx - 1] : null;
        int? next   = idx < numbers.Count - 1  ? numbers[idx + 1] : null;

        return BoardDetailViewModelFactory.Create(
            board, boardResult, prev, next,
            hasSessionContext: !IsSingleHandSession);
    }
}
