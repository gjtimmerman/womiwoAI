namespace BridgeGameCalculator.Tests.TestData;

/// <summary>PBN string constants used by parser unit tests.</summary>
internal static class PbnTestData
{
    // Valid 52-card deal used across most test cases (verified: 13 cards per hand, no duplicates).
    // N: AKQ2.32.AKQ2.AK3  E: JT98.QJT9.J543.2  S: 7654.A876.T97.T9  W: 3.K54.86.QJ87654
    private const string ValidDeal =
        "N:AKQ2.32.AKQ2.AK3 JT98.QJT9.J543.2 7654.A876.T97.T9 3.K54.86.QJ87654";

    // Second valid 52-card deal used for board 2 in ValidTwoBoards.
    // N: AQJ3.KJ6.AQT.K84  E: K752.Q873.K98.J2  S: T864.A94.J73.A75  W: 9.T52.6542.QT963
    private const string ValidDeal2 =
        "N:AQJ3.KJ6.AQT.K84 K752.Q873.K98.J2 T864.A94.J73.A75 9.T52.6542.QT963";

    /// <summary>A single complete, valid board with all standard tags.</summary>
    public const string ValidSingleBoard = $"""
        [Board "1"]
        [Dealer "N"]
        [Vulnerable "None"]
        [Deal "{ValidDeal}"]
        [Contract "3NT"]
        [Declarer "N"]
        [Result "9"]
        """;

    /// <summary>Two complete boards in sequence.</summary>
    public const string ValidTwoBoards = $"""
        [Board "1"]
        [Dealer "N"]
        [Vulnerable "None"]
        [Deal "{ValidDeal}"]
        [Contract "3NT"]
        [Declarer "N"]
        [Result "9"]

        [Board "2"]
        [Dealer "E"]
        [Vulnerable "NS"]
        [Deal "{ValidDeal2}"]
        [Contract "4H"]
        [Declarer "E"]
        [Result "10"]
        """;

    /// <summary>A board with Contract "Pass" (passed out).</summary>
    public const string PassedOutBoard = $"""
        [Board "3"]
        [Dealer "S"]
        [Vulnerable "EW"]
        [Deal "{ValidDeal}"]
        [Contract "Pass"]
        """;

    /// <summary>EC-7: passed-out board that also has a Result tag (should be ignored).</summary>
    public const string PassedOutBoardWithResult = $"""
        [Board "4"]
        [Dealer "W"]
        [Vulnerable "Both"]
        [Deal "{ValidDeal}"]
        [Contract "Pass"]
        [Result "0"]
        """;

    /// <summary>EC-4: board missing the required [Deal] tag.</summary>
    public const string MissingDealTag = """
        [Board "5"]
        [Dealer "N"]
        [Vulnerable "None"]
        [Contract "4S"]
        [Declarer "N"]
        [Result "10"]
        """;

    /// <summary>EC-5: Ace of Spades appears in both North and East hands.</summary>
    // East's spades changed from JT98 to AT98; duplicate AS is detected before count issues.
    public const string DuplicateCard = """
        [Board "6"]
        [Dealer "N"]
        [Vulnerable "None"]
        [Deal "N:AKQ2.32.AKQ2.AK3 AT98.QJT9.J543.2 7654.A876.T97.T9 3.K54.86.QJ87654"]
        [Contract "3NT"]
        [Declarer "N"]
        [Result "9"]
        """;

    /// <summary>EC-4: North's hand has only 12 cards.</summary>
    // North spades trimmed from AKQ2 to AKQ (12 total). Parser rejects before reaching other hands.
    public const string Hand13CardViolation = """
        [Board "7"]
        [Dealer "N"]
        [Vulnerable "None"]
        [Deal "N:AKQ.32.AKQ2.AK3 JT98.QJT9.J543.2 7654.A876.T97.T9 3.K54.86.QJ87654"]
        [Contract "3NT"]
        [Declarer "N"]
        [Result "9"]
        """;

    /// <summary>Board with non-standard extra tags that should be silently ignored.</summary>
    public const string UnrecognizedTags = $"""
        [Board "8"]
        [Event "Club Championship"]
        [Site "Club Room"]
        [Dealer "N"]
        [Vulnerable "None"]
        [Deal "{ValidDeal}"]
        [Score "NS 400"]
        [Contract "3NT"]
        [Declarer "N"]
        [Result "9"]
        [ScoreIMP "2.5"]
        """;

    /// <summary>Empty file / no content.</summary>
    public const string EmptyFile = "";

    /// <summary>Plain text that is not PBN.</summary>
    public const string NotPbnContent = """
        This is not a PBN file.
        It contains no bridge notation.
        Just some random text.
        """;

    /// <summary>EC-8: board with Deal but no Contract or Result tags.</summary>
    public const string MissingContractAndResult = $"""
        [Board "9"]
        [Dealer "N"]
        [Vulnerable "None"]
        [Deal "{ValidDeal}"]
        """;

    /// <summary>Board with a doubled contract.</summary>
    public const string DoubledContract = $"""
        [Board "10"]
        [Dealer "S"]
        [Vulnerable "NS"]
        [Deal "{ValidDeal}"]
        [Contract "4HX"]
        [Declarer "S"]
        [Result "9"]
        """;

    /// <summary>Board with a redoubled contract.</summary>
    public const string RedoubledContract = $"""
        [Board "11"]
        [Dealer "S"]
        [Vulnerable "Both"]
        [Deal "{ValidDeal}"]
        [Contract "3NTXX"]
        [Declarer "N"]
        [Result "9"]
        """;

    /// <summary>Board with a NoTrump contract.</summary>
    public const string NoTrumpContract = $"""
        [Board "12"]
        [Dealer "N"]
        [Vulnerable "None"]
        [Deal "{ValidDeal}"]
        [Contract "3NT"]
        [Declarer "N"]
        [Result "9"]
        """;
}
