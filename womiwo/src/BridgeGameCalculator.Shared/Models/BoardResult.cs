namespace BridgeGameCalculator.Shared.Models;

/// <summary>
/// Display-ready result for one board, used by the session dashboard.
/// All strings are pre-formatted for the UI.
/// </summary>
public sealed record BoardResult(
    int     BoardNumber,
    string  VulnerabilityLabel,  // "None", "NS", "EW", "Both"
    string? ContractPlayed,      // e.g. "3NT by N", "Pass", null when not recorded
    string? TricksResult,        // e.g. "=", "+1", "-2", null when not recorded
    int?    ActualScore,
    string? ParContractLabel,    // e.g. "4S by N", "Pass"
    int     ParScore,
    int?    ImpDelta             // null when no result recorded or analysis failed
);
