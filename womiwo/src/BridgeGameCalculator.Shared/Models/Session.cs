namespace BridgeGameCalculator.Shared.Models;

/// <summary>
/// A collection of boards from a single PBN file upload.
/// Not persisted — lives in memory for the duration of the browser session.
/// </summary>
public sealed class Session
{
    public required string              SourceFile { get; init; }
    public required IReadOnlyList<Board> Boards    { get; init; }
}
