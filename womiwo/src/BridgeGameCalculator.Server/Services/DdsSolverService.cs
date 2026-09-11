using BridgeGameCalculator.Server.Dds;
using BridgeGameCalculator.Shared.Models;


namespace BridgeGameCalculator.Server.Services
{
    public class DdsSolverService : IDdsSolverService
    {
        ILogger<DdsSolverService> _logger;
        public DdsSolverService(ILogger<DdsSolverService> logger)
        {
            _logger = logger;
        }

        public async Task<BoardSolveResult> SolveSessionAsync(
            BoardPbn boards,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => SolveSessionCore(boards, cancellationToken));
        }

        private BoardSolveResult SolveSessionCore(BoardPbn boards, CancellationToken cancellationToken)
        { 
            BoardsPBN boardsPBN = new();
            boardsPBN.numberOfBoards = boards.numberOfBoards;
            boardsPBN.deals = new DealPBN[DdsConstants.MaxNoOfBoards];
            boardsPBN.solutions = new int[DdsConstants.MaxNoOfBoards];
            boardsPBN.target = new int[DdsConstants.MaxNoOfBoards];
            for (int i = 0; i < boards.numberOfBoards; i++)
            {
                boardsPBN.deals[i] = new DealPBN();
                boardsPBN.deals[i].trump = boards.deals[i].trump;
                boardsPBN.deals[i].first = boards.deals[i].first;
                boardsPBN.deals[i].currentTrickRank = new int[3];
                for (int j = 0; j < 3; j++)
                {
                    boardsPBN.deals[i].currentTrickRank[j] = boards.deals[i].currentTrickRank[j];
                }
                boardsPBN.deals[i].currentTrickSuit = new int[3];
                for (int j = 0; j < 3; j++)
                {
                    boardsPBN.deals[i].currentTrickSuit[j] = boards.deals[i].currentTrickSuit[j];
                }

                boardsPBN.deals[i].remainingCards = boards.deals[i].remainingCards;
            }
            SolvedBoardsInterop solvedBoards = new();
            solvedBoards.solved_board = new Dds.FutureTricks[DdsConstants.MaxNoOfBoards];
            for (int i = 0; i < DdsConstants.MaxNoOfBoards; i++)
            {
                solvedBoards.solved_board[i].rank = new int[DdsConstants.NumberOfTricks];
                solvedBoards.solved_board[i].suit = new int[DdsConstants.NumberOfTricks];
                solvedBoards.solved_board[i].equals = new int[DdsConstants.NumberOfTricks];
                solvedBoards.solved_board[i].score = new int[DdsConstants.NumberOfTricks];
            }
            DdsInterop.SolveAllBoards(boardsPBN, solvedBoards);
            BoardSolveResult boardSolveResult = new BoardSolveResult();
            return boardSolveResult;
        }

        public Task<BoardSolveResult> SolveBoardAsync(
            BoardPbn board,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
