namespace NebulaSpace.Outcome;

/// <summary>
/// The unit type: a success with no payload.
///
/// <para>
/// Use <c>Outcome&lt;Unit&gt;</c> (created with <c>Outcome.Success()</c>) for operations
/// that have no meaningful return value — create/delete/send actions that just succeed or
/// fail. A failed <c>Outcome&lt;Unit&gt;</c> converts implicitly to <c>Outcome&lt;T&gt;</c>,
/// so a failure can be lifted wherever a payload outcome is expected. A successful unit
/// outcome has no payload, so lifting it throws instead of producing a null success.
/// </para>
///
/// <para>
/// Every entry point maps a successful <c>Outcome&lt;Unit&gt;</c> to <c>204 No Content</c>:
/// the <c>[OutcomeResult]</c> filter and the payload-less
/// <c>ToResult</c>/<c>ToActionResult</c> overloads.
/// </para>
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    /// <summary>The singleton value of <see cref="Unit"/>.</summary>
    public static Unit Default => default;

    /// <inheritdoc />
    public bool Equals(Unit other) => true;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Unit;

    /// <inheritdoc />
    public override int GetHashCode() => 0;

    /// <summary>Returns <c>()</c>, the conventional unit value.</summary>
    public override string ToString() => "()";
}
