namespace BridgeGameCalculator.Client.Models;

using BridgeGameCalculator.Shared.Models;

public sealed class SingleHandFormModel
{
    public string        NorthHand      { get; set; } = "";
    public string        EastHand       { get; set; } = "";
    public string        SouthHand      { get; set; } = "";
    public string        WestHand       { get; set; } = "";
    public Seat          Dealer         { get; set; } = Seat.North;
    public Vulnerability Vulnerability  { get; set; } = Vulnerability.None;

    // Optional contract (all or none)
    public int?          ContractLevel  { get; set; }
    public Strain?       ContractStrain { get; set; }
    public DoubleState?  Doubled        { get; set; }
    public Seat?         Declarer       { get; set; }
    public int?          Result         { get; set; }
}
