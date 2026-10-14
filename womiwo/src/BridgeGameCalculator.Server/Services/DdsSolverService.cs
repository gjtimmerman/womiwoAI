using BridgeGameCalculator.Server.Dds;
using System.Text;
using Model = BridgeGameCalculator.Shared.Models;


namespace BridgeGameCalculator.Server.Services
{
    public class DdsSolverService : IDdsSolverService
    {
        ILogger<DdsSolverService> _logger;
        public DdsSolverService(ILogger<DdsSolverService> logger)
        {
            _logger = logger;
        }

        public async Task<Model.BoardSolveResult> SolveSessionAsync(
            Model.BoardPbn boards,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => SolveSessionCore(boards, cancellationToken));
        }

        private Model.BoardSolveResult SolveSessionCore(Model.BoardPbn boards, CancellationToken cancellationToken)
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
                boardsPBN.solutions[i] = 1;
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
            int result = DdsInterop.SolveAllBoards(ref boardsPBN, ref solvedBoards);
            Model.FutureTricks [] futTricks = new Model.FutureTricks[solvedBoards.no_of_boards];
            for (int i = 0; i < futTricks.Length; i++)
            {
                futTricks[i] = new Model.FutureTricks()
                {
                    Nodes = solvedBoards.solved_board[i].nodes,
                    Cards = solvedBoards.solved_board[i].cards,
                    Suit = solvedBoards.solved_board[i].suit,
                    Rank = solvedBoards.solved_board[i].rank,
                    Equals = solvedBoards.solved_board[i].equals,
                    Score = solvedBoards.solved_board[i].score
                };
            }
            StringBuilder message = new StringBuilder();
            DdsInterop.ErrorMessage(result, message);
            Model.BoardSolveResult boardSolveResult = new Model.BoardSolveResult() { IsSuccess = result == 1, solvedBoards =  new Model.SolvedBoards() { NumberOfBoards = solvedBoards.no_of_boards, futureTricks = futTricks }, ErrorMessage = message.ToString() };
            
            return boardSolveResult;
        }

        public Task<Model.BoardSolveResult> SolveBoardAsync(
            Model.BoardPbn board,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
