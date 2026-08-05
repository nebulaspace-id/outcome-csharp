namespace NebulaSpace.Outcome;

public sealed class Outcome : Outcome<Outcome>
{
    private Outcome()
    {
    }

    public static Outcome Success() => new() { IsSuccess = true };
    public static Outcome<T> Success<T>(T value) => Outcome<T>.Success(value);
    public static Outcome Fail(Failure failure) => new() { Failure = failure };
}

public class Outcome<T>
{
    internal Outcome()
    {
    }

    public bool IsSuccess { get; internal set; }
    public bool IsFailure => !IsSuccess;
    public T Value { get; internal set; } = default!;
    public Failure? Failure { get; internal set; }

    public static Outcome<T> Success(T value) => new() { IsSuccess = true, Value = value };

    public static implicit operator Outcome<T>(T value) => Success(value);

    public static implicit operator Outcome<T>(Outcome result) =>
        new() { IsSuccess = result.IsSuccess, Failure = result.Failure };

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Failure, TOut> onFailure)
        where TOut : notnull => IsSuccess ? onSuccess(Value) : onFailure(Failure!);
}
