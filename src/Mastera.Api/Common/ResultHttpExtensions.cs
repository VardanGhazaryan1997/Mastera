using Mastera.Application.Common;

namespace Mastera.Api.Common;

public static class ResultHttpExtensions
{
    /// <summary>200 with the value, or the error as ProblemDetails.</summary>
    public static IResult ToHttp<T>(this Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
    }

    /// <summary>201 with the value, or the error as ProblemDetails.</summary>
    public static IResult ToCreated<T>(this Result<T> result, Func<T, string> location)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(location);
        return result.IsSuccess ? Results.Created(location(result.Value!), result.Value) : result.Error!.ToProblem();
    }
}
