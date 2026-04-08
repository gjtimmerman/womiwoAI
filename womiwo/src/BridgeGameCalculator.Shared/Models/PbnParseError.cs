namespace BridgeGameCalculator.Shared.Models;

/// <summary>
/// Structured error returned when PBN parsing fails.
/// </summary>
public sealed record PbnParseError(
    string  Message,
    int?    BoardNumber = null,
    int?    LineNumber  = null
);
