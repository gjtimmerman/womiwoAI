namespace BridgeGameCalculator.Server.Services;

using BridgeGameCalculator.Server.Dds;
using BridgeGameCalculator.Shared.Models;

/// <summary>
/// Analyses bridge boards using the DDS native library via P/Invoke.
/// Register as a singleton — DDS manages its own internal thread pool.
/// </summary>
public sealed class DdsAnalysisService : IDdsAnalysisService
{
    private static readonly int[] TrumpFilterAll = [0, 0, 0, 0, 0]; // calculate all strains

    private readonly ILogger<DdsAnalysisService> _logger;

    public DdsAnalysisService(ILogger<DdsAnalysisService> logger)
    {
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public async Task<IReadOnlyList<BoardAnalysisResult>> AnalyzeSessionAsync(
        IReadOnlyList<Board> boards,
        CancellationToken    cancellationToken = default)
    {
        return await Task.Run(() => AnalyzeSessionCore(boards, cancellationToken),
                              cancellationToken);
    }

    public async Task<BoardAnalysisResult> AnalyzeBoardAsync(
        Board             board,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => AnalyzeBoardCore(board), cancellationToken);
    }

    // -------------------------------------------------------------------------
    // Core (synchronous, run on a background thread)
    // -------------------------------------------------------------------------

    private IReadOnlyList<BoardAnalysisResult> AnalyzeSessionCore(
        IReadOnlyList<Board> boards,
        CancellationToken    cancellationToken)
    {
        var results   = new BoardAnalysisResult[boards.Count];
        int count     = boards.Count;

        // --- Build batch input ---
        var dealsPbn = new DdTableDealsPbn
        {
            NoOfTables = count,
            Deals      = new DdTableDealPbn[DdsConstants.MaxNoOfBoards * 5]
        };
        for (int i = 0; i < count; i++)
            dealsPbn.Deals[i] = ToDdsDeal(boards[i]);

        // --- Batch DD table calculation ---
        var tablesRes  = new DdTablesRes  { Results      = new DdTableResults [DdsConstants.MaxNoOfBoards * 5] };
        var allParRes  = new AllParResults { PresResults  = new ParResultsDealer[DdsConstants.MaxNoOfBoards * 5] };

        int rc = DdsInterop.CalcAllTablesPBN(
            ref dealsPbn, mode: 0, TrumpFilterAll, ref tablesRes, ref allParRes);

        if (!DdsErrorHelper.IsSuccess(rc))
        {
            var errorMsg = DdsErrorHelper.GetErrorMessage(rc);
            _logger.LogError("DDS CalcAllTablesPBN failed: {Error}", errorMsg);
            // Per-board failure: return failure for every board
            for (int i = 0; i < count; i++)
                results[i] = BoardAnalysisResult.Failure(boards[i].BoardNumber, errorMsg);
            return results;
        }

        // --- Per-board par calculation ---
        for (int i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results[i] = ComputeParForBoard(boards[i], ref tablesRes.Results[i]);
        }

        return results;
    }

    private BoardAnalysisResult AnalyzeBoardCore(Board board)
    {
        var deal      = ToDdsDeal(board);
        var tableRes  = new DdTableResults { ResTable = new int[DdsConstants.DdsSuits * DdsConstants.DdsHands] };

        int rc = DdsInterop.CalcDDtablePBN(deal, ref tableRes);
        if (!DdsErrorHelper.IsSuccess(rc))
        {
            var errorMsg = DdsErrorHelper.GetErrorMessage(rc);
            _logger.LogError("DDS CalcDDtablePBN failed for board {Board}: {Error}",
                             board.BoardNumber, errorMsg);
            return BoardAnalysisResult.Failure(board.BoardNumber, errorMsg);
        }

        return ComputeParForBoard(board, ref tableRes);
    }

    private BoardAnalysisResult ComputeParForBoard(Board board, ref DdTableResults tableRes)
    {
        try
        {
            var ddTable = MapDdTable(board.BoardNumber, tableRes);

            var parRes    = new ParResultsDealer
            {
                Score      = new int[2],
                Contracts0 = string.Empty,
                Contracts1 = string.Empty
            };
            int rc = DdsInterop.DealerPar(
                ref tableRes, ref parRes,
                (int)board.Dealer,
                MapVulnerability(board.Vulnerability));

            ParResult parResult;
            if (!DdsErrorHelper.IsSuccess(rc))
            {
                _logger.LogWarning("DDS DealerPar failed for board {Board}: {Error}",
                                   board.BoardNumber, DdsErrorHelper.GetErrorMessage(rc));
                parResult = ParResult.PassedOut(board.BoardNumber);
            }
            else
            {
                parResult = MapParResult(board.BoardNumber, parRes);
            }

            return BoardAnalysisResult.Success(ddTable, parResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error analysing board {Board}", board.BoardNumber);
            return BoardAnalysisResult.Failure(board.BoardNumber, ex.Message);
        }
    }

    // -------------------------------------------------------------------------
    // Mapping helpers (internal for testability via InternalsVisibleTo)
    // -------------------------------------------------------------------------

    internal static DdTableDealPbn ToDdsDeal(Board board) =>
        new() { Cards = $"N:{board.Hands.North} {board.Hands.East} {board.Hands.South} {board.Hands.West}" };

    internal static DdTable MapDdTable(int boardNumber, DdTableResults ddsResults)
    {
        // ResTable layout: [strain * 4 + hand]
        // strain: 0=S, 1=H, 2=D, 3=C, 4=NT   (matches Strain enum)
        // hand:   0=N, 1=E, 2=S, 3=W          (matches Seat enum)
        var results = new List<DdResult>(20);
        for (int strain = 0; strain < 5; strain++)
        {
            for (int hand = 0; hand < 4; hand++)
            {
                int tricks = ddsResults.ResTable[strain * 4 + hand];
                results.Add(new DdResult((Seat)hand, (Strain)strain, tricks));
            }
        }
        return new DdTable(boardNumber, results);
    }

    internal static ParResult MapParResult(int boardNumber, ParResultsDealer parRes)
    {
        var contracts = DdsParContractParser.Parse(parRes.Contracts0);
        return new ParResult
        {
            BoardNumber  = boardNumber,
            ParScore     = parRes.Score[0],  // NS perspective
            ParContracts = contracts
        };
    }

    internal static int MapVulnerability(Vulnerability vulnerability) =>
        vulnerability switch
        {
            Vulnerability.None        => 0,
            Vulnerability.Both        => 1,
            Vulnerability.NorthSouth  => 2,
            Vulnerability.EastWest    => 3,
            _                         => throw new ArgumentOutOfRangeException(nameof(vulnerability))
        };
}
