namespace Mastera.Application.Common;

public enum ErrorKind
{
    Validation,
    Conflict,
    NotFound,
    Unauthorized,
    Forbidden,
}

/// <summary>An expected failure of a use case. <see cref="Code"/> is stable and translated by the UI.</summary>
public sealed record Error(string Code, string Message, ErrorKind Kind)
{
    public static Error Validation(string code, string message) => new(code, message, ErrorKind.Validation);

    public static Error Conflict(string code, string message) => new(code, message, ErrorKind.Conflict);

    public static Error NotFound(string code, string message) => new(code, message, ErrorKind.NotFound);
}

public sealed class Result<T>
{
    internal Result(T? value, Error? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public Error? Error { get; }

    public bool IsSuccess => Error is null;
}

public static class Result
{
    public static Result<T> Success<T>(T value) => new(value, null);

    public static Result<T> Failure<T>(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(default, error);
    }
}
