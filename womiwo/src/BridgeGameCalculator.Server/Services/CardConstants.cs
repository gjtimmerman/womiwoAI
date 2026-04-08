namespace BridgeGameCalculator.Server.Services;

internal static class CardConstants
{
    /// <summary>Valid single-character card ranks in PBN notation (T = ten).</summary>
    public static readonly IReadOnlySet<char> ValidRanks =
        new HashSet<char> { 'A', 'K', 'Q', 'J', 'T', '9', '8', '7', '6', '5', '4', '3', '2' };

    /// <summary>Rank ordering high-to-low for display (index = display order).</summary>
    public static readonly string RankOrder = "AKQJT98765432";
}
