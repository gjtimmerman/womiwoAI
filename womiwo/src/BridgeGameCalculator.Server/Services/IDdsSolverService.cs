using BridgeGameCalculator.Shared.Dtos;
using BridgeGameCalculator.Shared.Models;

namespace BridgeGameCalculator.Server.Services
{
    public interface IDdsSolverService
    {
        Task<BoardSolveResult> SolveSessionAsync(
        SingleHandSolveRequest request,
        CancellationToken cancellationToken = default);

        Task<BoardSolveResult> SolveBoardAsync(
            BoardPbn board,
            CancellationToken cancellationToken = default);
    }
}
