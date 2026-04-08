namespace BridgeGameCalculator.Shared.Dtos;

using BridgeGameCalculator.Shared.Models;

/// <summary>
/// Request body for POST /api/hands/analyze.
/// Hand strings are in free format (dot or suit-colon notation).
/// Contract fields are all optional; if any are set, all must be set.
/// </summary>
public record SingleHandRequest(
    string       NorthHand,
    string       EastHand,
    string       SouthHand,
    string       WestHand,
    Seat         Dealer,
    Vulnerability Vulnerability,
    int?          ContractLevel,
    Strain?       ContractStrain,
    DoubleState?  Doubled,
    Seat?         Declarer,
    int?          Result);
