using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace BridgeGameCalculator.Shared.Models
{
    public class FutureTricks
    {
        public int Nodes { get; init; }
        public int Cards { get; init; }
        public int[] Suit {  get; init; }
        public int[] Rank { get; init; }
        public int[] Equals { get; init; }
        public int[] Score { get; init; }
    }
}
