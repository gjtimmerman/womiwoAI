namespace BridgeGameCalculator.Shared;

/// <summary>
/// A discriminated union representing either a success value or an error.
/// Keeps parse errors as structured data rather than exceptions.
/// </summary>
public sealed class Result<TValue, TError>
{
    private readonly TValue? _value;
    private readonly TError? _error;

    public bool IsSuccess { get; }
    public bool IsError   => !IsSuccess;

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value on an error result.");

    public TError Error => IsError
        ? _error!
        : throw new InvalidOperationException("Cannot access Error on a success result.");

    private Result(TValue value)
    {
        _value    = value;
        IsSuccess = true;
    }

    private Result(TError error, bool _)
    {
        _error    = error;
        IsSuccess = false;
    }

    public static Result<TValue, TError> Success(TValue value)  => new(value);
    public static Result<TValue, TError> Failure(TError error)  => new(error, false);
}
