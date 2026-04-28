using Microsoft.AspNetCore.Http;

namespace HackerNewsApi.ViewModels;

public static class ApiResults
{
    public static IResult Ok<T>(T data)
    {
        return Results.Ok(new ResponseViewModel<T>(StatusCodes.Status200OK, data));
    }

    public static IResult Created<T>(string location, T data)
    {
        return Results.Created(location, new ResponseViewModel<T>(StatusCodes.Status201Created, data));
    }

    public static IResult BadRequest(
        string message,
        int codeError = StatusCodes.Status400BadRequest,
        string errorKey = "badrequest")
    {
        return Error(StatusCodes.Status400BadRequest, message, codeError, errorKey);
    }

    public static IResult ValidationError(
        IReadOnlyDictionary<string, string[]> errors,
        string message = "Validation failed.",
        IReadOnlyDictionary<string, string[]>? errorKeys = null,
        string errorKey = "validation_failed")
    {
        return Error(
            StatusCodes.Status400BadRequest,
            message,
            StatusCodes.Status400BadRequest,
            errorKey,
            errors,
            errorKeys);
    }

    public static IResult Unauthorized(
        string message = "Unauthorized",
        string errorKey = "unauthorized")
    {
        return Error(StatusCodes.Status401Unauthorized, message, StatusCodes.Status401Unauthorized, errorKey);
    }

    public static IResult Forbidden(
        string message = "Forbidden",
        string errorKey = "forbidden")
    {
        return Error(StatusCodes.Status403Forbidden, message, StatusCodes.Status403Forbidden, errorKey);
    }

    public static IResult NotFound(
        string message = "Resource not found",
        string errorKey = "not_found")
    {
        return Error(StatusCodes.Status404NotFound, message, StatusCodes.Status404NotFound, errorKey);
    }

    public static IResult Error(int statusCode, string message, int codeError)
    {
        return Error(statusCode, message, codeError, message, null, null);
    }

    public static IResult Error(int statusCode, string message, int codeError, string errorKey)
    {
        return Error(statusCode, message, codeError, errorKey, null, null);
    }

    public static IResult Error(
        int statusCode,
        string message,
        int codeError,
        string errorKey,
        IReadOnlyDictionary<string, string[]>? errors,
        IReadOnlyDictionary<string, string[]>? errorKeys)
    {
        var resolvedErrorKey = string.IsNullOrWhiteSpace(errorKey)
            ? string.Empty
            : errorKey;

        var errorViewModel = errors is null
            ? new ErrorViewModel(message, codeError, resolvedErrorKey)
            : new ErrorViewModel(message, codeError, resolvedErrorKey, errors, errorKeys);
        var payload = new ResponseViewModel<object?>(statusCode, errorViewModel);
        return Results.Json(payload, statusCode: statusCode);
    }
}
