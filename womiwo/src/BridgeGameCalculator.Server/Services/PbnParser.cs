namespace BridgeGameCalculator.Server.Services;

using System.Text.RegularExpressions;
using BridgeGameCalculator.Shared;
using BridgeGameCalculator.Shared.Models;

/// <summary>
/// Parses PBN (Portable Bridge Notation) files into a Session domain object.
/// The parser is stateless — safe to use as a singleton.
/// </summary>
public sealed class PbnParser
{
    private static readonly Regex TagPattern =
        new(@"^\[(\w+)\s+""(.*)""\]\s*$", RegexOptions.Compiled);

    /// <summary>
    /// Parse a PBN stream and return either a Session or a structured error.
    /// Never throws on malformed input.
    /// </summary>
    public Result<Session, PbnParseError> Parse(Stream content, string fileName)
    {
        var boards       = new List<Board>();
        var currentTags  = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int lineNumber   = 0;

        using var reader = new StreamReader(content, leaveOpen: true);

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            var trimmed = line.Trim();

            if (string.IsNullOrEmpty(trimmed))
                continue;

            var match = TagPattern.Match(trimmed);
            if (!match.Success)
                continue;   // Ignore non-tag lines (comments, play records, etc.)

            var tagName  = match.Groups[1].Value;
            var tagValue = match.Groups[2].Value;

            // A new [Board] tag signals the start of a new board context.
            // Finalize the previous board first.
            if (tagName.Equals("Board", StringComparison.OrdinalIgnoreCase)
                && currentTags.ContainsKey("Board"))
            {
                var result = FinalizeBoard(currentTags, lineNumber);
                if (result.IsError) return Result<Session, PbnParseError>.Failure(result.Error);
                boards.Add(result.Value);
                currentTags.Clear();
            }

            currentTags[tagName] = tagValue;
        }

        // Finalize the last board (no trailing [Board] tag triggers it above).
        if (currentTags.ContainsKey("Board"))
        {
            var result = FinalizeBoard(currentTags, lineNumber);
            if (result.IsError) return Result<Session, PbnParseError>.Failure(result.Error);
            boards.Add(result.Value);
        }

        if (boards.Count == 0)
            return Result<Session, PbnParseError>.Failure(
                new PbnParseError("The PBN file contains no boards."));

        return Result<Session, PbnParseError>.Success(
            new Session { SourceFile = fileName, Boards = boards });
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static Result<Board, PbnParseError> FinalizeBoard(
        Dictionary<string, string> tags, int lineNumber)
    {
        // --- Board number ---
        if (!tags.TryGetValue("Board", out var boardStr) ||
            !int.TryParse(boardStr, out int boardNumber))
            return Fail<Board>("Board number is missing or invalid.", null, lineNumber);

        // --- Dealer ---
        tags.TryGetValue("Dealer", out var dealerStr);
        if (!TryParseSeat(dealerStr, out var dealer))
            dealer = Seat.North; // Default per EC-10

        // --- Vulnerability ---
        tags.TryGetValue("Vulnerable", out var vulStr);
        var vulnerability = ParseVulnerability(vulStr ?? "None");

        // --- Deal (hands) ---
        if (!tags.TryGetValue("Deal", out var dealStr))
            return Fail<Board>($"Board {boardNumber}: missing required [Deal] tag.",
                        boardNumber, lineNumber);

        var handsResult = ParseDeal(dealStr, boardNumber);
        if (handsResult.IsError) return Result<Board, PbnParseError>.Failure(handsResult.Error);

        var deckError = ValidateDeck(handsResult.Value, boardNumber);
        if (deckError is not null) return Result<Board, PbnParseError>.Failure(deckError);

        // --- Contract (optional) ---
        tags.TryGetValue("Contract", out var contractStr);
        Contract? contract = null;
        Seat? declarer = null;
        int? result = null;

        bool isPassedOut = string.Equals(contractStr, "Pass",
                                         StringComparison.OrdinalIgnoreCase);
        if (!isPassedOut && contractStr is not null)
        {
            var contractResult = TryParseContract(contractStr, boardNumber);
            if (contractResult.IsError) return Result<Board, PbnParseError>.Failure(contractResult.Error);
            contract = contractResult.Value;

            if (tags.TryGetValue("Declarer", out var declarerStr) &&
                TryParseSeat(declarerStr, out var d))
                declarer = d;

            if (tags.TryGetValue("Result", out var resultStr) &&
                int.TryParse(resultStr, out int tricks) &&
                tricks >= 0 && tricks <= 13)
                result = tricks;
        }
        // EC-7: passed-out board with Result tag → ignore result, return success.

        return Result<Board, PbnParseError>.Success(new Board
        {
            BoardNumber   = boardNumber,
            Dealer        = dealer,
            Vulnerability = vulnerability,
            Hands         = handsResult.Value,
            Contract      = contract,
            Declarer      = declarer,
            Result        = result
        });
    }

    /// <summary>
    /// Parses a PBN Deal string: "N:s.h.d.c s.h.d.c s.h.d.c s.h.d.c"
    /// The first character is the seat the first hand belongs to.
    /// Hands are listed clockwise: N→E→S→W.
    /// </summary>
    private static Result<Hands, PbnParseError> ParseDeal(string deal, int boardNumber)
    {
        var colonIdx = deal.IndexOf(':');
        if (colonIdx < 0)
            return Fail<Hands>($"Board {boardNumber}: [Deal] tag has invalid format (missing ':').",
                        boardNumber);

        var firstSeatChar = deal[..colonIdx].Trim().ToUpperInvariant();
        if (!TryParseSeat(firstSeatChar, out var firstSeat))
            return Fail<Hands>($"Board {boardNumber}: invalid seat '{firstSeatChar}' in [Deal] tag.",
                        boardNumber);

        var handStrings = deal[(colonIdx + 1)..].Trim().Split(' ',
            StringSplitOptions.RemoveEmptyEntries);

        if (handStrings.Length != 4)
            return Fail<Hands>($"Board {boardNumber}: [Deal] must contain 4 hands, found {handStrings.Length}.",
                        boardNumber);

        // Map hands to seats (clockwise from firstSeat)
        var seats  = new Seat[4];
        var order  = new[] { Seat.North, Seat.East, Seat.South, Seat.West };
        int start  = (int)firstSeat;
        for (int i = 0; i < 4; i++)
            seats[i] = order[(start + i) % 4];

        string? north = null, east = null, south = null, west = null;
        for (int i = 0; i < 4; i++)
        {
            switch (seats[i])
            {
                case Seat.North: north = handStrings[i]; break;
                case Seat.East:  east  = handStrings[i]; break;
                case Seat.South: south = handStrings[i]; break;
                case Seat.West:  west  = handStrings[i]; break;
            }
        }

        if (north is null || east is null || south is null || west is null)
            return Fail<Hands>($"Board {boardNumber}: could not assign all four hands.", boardNumber);

        return Result<Hands, PbnParseError>.Success(new Hands(north, east, south, west));
    }

    /// <summary>
    /// Validates that all four hands together contain exactly 52 unique cards.
    /// Each hand must have exactly 13 cards; no card may appear twice.
    /// </summary>
    private static PbnParseError? ValidateDeck(Hands hands, int boardNumber)
    {
        var seatNames  = new[] { "North", "East", "South", "West" };
        var handValues = new[] { hands.North, hands.East, hands.South, hands.West };
        var seen       = new HashSet<string>(52);

        for (int s = 0; s < 4; s++)
        {
            var cards = ParseHandCards(handValues[s]);
            if (cards.Count != 13)
                return new PbnParseError(
                    $"Board {boardNumber}: {seatNames[s]} does not have 13 cards (found {cards.Count}).",
                    boardNumber);

            foreach (var card in cards)
            {
                if (!seen.Add(card))
                    return new PbnParseError(
                        $"Board {boardNumber}: card '{card}' appears more than once.",
                        boardNumber);
            }
        }

        return null; // Valid
    }

    /// <summary>
    /// Enumerates all (rank, suit) pairs from a PBN hand string "AK32.QJ.T987.654".
    /// Returns them as two-char strings like "AS", "KH", "TD".
    /// </summary>
    private static List<string> ParseHandCards(string hand)
    {
        var cards   = new List<string>(13);
        var suitChars = new[] { 'S', 'H', 'D', 'C' };
        var suits   = hand.Split('.');

        for (int i = 0; i < Math.Min(suits.Length, 4); i++)
        {
            foreach (char rank in suits[i])
            {
                if (CardConstants.ValidRanks.Contains(char.ToUpper(rank)))
                    cards.Add($"{char.ToUpper(rank)}{suitChars[i]}");
            }
        }

        return cards;
    }

    /// <summary>
    /// Parses a contract string like "4H", "3NT", "4HX", "4HXX".
    /// </summary>
    private static Result<Contract, PbnParseError> TryParseContract(
        string contractStr, int boardNumber)
    {
        if (contractStr.Length < 2)
            return Fail<Contract>($"Board {boardNumber}: invalid contract '{contractStr}'.", boardNumber);

        if (!int.TryParse(contractStr[..1], out int level) || level < 1 || level > 7)
            return Fail<Contract>($"Board {boardNumber}: invalid contract level in '{contractStr}'.",
                        boardNumber);

        // Determine double state from suffix
        DoubleState doubleState;
        string strainPart;
        if (contractStr.EndsWith("XX", StringComparison.OrdinalIgnoreCase))
        {
            doubleState = DoubleState.Redoubled;
            strainPart  = contractStr[1..^2];
        }
        else if (contractStr.EndsWith("X", StringComparison.OrdinalIgnoreCase))
        {
            doubleState = DoubleState.Doubled;
            strainPart  = contractStr[1..^1];
        }
        else
        {
            doubleState = DoubleState.Undoubled;
            strainPart  = contractStr[1..];
        }

        var strain = strainPart.ToUpperInvariant() switch
        {
            "S"  => (Strain?)Strain.Spades,
            "H"  => Strain.Hearts,
            "D"  => Strain.Diamonds,
            "C"  => Strain.Clubs,
            "NT" => Strain.NoTrump,
            "N"  => Strain.NoTrump, // Some PBN files use "N" for NoTrump
            _    => null
        };

        if (strain is null)
            return Fail<Contract>($"Board {boardNumber}: unrecognised strain in contract '{contractStr}'.",
                        boardNumber);

        return Result<Contract, PbnParseError>.Success(
            new Contract(level, strain.Value, doubleState));
    }

    private static bool TryParseSeat(string? value, out Seat seat)
    {
        seat = Seat.North;
        return value?.ToUpperInvariant() switch
        {
            "N" => (seat = Seat.North) == Seat.North,
            "E" => (seat = Seat.East)  == Seat.East,
            "S" => (seat = Seat.South) == Seat.South,
            "W" => (seat = Seat.West)  == Seat.West,
            _   => false
        };
    }

    private static Vulnerability ParseVulnerability(string value) =>
        value.ToUpperInvariant() switch
        {
            "NONE"  or "0"    => Vulnerability.None,
            "NS"              => Vulnerability.NorthSouth,
            "EW"              => Vulnerability.EastWest,
            "BOTH" or "ALL"   => Vulnerability.Both,
            _                 => Vulnerability.None
        };

    private static Result<T, PbnParseError> Fail<T>(
        string message, int? boardNumber = null, int? lineNumber = null)
        => Result<T, PbnParseError>.Failure(new PbnParseError(message, boardNumber, lineNumber));
}
