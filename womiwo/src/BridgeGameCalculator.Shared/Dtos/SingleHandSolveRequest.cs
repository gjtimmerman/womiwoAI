using System;
using System.Collections.Generic;
using System.Text;

namespace BridgeGameCalculator.Shared.Dtos
{
    public record SingleHandSolveRequest
    {
        public int trump { get; set; }
        public int first { get; set;  }
        public string remainingCards { get; set; }
    }
}
