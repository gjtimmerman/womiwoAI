namespace BridgeGameCalculator.Shared.Dtos;

using BridgeGameCalculator.Shared.Models;

/// <summary>
/// Response from POST /api/hands/analyze.
/// Contains both the reconstructed Session (for hand diagrams) and the
/// SessionAnalysisResult (for par/delta display) so the client can populate
/// SessionState in a single round-trip.
/// </summary>
public record SingleHandAnalysisResult(
    Session               Session,
    SessionAnalysisResult Analysis);
