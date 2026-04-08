namespace BridgeGameCalculator.Shared.Models;

/// <summary>
/// The four hands of a bridge deal. Each hand is stored as a PBN suit string
/// with suits separated by dots in S.H.D.C order (e.g., "AK32.QJ.T987.654").
/// </summary>
public sealed record Hands(string North, string East, string South, string West);
