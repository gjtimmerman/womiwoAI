using BridgeGameCalculator.Shared.Models;

namespace BridgeGameCalculator.Client.Models
{
    public class SolveBoardModel
    {
        public string Cards { get; set; } = "";
        public Seat Dealer { get; set; } = Seat.North;
        public Strain? ContractStrain { get; set; }
        public Seat? Declarer { get; set; }

    }
}
