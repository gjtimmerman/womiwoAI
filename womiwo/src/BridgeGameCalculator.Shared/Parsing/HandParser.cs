namespace BridgeGameCalculator.Shared.Parsing;

using BridgeGameCalculator.Shared.Models;

/// <summary>
/// Parses a bridge hand entered as free text.
/// Accepts two formats:
/// <list type="bullet">
///   <item><c>S:AKQ H:JT9 D:87 C:65432</c> — suit-colon tokens, any order</item>
///   <item><c>AKQ.JT9.87.65432</c> — PBN dot notation, S.H.D.C order</item>
/// </list>
/// Normalises lowercase input and "10" → "T". Static — no dependencies.
/// </summary>
public static class HandParser
{
    private static readonly Dictionary<char, Rank> RankMap = new()
    {
        ['A'] = Rank.Ace,   ['K'] = Rank.King,  ['Q'] = Rank.Queen, ['J'] = Rank.Jack,
        ['T'] = Rank.Ten,   ['9'] = Rank.Nine,  ['8'] = Rank.Eight, ['7'] = Rank.Seven,
        ['6'] = Rank.Six,   ['5'] = Rank.Five,  ['4'] = Rank.Four,  ['3'] = Rank.Three,
        ['2'] = Rank.Two,
    };

    private static readonly Dictionary<char, Suit> SuitMap = new()
    {
        ['S'] = Suit.Spades, ['H'] = Suit.Hearts, ['D'] = Suit.Diamonds, ['C'] = Suit.Clubs
    };

    public static HandParseResult Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return HandParseResult.Failure("Hand is required.");

        // Normalise: uppercase, "10" → "T"
        string normalised = input.Trim().ToUpperInvariant().Replace("10", "T");

        if (normalised.Contains(':'))
            return ParseColonFormat(normalised);

        if (normalised.Contains('.'))
            return ParseDotFormat(normalised);

        return HandParseResult.Failure(
            "Unrecognized hand format. Use S:AKQ H:JT9 D:87 C:654 or AKQ.JT9.87.654.");
    }

    // -------------------------------------------------------------------------

    private static HandParseResult ParseColonFormat(string input)
    {
        var suitCards = EmptySuitDict();

        var tokens = input.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            if (token.Length < 2 || token[1] != ':')
                return HandParseResult.Failure($"Invalid token '{token}'. Expected format 'S:cards'.");

            char suitChar = token[0];
            if (!SuitMap.TryGetValue(suitChar, out var suit))
                return HandParseResult.Failure($"Unknown suit '{suitChar}'. Use S, H, D, or C.");

            string rankPart = token[2..];
            foreach (char c in rankPart)
            {
                if (!RankMap.TryGetValue(c, out var rank))
                    return HandParseResult.Failure($"Unknown rank '{c}' in {suitChar} suit.");
                suitCards[suit].Add(new Card(suit, rank));
            }
        }

        return BuildResult(suitCards);
    }

    private static HandParseResult ParseDotFormat(string input)
    {
        var parts = input.Split('.');
        if (parts.Length != 4)
            return HandParseResult.Failure(
                $"Expected 4 suit sections separated by '.', got {parts.Length}.");

        var suitCards   = EmptySuitDict();
        Suit[] suitOrder = [Suit.Spades, Suit.Hearts, Suit.Diamonds, Suit.Clubs];

        for (int i = 0; i < 4; i++)
        {
            var suit = suitOrder[i];
            foreach (char c in parts[i])
            {
                if (!RankMap.TryGetValue(c, out var rank))
                    return HandParseResult.Failure($"Unknown rank '{c}' in section {i + 1}.");
                suitCards[suit].Add(new Card(suit, rank));
            }
        }

        return BuildResult(suitCards);
    }

    private static HandParseResult BuildResult(Dictionary<Suit, List<Card>> suitCards)
    {
        // Check for duplicates within the hand
        var seen = new HashSet<(Suit, Rank)>();
        foreach (var (suit, cards) in suitCards)
        {
            foreach (var card in cards)
            {
                if (!seen.Add((card.Suit, card.Rank)))
                    return HandParseResult.Failure(
                        $"Duplicate card: {RankChar(card.Rank)} of {suit}.");
            }
        }

        // Check total card count
        int total = suitCards.Values.Sum(c => c.Count);
        if (total != 13)
            return HandParseResult.Failure(
                $"A bridge hand must have exactly 13 cards; found {total}.");

        // Sort each suit high-to-low
        foreach (var list in suitCards.Values)
            list.Sort((a, b) => b.Rank.CompareTo(a.Rank));

        var spades   = suitCards[Suit.Spades].AsReadOnly();
        var hearts   = suitCards[Suit.Hearts].AsReadOnly();
        var diamonds = suitCards[Suit.Diamonds].AsReadOnly();
        var clubs    = suitCards[Suit.Clubs].AsReadOnly();

        var allCards = spades.Concat(hearts).Concat(diamonds).Concat(clubs)
                             .ToList().AsReadOnly();

        string pbnHand =
            $"{ToRankStr(spades)}.{ToRankStr(hearts)}.{ToRankStr(diamonds)}.{ToRankStr(clubs)}";

        return new HandParseResult
        {
            IsSuccess = true,
            Spades = spades, Hearts = hearts, Diamonds = diamonds, Clubs = clubs,
            AllCards  = allCards,
            PbnHand   = pbnHand,
        };
    }

    // -------------------------------------------------------------------------

    private static Dictionary<Suit, List<Card>> EmptySuitDict() =>
        new()
        {
            [Suit.Spades] = [], [Suit.Hearts] = [], [Suit.Diamonds] = [], [Suit.Clubs] = []
        };

    private static string ToRankStr(IReadOnlyList<Card> cards)
    {
        if (cards.Count == 0) return "";
        var sb = new System.Text.StringBuilder(cards.Count);
        foreach (var c in cards) sb.Append(RankChar(c.Rank));
        return sb.ToString();
    }

    internal static string RankChar(Rank rank) => rank switch
    {
        Rank.Ace   => "A",
        Rank.King  => "K",
        Rank.Queen => "Q",
        Rank.Jack  => "J",
        Rank.Ten   => "T",
        _          => ((int)rank).ToString()
    };
}
