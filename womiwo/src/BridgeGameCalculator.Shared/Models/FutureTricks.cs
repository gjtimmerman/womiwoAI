using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace BridgeGameCalculator.Shared.Models
{
    public class FutureTricks
    {
        int Nodes { get; init; }
        int Cards { get; init; }
        int[] Suit {  get; init; }
        int[] Rank { get; init; }
        int[] Equals { get; init; }
        int[] Score { get; init; }
    }
}
