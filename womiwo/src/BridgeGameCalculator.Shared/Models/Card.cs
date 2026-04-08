namespace BridgeGameCalculator.Shared.Models;

public enum Suit { Spades, Hearts, Diamonds, Clubs }

public enum Rank
{
    Two = 2, Three, Four, Five, Six, Seven, Eight, Nine, Ten,
    Jack, Queen, King, Ace
}

public record Card(Suit Suit, Rank Rank);
