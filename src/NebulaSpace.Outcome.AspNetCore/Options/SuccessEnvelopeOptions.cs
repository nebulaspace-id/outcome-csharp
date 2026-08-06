namespace NebulaSpace.Outcome.AspNetCore.Options;

/// <summary>
/// Configuration for the success envelope: whether successful responses are wrapped in
/// <c>SuccessResponse&lt;T&gt;</c>. When disabled, <c>Ok</c>/<c>Created</c> return the raw
/// payload. Error responses always use an envelope regardless of this setting.
/// </summary>
public sealed class SuccessEnvelopeOptions
{
    /// <summary>
    /// Whether success responses are wrapped in <c>SuccessResponse&lt;T&gt;</c>.
    /// Default <c>true</c>.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
