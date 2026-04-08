namespace BridgeGameCalculator.Server.Dds;

using BridgeGameCalculator.Shared.Models;

/// <summary>
/// Parses the contract-string output of DDS DealerPar into <see cref="ParContract"/> objects.
///
/// DDS output format (from parResultsDealer.Contracts0):
///   "4S-N"       → 4 Spades by North, undoubled
///   "4S*-N"      → 4 Spades by North, doubled
///   "3NT-E"      → 3 NoTrump by East
///   "4S-NS"      → 4 Spades by North or South (two par contracts)
///   "4S-N,3NT-E" → two separate par contracts (comma-separated)
///   "pass" / "" → passed out, empty list
/// </summary>
internal static class DdsParContractParser
{
    public static IReadOnlyList<ParContract> Parse(string? contractsString)
    {
        if (string.IsNullOrWhiteSpace(contractsString))
            return [];

        if (contractsString.Equals("pass", StringComparison.OrdinalIgnoreCase))
            return [];

        var contracts = new List<ParContract>();
        foreach (var token in contractsString.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            contracts.AddRange(ParseToken(token.Trim()));
        }
        return contracts;
    }

    // ---- private ----

    private static IEnumerable<ParContract> ParseToken(string token)
    {
        // Format: <level><strain>[*]-<declarers>
        // e.g. "4S*-NS", "3NT-E", "2H-W"
        if (token.Length < 3) yield break;

        if (!int.TryParse(token[..1], out int level) || level < 1 || level > 7)
            yield break;

        // Find the '-' separating strain from declarers
        int dashIdx = token.IndexOf('-', 1);
        if (dashIdx < 0) yield break;

        var strainPart    = token[1..dashIdx];
        var declarersPart = token[(dashIdx + 1)..];

        // Strip doubled marker '*'
        bool doubled = strainPart.EndsWith('*');
        if (doubled) strainPart = strainPart[..^1];
        var doubleState = doubled ? DoubleState.Doubled : DoubleState.Undoubled;

        var strain = ParseStrain(strainPart);
        if (strain is null) yield break;

        // Declarers can be "N", "E", "S", "W", "NS", "EW", "NE", etc.
        foreach (char dc in declarersPart)
        {
            if (TryParseSeat(dc, out var seat))
                yield return new ParContract(level, strain.Value, seat, doubleState);
        }
    }

    private static Strain? ParseStrain(string s) =>
        s.ToUpperInvariant() switch
        {
            "S"  => Strain.Spades,
            "H"  => Strain.Hearts,
            "D"  => Strain.Diamonds,
            "C"  => Strain.Clubs,
            "NT" => Strain.NoTrump,
            "N"  => Strain.NoTrump,   // Some DDS versions omit the 'T'
            _    => null
        };

    private static bool TryParseSeat(char c, out Seat seat)
    {
        switch (char.ToUpperInvariant(c))
        {
            case 'N': seat = Seat.North; return true;
            case 'E': seat = Seat.East;  return true;
            case 'S': seat = Seat.South; return true;
            case 'W': seat = Seat.West;  return true;
            default:  seat = Seat.North; return false;
        }
    }
}
