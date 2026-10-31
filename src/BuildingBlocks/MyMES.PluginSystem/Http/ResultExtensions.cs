using Microsoft.AspNetCore.Http;
using MyMES.Abstractions.Results;

namespace MyMES.PluginSystem.Http;

/// <summary>
/// Turns an application <see cref="Result"/> into an HTTP response.
/// </summary>
/// <remarks>
/// Every endpoint, in the host and in any module or plugin, goes through here, so a failed
/// operation produces the same <c>ProblemDetails</c> shape and the same status code everywhere.
/// </remarks>
public static class ResultExtensions
{
    /// <summary>Maps a failure to its status code and <c>ProblemDetails</c> body.</summary>
    public static IResult ToProblem(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess)
        {
            throw new InvalidOperationException("ToProblem was called on a successful result.");
        }

        var error = result.Error!.Value;
        var status = StatusFor(error.Kind);

        return Results.Problem(
            title: TitleFor(error.Kind),
            detail: error.Message,
            statusCode: status,
            // The machine-readable code is what a client should branch on, not the message.
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }

    /// <summary>Returns <c>200 OK</c> with the value, or the mapped problem.</summary>
    public static IResult ToOk<T>(this Result<T> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : ((Result)result).ToProblem();

    /// <summary>Returns <c>201 Created</c> at <paramref name="location"/>, or the mapped problem.</summary>
    public static IResult ToCreated<T>(this Result<T> result, Func<T, string> location) =>
        result.IsSuccess
            ? Results.Created(location(result.Value), result.Value)
            : ((Result)result).ToProblem();

    private static int StatusFor(ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => StatusCodes.Status400BadRequest,
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status500InternalServerError
    };

    private static string TitleFor(ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => "Invalid request",
        ErrorKind.NotFound => "Not found",
        ErrorKind.Conflict => "Conflict",
        ErrorKind.Forbidden => "Forbidden",
        _ => "Unexpected error"
    };
}
