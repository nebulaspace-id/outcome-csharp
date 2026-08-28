namespace NebulaSpace.Outcome;

/// <summary>
/// A result of an operation: either a success (with a <typeparamref name="T"/> value)
/// or a failure (with a <see cref="Failure"/>). Immutable — instances are created only
/// through the static factories and implicit conversions, and their state cannot change.
/// </summary>
public readonly struct Outcome<T> : IOutcome
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
            if (!_isSuccess) ThrowOnValueAccessInFailureState();
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
            if (_isSuccess) ThrowOnFailureAccessInSuccessState();
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

    // ── IOutcome — non-generic view for reflection-free dispatch (e.g. MVC filter) ──

    /// <inheritdoc />
    object? IOutcome.Value => _isSuccess ? _value : null;

    /// <inheritdoc />
    Failure IOutcome.Failure => _isSuccess ? null! : _failure!;

    /// <inheritdoc />
    bool IOutcome.IsSuccess => _isSuccess;

    private static void ThrowOnValueAccessInFailureState() =>
        throw new InvalidOperationException(
            $"Cannot access Outcome<{typeof(T).Name}>.Value because the outcome is a failure. " +
            $"Check {nameof(IsSuccess)} first, or handle both branches with {nameof(Match)}.");

    private static void ThrowOnFailureAccessInSuccessState() =>
        throw new InvalidOperationException(
            $"Cannot access Outcome<{typeof(T).Name}>.Failure because the outcome is a success. " +
            $"Check {nameof(IsSuccess)} first, or handle both branches with {nameof(Match)}.");
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

/// <summary>
/// Non-generic view over a boxed <see cref="Outcome{T}"/>, for code that must dispatch
/// without knowing <c>T</c> — e.g. the MVC <c>[OutcomeResult]</c> filter. A boxed
/// <c>Outcome{T}</c> implements this interface; check <see cref="IsSuccess"/> first, then
/// read <see cref="Value"/> (boxed payload) or <see cref="Failure"/>.
/// </summary>
public interface IOutcome
{
    /// <summary>Whether the operation succeeded. Check this before reading <see cref="Value"/> or <see cref="Failure"/>.</summary>
    bool IsSuccess { get; }

    /// <summary>The boxed payload on success, <c>null</c> on failure.</summary>
    object? Value { get; }

    /// <summary>The failure on failure, <c>null</c> on success.</summary>
    Failure Failure { get; }
}
