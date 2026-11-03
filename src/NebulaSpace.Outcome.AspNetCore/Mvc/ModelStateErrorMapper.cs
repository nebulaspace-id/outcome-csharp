using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace NebulaSpace.Outcome.AspNetCore.Mvc;

/// <summary>
/// Flattens ASP.NET Core's <see cref="ModelStateDictionary"/> into the field-keyed error
/// shape used by <c>FailureDetails</c>, so binding and validation failures can be rendered
/// by the configured error envelope.
/// </summary>
internal static class ModelStateErrorMapper
{
    // ModelState reports body-level failures (malformed JSON, missing body) under an empty
    // key. "request" is friendlier in a client payload than an empty property name.
    private const string RequestKey = "request";

    /// <summary>
    /// Returns one entry per failed field, each holding that field's messages. Returns
    /// <c>null</c> when the model state has no usable message, so the envelope omits
    /// the <c>errors</c> member entirely.
    /// </summary>
    public static IReadOnlyDictionary<string, string[]>? ToErrorDictionary(ModelStateDictionary modelState)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        foreach (var entry in modelState)
        {
            var messages = new List<string>(entry.Value.Errors.Count);
            foreach (var error in entry.Value.Errors)
            {
                var message = string.IsNullOrEmpty(error.ErrorMessage)
                    ? error.Exception?.Message
                    : error.ErrorMessage;

                if (!string.IsNullOrEmpty(message))
                    messages.Add(message);
            }

            if (messages.Count == 0)
                continue;

            errors[string.IsNullOrEmpty(entry.Key) ? RequestKey : entry.Key] = messages.ToArray();
        }

        return errors.Count == 0 ? null : errors;
    }
}
