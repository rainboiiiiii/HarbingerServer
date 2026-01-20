using Microsoft.AspNetCore.Http;

namespace GameBackend.Api.Services;

public static class ApiResults
{
    public static IResult Error(string message, int statusCode)
    {
        return Results.Json(new { error = message }, statusCode: statusCode);
    }

    public static IResult Ok(object value)
    {
        return Results.Json(value);
    }
}
