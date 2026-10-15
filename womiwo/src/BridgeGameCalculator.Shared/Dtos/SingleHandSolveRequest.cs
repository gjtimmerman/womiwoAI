using System;
using System.Collections.Generic;
using System.Text;

namespace BridgeGameCalculator.Shared.Dtos
{
    public record SingleHandSolveRequest
    {
        public int trump;
        public int first;
        public string remainingCards = "";
    }
}
