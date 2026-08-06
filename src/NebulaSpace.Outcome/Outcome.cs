namespace NebulaSpace.Outcome;

/// <summary>
/// A result of an operation: either a success (with a <typeparamref name="T"/> value)
/// or a failure (with a <see cref="Failure"/>). Immutable — instances are created only
/// through the static factories and implicit conversions, and their state cannot change.
/// </summary>
public readonly struct Outcome<T>
{
    private readonly T? _value;
    private readonly Failure? _failure;
    private readonly bool _isSuccess;

    private Outcome(T value)
    {
        _value = value;
        _failure = null;
        _isSuccess = true;
    }

    private Outcome(Failure failure)
    {
        _value = default;
        _failure = failure;
        _isSuccess = false;
    }

    /// <summary>Whether the operation succeeded.</summary>
    public bool IsSuccess => _isSuccess;

    /// <summary>Whether the operation failed.</summary>
    public bool IsFailure => !_isSuccess;

    /// <summary>
    /// The payload on success. Accessing it on a failure throws <see cref="InvalidOperationException"/>.
    /// </summary>
    public T Value
    {
        get
        {
            if (!_isSuccess) ThrowInvalidStateAccess("Value");
            return _value!;
        }
    }

    /// <summary>
    /// The failure on failure. Accessing it on a success throws <see cref="InvalidOperationException"/>.
    /// </summary>
    public Failure Failure
    {
        get
        {
            if (_isSuccess) ThrowInvalidStateAccess("Failure");
            return _failure!;
        }
    }

    /// <summary>Creates a successful outcome carrying <paramref name="value"/>.</summary>
    public static Outcome<T> Success(T value) => new(value);

    /// <summary>Creates a failed outcome carrying <paramref name="failure"/>.</summary>
    public static Outcome<T> Fail(Failure failure) => new(failure);

    /// <summary>Implicitly converts a raw value into a successful outcome.</summary>
    public static implicit operator Outcome<T>(T value) => Success(value);

    /// <summary>Implicitly converts a unit outcome, preserving its success/failure state.</summary>
    public static implicit operator Outcome<T>(Outcome<Unit> unit) =>
        unit.IsSuccess ? Success(default!) : Fail(unit.Failure);

    /// <summary>
    /// Collapses both branches into a single value. On success invokes
    /// <paramref name="onSuccess"/> with the payload; on failure invokes
    /// <paramref name="onFailure"/> with the failure.
    /// </summary>
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Failure, TOut> onFailure)
        => _isSuccess ? onSuccess(_value!) : onFailure(_failure!);

    /// <summary>Deconstructs the outcome for C# pattern matching.</summary>
    public void Deconstruct(out bool isSuccess, out T? value, out Failure? failure)
    {
        isSuccess = _isSuccess;
        value = _value;
        failure = _failure;
    }

    private static void ThrowInvalidStateAccess(string member) =>
        throw new InvalidOperationException(
            $"Cannot access Outcome<{typeof(T).Name}>.{member} on the current state.");
}

/// <summary>
/// Non-generic factory helpers for creating outcomes.
/// </summary>
public static class Outcome
{
    /// <summary>Creates a successful unit outcome (no payload).</summary>
    public static Outcome<Unit> Success() => Outcome<Unit>.Success(Unit.Default);

    /// <summary>Creates a successful outcome carrying <paramref name="value"/>.</summary>
    public static Outcome<T> Success<T>(T value) => Outcome<T>.Success(value);

    /// <summary>Creates a failed outcome carrying <paramref name="failure"/>.</summary>
    public static Outcome<Unit> Fail(Failure failure) => Outcome<Unit>.Fail(failure);
}
