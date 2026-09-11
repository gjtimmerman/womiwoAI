using System;
using System.Collections.Generic;
using System.Text;

namespace BridgeGameCalculator.Shared.Models
{
    public class BoardPbn
    {
        public int numberOfBoards {  get; set; }
        public DealPbn[] deals;
        public int []target {  get; set; }
        public int []solutions { get; set; }
        public int []mode { get; set; }
    }
}
