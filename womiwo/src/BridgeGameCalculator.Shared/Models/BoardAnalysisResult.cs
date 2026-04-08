namespace BridgeGameCalculator.Shared.Models;

/// <summary>
/// DD analysis result for one board — either a success (with DdTable + ParResult)
/// or a per-board failure (with error message). A failure on one board never
/// aborts analysis of the remaining boards.
/// </summary>
public sealed class BoardAnalysisResult
{
    public int      BoardNumber  { get; init; }
    public bool     IsSuccess    { get; init; }
    public DdTable? DdTable      { get; init; }
    public ParResult? ParResult  { get; init; }
    public string?  ErrorMessage { get; init; }

    public static BoardAnalysisResult Success(DdTable ddTable, ParResult parResult) =>
        new()
        {
            BoardNumber = ddTable.BoardNumber,
            IsSuccess   = true,
            DdTable     = ddTable,
            ParResult   = parResult
        };

    public static BoardAnalysisResult Failure(int boardNumber, string errorMessage) =>
        new()
        {
            BoardNumber  = boardNumber,
            IsSuccess    = false,
            ErrorMessage = errorMessage
        };
}
