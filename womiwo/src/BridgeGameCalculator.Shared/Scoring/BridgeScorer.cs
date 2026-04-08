namespace BridgeGameCalculator.Shared.Scoring;

using BridgeGameCalculator.Shared.Models;

/// <summary>
/// Pure-arithmetic WBF duplicate bridge scoring.
/// All results are from NS perspective: positive = NS gains, negative = EW gains.
/// </summary>
public static class BridgeScorer
{
    // WBF IMP upper-bound table. Index = IMP value (0-20); value = upper bound of that bracket.
    // Differences above the last entry (> 2490) map to 24 IMPs.
    private static readonly int[] ImpUpperBounds =
    [
        10,   // 0 IMPs
        40,   // 1 IMP
        80,   // 2 IMPs
        120,  // 3 IMPs
        160,  // 4 IMPs
        210,  // 5 IMPs
        260,  // 6 IMPs
        310,  // 7 IMPs
        360,  // 8 IMPs
        420,  // 9 IMPs
        490,  // 10 IMPs
        590,  // 11 IMPs
        740,  // 12 IMPs
        890,  // 13 IMPs
        1090, // 14 IMPs
        1290, // 15 IMPs
        1490, // 16 IMPs
        1740, // 17 IMPs
        1990, // 18 IMPs
        2240, // 19 IMPs
        2490, // 20 IMPs
    ];

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Calculate the duplicate score for a contract result, from NS perspective.
    /// <paramref name="tricksMade"/> is the total tricks won by declarer (0-13).
    /// </summary>
    public static int CalculateScore(
        Contract      contract,
        Seat          declarer,
        Vulnerability vulnerability,
        int           tricksMade)
    {
        bool declarerIsNS = declarer is Seat.North or Seat.South;
        bool vul = declarerIsNS
            ? vulnerability is Vulnerability.NorthSouth or Vulnerability.Both
            : vulnerability is Vulnerability.EastWest   or Vulnerability.Both;

        int tricksOver = tricksMade - (6 + contract.Level); // negative = undertricks

        int rawScore = tricksOver >= 0
            ? ScoreMade(contract, vul, tricksOver)
            : -ScoreDown(-tricksOver, contract.DoubleState, vul);

        return declarerIsNS ? rawScore : -rawScore;
    }

    /// <summary>
    /// IMP delta between actual score and par score, from NS perspective.
    /// Returns null when <paramref name="actualScore"/> is null (no result recorded).
    /// </summary>
    public static int? CalculateImpDelta(int? actualScore, int parScore)
    {
        if (actualScore is null) return null;
        int diff = actualScore.Value - parScore;
        int imps = ImpFromDifference(Math.Abs(diff));
        return diff >= 0 ? imps : -imps;
    }

    /// <summary>
    /// Look up the WBF IMP value for an absolute point difference.
    /// </summary>
    public static int ImpFromDifference(int absoluteDifference)
    {
        for (int i = 0; i < ImpUpperBounds.Length; i++)
        {
            if (absoluteDifference <= ImpUpperBounds[i])
                return i;
        }
        return 24; // 2500+
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static int ScoreMade(Contract contract, bool vul, int overtricks)
    {
        int perTrick   = PerTrickValue(contract.Strain);
        int multiplier = contract.DoubleState switch
        {
            DoubleState.Undoubled  => 1,
            DoubleState.Doubled    => 2,
            DoubleState.Redoubled  => 4,
            _                      => throw new ArgumentOutOfRangeException()
        };

        // Bid-trick score (doubled/redoubled applies here)
        int trickScore = contract.Strain == Strain.NoTrump
            ? (40 + 30 * (contract.Level - 1)) * multiplier
            : perTrick * contract.Level * multiplier;

        bool isGame      = trickScore >= 100;
        int  gameBonus   = isGame ? (vul ? 500 : 300) : 50;
        int  slamBonus   = contract.Level == 7 ? (vul ? 1500 : 1000)
                         : contract.Level == 6 ? (vul ?  750 :  500)
                         : 0;
        int  insult      = contract.DoubleState switch
        {
            DoubleState.Undoubled  => 0,
            DoubleState.Doubled    => 50,
            DoubleState.Redoubled  => 100,
            _                      => throw new ArgumentOutOfRangeException()
        };

        // Overtrick value per trick
        int overtrickValue = contract.DoubleState switch
        {
            DoubleState.Undoubled  => perTrick,              // 20 or 30 (NT counts as 30)
            DoubleState.Doubled    => vul ? 200 : 100,
            DoubleState.Redoubled  => vul ? 400 : 200,
            _                      => throw new ArgumentOutOfRangeException()
        };

        return trickScore + gameBonus + slamBonus + insult + overtricks * overtrickValue;
    }

    private static int ScoreDown(int undertricks, DoubleState doubled, bool vul)
    {
        if (doubled == DoubleState.Undoubled)
            return undertricks * (vul ? 100 : 50);

        int penalty = 0;
        for (int i = 1; i <= undertricks; i++)
        {
            penalty += doubled == DoubleState.Doubled
                ? i == 1 ? (vul ? 200 : 100)
                : i <= 3 ? (vul ? 300 : 200)
                :            300           // 4th+ undertrick: both vul and NV = 300
                // For V doubled 4th+ is also 300; NV doubled 4th+ is 300 ✓
                : /* Redoubled */
                  i == 1 ? (vul ? 400 : 200)
                : i <= 3 ? (vul ? 600 : 400)
                :            600;
        }
        return penalty;
    }

    private static int PerTrickValue(Strain strain) => strain switch
    {
        Strain.Clubs    or Strain.Diamonds => 20,
        Strain.Hearts   or Strain.Spades   => 30,
        Strain.NoTrump                     => 30, // used for overtrick value only
        _                                  => throw new ArgumentOutOfRangeException()
    };
}
