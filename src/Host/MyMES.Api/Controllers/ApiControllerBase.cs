using Microsoft.AspNetCore.Mvc;
using MyMES.Abstractions.Results;

namespace MyMES.Api.Controllers;

/// <summary>Shared mapping from a <see cref="Result"/> to an HTTP response.</summary>
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult ToOk<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result.Value) : ToProblem(result);

    protected IActionResult ToCreated<T>(Result<T> result, Func<T, string> location) =>
        result.IsSuccess ? Created(location(result.Value), result.Value) : ToProblem(result);

    private ObjectResult ToProblem(Result result)
    {
        var error = result.Error!.Value;
        return Problem(
            title: TitleFor(error.Kind),
            detail: error.Message,
            statusCode: StatusFor(error.Kind));
    }

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
