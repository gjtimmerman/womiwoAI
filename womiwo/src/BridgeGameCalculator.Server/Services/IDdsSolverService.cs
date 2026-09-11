using BridgeGameCalculator.Shared.Models;

namespace BridgeGameCalculator.Server.Services
{
    public interface IDdsSolverService
    {
        Task<BoardSolveResult> SolveSessionAsync(
        BoardPbn boards,
        CancellationToken cancellationToken = default);

        Task<BoardSolveResult> SolveBoardAsync(
            BoardPbn board,
            CancellationToken cancellationToken = default);
    }
}
