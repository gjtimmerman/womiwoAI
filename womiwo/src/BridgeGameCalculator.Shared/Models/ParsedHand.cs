namespace BridgeGameCalculator.Shared.Models;

/// <summary>
/// The four suit holdings for one bridge hand.
/// Each list is pre-sorted high-to-low by rank.
/// </summary>
public record ParsedHand(
    IReadOnlyList<Card> Spades,
    IReadOnlyList<Card> Hearts,
    IReadOnlyList<Card> Diamonds,
    IReadOnlyList<Card> Clubs);
