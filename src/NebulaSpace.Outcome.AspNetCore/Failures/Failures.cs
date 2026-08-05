namespace NebulaSpace.Outcome.AspNetCore.Failures;

public sealed class NotFoundFailure : Failure
{
    public NotFoundFailure(string message) => Message = message;
    public string Message { get; }
}

public sealed class ValidationFailure : Failure
{
    public ValidationFailure(string message, Dictionary<string, string> errors)
    {
        Message = message;
        Errors = errors;
    }
    public string Message { get; }
    public Dictionary<string, string> Errors { get; }
}

public sealed class ConflictFailure : Failure
{
    public ConflictFailure(string message) => Message = message;
    public string Message { get; }
}

public sealed class BadRequestFailure : Failure
{
    public BadRequestFailure(string message, Dictionary<string, string>? errors = null)
    {
        Message = message;
        Errors = errors;
    }
    public string Message { get; }
    public Dictionary<string, string>? Errors { get; }
}

public sealed class UnauthorizedFailure : Failure
{
    public UnauthorizedFailure(string message) => Message = message;
    public string Message { get; }
}

public sealed class ForbiddenFailure : Failure
{
    public ForbiddenFailure(string message) => Message = message;
    public string Message { get; }
}
