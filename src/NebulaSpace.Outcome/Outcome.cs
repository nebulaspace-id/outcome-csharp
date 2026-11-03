using System.Diagnostics;

namespace NebulaSpace.Outcome;

/// <summary>
/// A result of an operation: either a success (with a <typeparamref name="T"/> value)
/// or a failure (with a <see cref="Failure"/>). Immutable — instances are created only
/// through the static factories and implicit conversions, and their state cannot change.
///
/// <para>
/// <c>default(Outcome&lt;T&gt;)</c> is not a valid value: it has neither a payload nor a
/// failure, so every member throws <see cref="InvalidOperationException"/>. Always create
/// outcomes through <see cref="Success"/> or <see cref="Fail"/>.
/// </para>
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public readonly struct Outcome<T> : IOutcome, IEquatable<Outcome<T>>
{
    private enum State : byte
    {
        Uninitialized = 0,
        Success = 1,
        Failure = 2,
    }

    private readonly T? _value;
    private readonly Failure? _failure;
    private readonly State _state;

    private Outcome(T value)
    {
        _value = value;
        _failure = null;
        _state = State.Success;
    }

    private Outcome(Failure failure)
    {
        _value = default;
        _failure = failure ?? throw new ArgumentNullException(nameof(failure));
        _state = State.Failure;
    }

    /// <summary>Whether the operation succeeded. Throws for an uninitialized <c>default</c> outcome.</summary>
    public bool IsSuccess
    {
        get
        {
            ThrowIfUninitialized();
            return _state == State.Success;
        }
    }

    /// <summary>Whether the operation failed. Throws for an uninitialized <c>default</c> outcome.</summary>
    public bool IsFailure
    {
        get
        {
            ThrowIfUninitialized();
            return _state == State.Failure;
        }
    }

    /// <summary>
    /// The payload on success. Accessing it on a failure throws <see cref="InvalidOperationException"/>.
    /// </summary>
    public T Value
    {
        get
        {
            ThrowIfUninitialized();
            if (_state != State.Success) ThrowOnValueAccessInFailureState();
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
            ThrowIfUninitialized();
            if (_state != State.Failure) ThrowOnFailureAccessInSuccessState();
            return _failure!;
        }
    }

    /// <summary>Creates a successful outcome carrying <paramref name="value"/>.</summary>
    /// <remarks>
    /// A <c>null</c> value is allowed. Use this factory when you want the null payload to be
    /// explicit at the call site.
    /// </remarks>
    public static Outcome<T> Success(T value) => new(value);

    /// <summary>Creates a failed outcome carrying <paramref name="failure"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is <c>null</c>.</exception>
    public static Outcome<T> Fail(Failure failure) => new(failure);

    /// <summary>
    /// Implicitly converts a raw value into a successful outcome, including <c>null</c>.
    /// </summary>
    /// <remarks>
    /// <c>T?</c> is a compile-time annotation only: <c>Outcome&lt;Product&gt;</c> and
    /// <c>Outcome&lt;Product?&gt;</c> share the same runtime type, so the operator cannot tell
    /// them apart. A <c>return null;</c> therefore becomes a success with a null payload.
    /// Declare the method as <c>Outcome&lt;Product?&gt;</c> when null is an intended result.
    /// </remarks>
    public static implicit operator Outcome<T>(T value) => Success(value);

    /// <summary>
    /// Implicitly converts a failed unit outcome, preserving the failure. A <b>successful</b>
    /// unit outcome has no payload, so converting it to <c>Outcome&lt;T&gt;</c> would create a
    /// success with a null/default value; that is rejected instead of silently producing one.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The unit outcome is a success — return <see cref="Outcome{Unit}"/> from the caller, or
    /// build the payload explicitly with <see cref="Success"/>.
    /// </exception>
    public static implicit operator Outcome<T>(Outcome<Unit> unit) =>
        unit.IsSuccess
            ? throw new InvalidOperationException(
                $"Cannot convert a successful Outcome<Unit> to Outcome<{typeof(T).Name}>: a unit outcome carries no payload. " +
                $"Return Outcome<Unit> from the caller, or create the payload with Outcome<{typeof(T).Name}>.Success(value).")
            : Fail(unit.Failure);

    /// <summary>Tries to read the payload. Returns <c>false</c> on failure.</summary>
    public bool TryGetValue(out T? value)
    {
        ThrowIfUninitialized();
        if (_state == State.Success)
        {
            value = _value;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>Tries to read the failure. Returns <c>false</c> on success.</summary>
    public bool TryGetFailure(out Failure? failure)
    {
        ThrowIfUninitialized();
        if (_state == State.Failure)
        {
            failure = _failure;
            return true;
        }

        failure = null;
        return false;
    }

    /// <summary>
    /// Collapses both branches into a single value. On success invokes
    /// <paramref name="onSuccess"/> with the payload; on failure invokes
    /// <paramref name="onFailure"/> with the failure.
    /// </summary>
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Failure, TOut> onFailure)
    {
        ThrowIfUninitialized();
        if (onSuccess is null) throw new ArgumentNullException(nameof(onSuccess));
        if (onFailure is null) throw new ArgumentNullException(nameof(onFailure));

        return _state == State.Success ? onSuccess(_value!) : onFailure(_failure!);
    }

    /// <summary>Deconstructs the outcome for C# pattern matching.</summary>
    public void Deconstruct(out bool isSuccess, out T? value, out Failure? failure)
    {
        ThrowIfUninitialized();
        isSuccess = _state == State.Success;
        value = _value;
        failure = _failure;
    }

    /// <summary>Value equality over the state, the payload, and the failure instance.</summary>
    public bool Equals(Outcome<T> other)
    {
        if (_state != other._state) return false;
        return _state switch
        {
            State.Success => EqualityComparer<T>.Default.Equals(_value!, other._value!),
            State.Failure => ReferenceEquals(_failure, other._failure),
            _ => true,
        };
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Outcome<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = (int)_state;
        hash = (hash * 397) ^ (_value is null ? 0 : EqualityComparer<T>.Default.GetHashCode(_value));
        hash = (hash * 397) ^ (_failure is null ? 0 : _failure.GetHashCode());
        return hash;
    }

    /// <summary>Returns <c>Success(value)</c> or <c>Failure(code)</c> for debugging.</summary>
    public override string ToString() => DebuggerDisplay;

    private string DebuggerDisplay => _state switch
    {
        State.Success => $"Success({_value})",
        State.Failure => $"Failure({_failure!.Code ?? _failure.Message})",
        _ => "Uninitialized",
    };

    // ── IOutcome — non-generic view for reflection-free dispatch (e.g. MVC filter) ──

    /// <inheritdoc />
    object? IOutcome.Value => _state == State.Success ? _value : null;

    /// <inheritdoc />
    Failure? IOutcome.Failure => _state == State.Failure ? _failure : null;

    /// <inheritdoc />
    bool IOutcome.IsSuccess
    {
        get
        {
            ThrowIfUninitialized();
            return _state == State.Success;
        }
    }

    private void ThrowIfUninitialized()
    {
        if (_state == State.Uninitialized)
        {
            throw new InvalidOperationException(
                $"This Outcome<{typeof(T).Name}> instance is uninitialized (default). " +
                $"Create outcomes with Outcome<{typeof(T).Name}>.Success(value) or Outcome<{typeof(T).Name}>.Fail(failure).");
        }
    }

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

    /// <summary>Creates a failed outcome of <typeparamref name="T"/> carrying <paramref name="failure"/>.</summary>
    public static Outcome<T> Fail<T>(Failure failure) => Outcome<T>.Fail(failure);
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
    Failure? Failure { get; }
}
