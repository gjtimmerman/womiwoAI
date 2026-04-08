namespace BridgeGameCalculator.Shared.Parsing;

public sealed class PbnParseException : Exception
{
    public PbnParseException(string message) : base(message) { }
}
