namespace BridgeGameCalculator.Shared.Parsing;

using BridgeGameCalculator.Shared.Models;

/// <summary>
/// Parses PBN hand strings (e.g. "AKQ2.JT9.87.654") into strongly-typed <see cref="ParsedHand"/>
/// collections. Static utility — no dependencies.
/// </summary>
public static class PbnDealParser
{
    private static readonly Dictionary<char, Rank> RankMap = new()
    {
        ['A'] = Rank.Ace,
        ['K'] = Rank.King,
        ['Q'] = Rank.Queen,
        ['J'] = Rank.Jack,
        ['T'] = Rank.Ten,
        ['9'] = Rank.Nine,
        ['8'] = Rank.Eight,
        ['7'] = Rank.Seven,
        ['6'] = Rank.Six,
        ['5'] = Rank.Five,
        ['4'] = Rank.Four,
        ['3'] = Rank.Three,
        ['2'] = Rank.Two,
    };

    /// <summary>
    /// Parses a single PBN hand string such as <c>"AKQ2.JT9.87.654"</c> into a
    /// <see cref="ParsedHand"/> with suits in S.H.D.C order, each sorted high-to-low.
    /// </summary>
    /// <exception cref="PbnParseException">Thrown for malformed input.</exception>
    public static ParsedHand ParseHand(string pbnHand)
    {
        if (string.IsNullOrEmpty(pbnHand))
            throw new PbnParseException("Hand string is empty.");

        // Normalise alternate "10" notation to "T"
        pbnHand = pbnHand.Replace("10", "T");

        var parts = pbnHand.Split('.');
        if (parts.Length != 4)
            throw new PbnParseException(
                $"Expected 4 suit sections separated by '.', got {parts.Length}: '{pbnHand}'.");

        return new ParsedHand(
            ParseSuit(parts[0], Suit.Spades),
            ParseSuit(parts[1], Suit.Hearts),
            ParseSuit(parts[2], Suit.Diamonds),
            ParseSuit(parts[3], Suit.Clubs));
    }

    /// <summary>
    /// Parses all four hands from a <see cref="Hands"/> record into a
    /// dictionary keyed by <see cref="Seat"/>.
    /// </summary>
    public static Dictionary<Seat, ParsedHand> ParseAllHands(Hands hands) =>
        new()
        {
            [Seat.North] = ParseHand(hands.North),
            [Seat.East]  = ParseHand(hands.East),
            [Seat.South] = ParseHand(hands.South),
            [Seat.West]  = ParseHand(hands.West),
        };

    private static IReadOnlyList<Card> ParseSuit(string suitStr, Suit suit)
    {
        var cards = new List<Card>(suitStr.Length);
        foreach (char c in suitStr)
        {
            if (!RankMap.TryGetValue(c, out var rank))
                throw new PbnParseException(
                    $"Unknown rank character '{c}' in hand segment '{suitStr}'.");
            cards.Add(new Card(suit, rank));
        }
        cards.Sort((a, b) => b.Rank.CompareTo(a.Rank));  // descending — Ace first
        return cards.AsReadOnly();
    }
}
