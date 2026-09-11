using System;
using System.Collections.Generic;
using System.Text;

namespace BridgeGameCalculator.Shared.Models
{
    public class BoardSolveResult
    {
        public bool IsSuccess { get; init; }
        public SolvedBoards solvedBoards {  get; init; }
        public string ErrorMessage { get; init; }
    }
}
