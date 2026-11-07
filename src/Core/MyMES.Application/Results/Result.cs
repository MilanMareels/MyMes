using System.Diagnostics.CodeAnalysis;

namespace MyMES.Abstractions.Results;

/// <summary>Classifies a failure so the host can map it onto a transport-specific status.</summary>
public enum ErrorKind
{
    /// <summary>Input failed validation.</summary>
    Validation,

    /// <summary>The requested resource does not exist.</summary>
    NotFound,

    /// <summary>The operation is not allowed in the current state of the aggregate.</summary>
    Conflict,

    /// <summary>The caller is not permitted to perform the operation.</summary>
    Forbidden,

    /// <summary>Something went wrong that the caller cannot act upon.</summary>
    Unexpected
}

/// <summary>
/// A single failure, carrying a stable machine-readable code alongside the human-readable text.
/// </summary>
/// <param name="Code">Stable identifier such as <c>machine.code_in_use</c>.</param>
/// <param name="Message">Description intended for a person.</param>
/// <param name="Kind">How the host should surface the failure.</param>
[SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "Error is the idiomatic name in the result pattern and this library is consumed from C# only.")]
public readonly record struct Error(string Code, string Message, ErrorKind Kind = ErrorKind.Unexpected)
{
    public static Error Validation(string code, string message) => new(code, message, ErrorKind.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorKind.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorKind.Conflict);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorKind.Forbidden);
}

/// <summary>
/// Outcome of an operation that can fail in an expected way.
/// </summary>
/// <remarks>
/// Expected failures (a duplicate code, a missing record, an illegal state transition) travel as
/// a <see cref="Result"/>; genuinely exceptional conditions still throw. That keeps control flow
/// honest without turning every business rule into an exception.
/// </remarks>
public class Result
{
    protected Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error? Error { get; }

    public static Result Success() => new(true, null);

    public static Result Failure(Error error) => new(false, error);

    public static Result<T> Success<T>(T value) => Result<T>.Ok(value);

    public static Result<T> Failure<T>(Error error) => Result<T>.Fail(error);
}

/// <summary>Outcome of an operation that returns a value on success.</summary>
/// <typeparam name="T">Type produced when the operation succeeds.</typeparam>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(bool isSuccess, T? value, Error? error) : base(isSuccess, error) => _value = value;

    /// <summary>The produced value. Reading it on a failed result is a programming error.</summary>
    /// <exception cref="InvalidOperationException">The result represents a failure.</exception>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Result carries no value: {Error?.Code}.");

    internal static Result<T> Ok(T value) => new(true, value, null);

    internal static Result<T> Fail(Error error) => new(false, default, error);
}
