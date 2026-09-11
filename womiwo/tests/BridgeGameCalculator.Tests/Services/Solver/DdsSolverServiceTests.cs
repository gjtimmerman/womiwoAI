using BridgeGameCalculator.Server.Dds;
using BridgeGameCalculator.Server.Services;
using BridgeGameCalculator.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace BridgeGameCalculator.Tests.Services.Solver
{
    public class DdsSolverServiceTests
    {
        [Fact]
        public async Task TestDdsSolverService_NormalHand()
        {
            DdsSolverService ddsSolverService = new DdsSolverService(NullLogger<DdsSolverService>.Instance);
            BoardPbn boardPbn = new BoardPbn();
            boardPbn.numberOfBoards = 1;
            boardPbn.deals = new DealPbn[boardPbn.numberOfBoards];
            boardPbn.deals[0] = new DealPbn();
            boardPbn.deals[0].trump = 0;
            boardPbn.deals[0].first = 0;
            boardPbn.deals[0].currentTrickRank = new int[3];
            boardPbn.deals[0].currentTrickSuit = new int[3];
            boardPbn.deals[0].remainingCards = "N:T642.K9.AKT62.82 Q87.T7632.J83.74 AJ3.J85.Q7.KQJT5 K95.AQ4.954.A963";
            boardPbn.solutions = new int[boardPbn.numberOfBoards];
            boardPbn.target = new int [boardPbn.numberOfBoards];
            boardPbn.mode = new int [boardPbn.numberOfBoards];
            BoardSolveResult result = await ddsSolverService.SolveSessionAsync(boardPbn);
        }
    }
}
