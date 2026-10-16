using BridgeGameCalculator.Server.Dds;
using BridgeGameCalculator.Shared.Dtos;
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
            SingleHandSolveRequest request,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => SolveSessionCore(request, cancellationToken));
        }

        private Model.BoardSolveResult SolveSessionCore(SingleHandSolveRequest request, CancellationToken cancellationToken)
        { 
            BoardsPBN boardsPBN = new();
            boardsPBN.numberOfBoards = 1;
            boardsPBN.deals = new DealPBN[DdsConstants.MaxNoOfBoards];
            boardsPBN.solutions = new int[DdsConstants.MaxNoOfBoards];
            boardsPBN.target = new int[DdsConstants.MaxNoOfBoards];
            boardsPBN.mode = new int[DdsConstants.MaxNoOfBoards];
            for (int i = 0; i < boardsPBN.numberOfBoards; i++)
            {
                boardsPBN.deals[i] = new DealPBN();
                boardsPBN.deals[i].trump = request.trump;
                boardsPBN.deals[i].first = request.first;
                boardsPBN.deals[i].currentTrickRank = new int[3];
                for (int j = 0; j < 3; j++)
                {
                    boardsPBN.deals[i].currentTrickRank[j] = 0;
                }
                boardsPBN.deals[i].currentTrickSuit = new int[3];
                for (int j = 0; j < 3; j++)
                {
                    boardsPBN.deals[i].currentTrickSuit[j] = 0;
                }

                boardsPBN.deals[i].remainingCards = new char[80];
                char[] remainingCards = request.remainingCards.ToCharArray();
                Array.Copy(remainingCards, boardsPBN.deals[i].remainingCards, remainingCards.Length);
                boardsPBN.solutions[i] = 1;
                boardsPBN.target[i] = 1;
                boardsPBN.mode[i] = 0;
            }
            SolvedBoardsInterop solvedBoards = new();
            solvedBoards.solved_board = new Dds.FutureTricks[DdsConstants.MaxNoOfBoards];
            for (int i = 0; i < DdsConstants.MaxNoOfBoards; i++)
            {
                solvedBoards.solved_board[i].rank = new int[DdsConstants.NumberOfTricks];
                solvedBoards.solved_board[i].suit = new int[DdsConstants.NumberOfTricks];
//                solvedBoards.solved_board[i].equals = new int[DdsConstants.NumberOfTricks];
//                solvedBoards.solved_board[i].score = new int[DdsConstants.NumberOfTricks];
            }
            int result = DdsInterop.SolveAllBoards(ref boardsPBN, ref solvedBoards);
            Model.FutureTricks [] futTricks = new Model.FutureTricks[solvedBoards.solved_board.Length];
            for (int i = 0; i < futTricks.Length; i++)
            {
                futTricks[i] = new Model.FutureTricks()
                {
                    Nodes = solvedBoards.solved_board[i].nodes,
                    Cards = solvedBoards.solved_board[i].cards,
                    Suit = solvedBoards.solved_board[i].suit,
                    Rank = solvedBoards.solved_board[i].rank,
//                    Equals = solvedBoards.solved_board[i].equals,
//                    Score = solvedBoards.solved_board[i].score
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
