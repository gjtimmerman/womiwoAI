using System;
using System.Collections.Generic;
using System.Text;

namespace BridgeGameCalculator.Shared.Models
{
    public class SolvedBoards
    {
        public int NumberOfBoards { get; set; }
        public FutureTricks[] futureTricks {  get; set; } 
    }
}
