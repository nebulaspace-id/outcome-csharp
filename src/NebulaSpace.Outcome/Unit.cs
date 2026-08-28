namespace NebulaSpace.Outcome;

/// <summary>
/// The unit type: a success with no payload.
///
/// <para>
/// Use <c>Outcome&lt;Unit&gt;</c> (created with <c>Outcome.Success()</c>) for operations
/// that have no meaningful return value — create/delete/send actions that just succeed or
/// fail. An <c>Outcome&lt;Unit&gt;</c> converts implicitly to <c>Outcome&lt;T&gt;</c>,
/// so a no-payload outcome can be adapted wherever a payload outcome is expected.
/// </para>
/// </summary>
public readonly struct Unit
{
    /// <summary>The singleton value of <see cref="Unit"/>.</summary>
    public static Unit Default => default;
}
