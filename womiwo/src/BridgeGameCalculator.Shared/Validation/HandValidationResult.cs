namespace BridgeGameCalculator.Shared.Validation;

/// <summary>
/// Result of <see cref="HandValidator.Validate"/>.
/// Errors are keyed by field name (e.g. "North", "Cards", "ContractLevel").
/// </summary>
public sealed class HandValidationResult
{
    public IReadOnlyDictionary<string, string> Errors { get; }

    public bool IsValid => Errors.Count == 0;

    public HandValidationResult(Dictionary<string, string> errors)
    {
        Errors = errors.AsReadOnly();
    }
}
