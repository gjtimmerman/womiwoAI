namespace BridgeGameCalculator.Shared.Models;

/// <summary>
/// A single bridge deal as extracted from a PBN file.
/// Contract, Declarer, and Result are null for passed-out boards.
/// </summary>
public sealed class Board
{
    public required int           BoardNumber    { get; init; }
    public required Seat          Dealer         { get; init; }
    public required Vulnerability Vulnerability  { get; init; }
    public required Hands         Hands          { get; init; }
    public Contract?              Contract       { get; init; }
    public Seat?                  Declarer       { get; init; }
    public int?                   Result         { get; init; }

    public bool IsPassedOut => Contract is null;
}
