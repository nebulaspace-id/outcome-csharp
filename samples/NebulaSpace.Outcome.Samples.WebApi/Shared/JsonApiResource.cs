using System.Text.Json.Serialization;

namespace NebulaSpace.Outcome.Samples.WebApi.Shared;

/// <summary>
/// JSON:API resource object carried inside <c>data</c>: a stable <see cref="Type"/>,
/// a string <see cref="Id"/>, and the resource's own <see cref="Attributes"/>.
///
/// <para>
/// This is a sample type, not part of the library. The response envelope stays
/// <see cref="SuccessResponse{TData}"/>, so the body is hybrid — the app's
/// <c>statusCode</c>/<c>meta</c> plus a JSON:API-shaped <c>data</c>. It is not a
/// strict JSON:API document.
/// </para>
///
/// <para>
/// <see cref="Attributes"/> must not repeat the identifier or the type: those belong
/// to the resource object, not to its attributes. Use a dedicated attributes record
/// (for example <c>ProductAttributes</c>) instead of the domain model.
/// </para>
/// </summary>
/// <typeparam name="TAttributes">The resource's attribute shape.</typeparam>
public sealed class JsonApiResource<TAttributes>
{
    /// <summary>Stable resource type name, e.g. <c>products</c>.</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Resource identifier — JSON:API requires a string.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>The resource's attributes, excluding <c>id</c> and <c>type</c>.</summary>
    [JsonPropertyName("attributes")]
    public required TAttributes Attributes { get; init; }
}

/// <summary>
/// Static factory for <see cref="JsonApiResource{TAttributes}"/>. Two overloads so a
/// caller can pass the domain identifier directly — <see cref="Guid"/> or
/// <see cref="string"/> — without repeating the conversion at every call site.
/// </summary>
public static class JsonApiResource
{
    /// <summary>Creates a resource object from a <see cref="Guid"/> identifier.</summary>
    /// <param name="type">Stable resource type name, e.g. <c>products</c>.</param>
    /// <param name="id">Resource identifier.</param>
    /// <param name="attributes">The resource's attributes.</param>
    public static JsonApiResource<TAttributes> Create<TAttributes>(
        string type,
        Guid id,
        TAttributes attributes) =>
        Create(type, id.ToString(), attributes);

    /// <summary>Creates a resource object from an already-stringified identifier.</summary>
    /// <param name="type">Stable resource type name, e.g. <c>products</c>.</param>
    /// <param name="id">Resource identifier.</param>
    /// <param name="attributes">The resource's attributes.</param>
    public static JsonApiResource<TAttributes> Create<TAttributes>(
        string type,
        string id,
        TAttributes attributes) =>
        new()
        {
            Type = type,
            Id = id,
            Attributes = attributes,
        };
}
