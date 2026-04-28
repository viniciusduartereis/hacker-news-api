using System.Text.Json.Serialization;

namespace HackerNewsApi.ViewModels;

/// <summary>
/// A generic response model to standardize API responses, including success and error cases.
/// </summary>
/// <typeparam name="T"></typeparam>
public class ResponseViewModel<T>
{
    public int StatusCode { get; set; }
    public string Message => GetMessage();
    public T? Data { get; set; }
    public ErrorViewModel? Error { get; set; }

    public ResponseViewModel(int statusCode, T data)
    {
        StatusCode = statusCode;
        Data = data;
        Error = null;
    }

    public ResponseViewModel(int statusCode, ErrorViewModel error)
    {
        StatusCode = statusCode;
        Error = error;
        Data = default;
    }

    private string GetMessage()
    {
        if (StatusCode >= 200 && StatusCode < 300) return "Success";
        else if (StatusCode >= 400 && StatusCode < 500) return "Client Error";
        else if (StatusCode >= 500) return "Server Error";
        else return "Unknown Status";
    }
}

public class ErrorViewModel
{
    public string Message { get; set; }
    public int CodeError { get; set; }
    public string ErrorKey { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string[]>? Errors { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string[]>? ErrorKeys { get; set; }

    public ErrorViewModel(string message, int codeError, string errorKey)
    {
        Message = message;
        CodeError = codeError;
        ErrorKey = errorKey;
        Errors = null;
        ErrorKeys = null;
    }

    public ErrorViewModel(
        string message,
        int codeError,
        string errorKey,
        IReadOnlyDictionary<string, string[]> errors,
        IReadOnlyDictionary<string, string[]>? errorKeys = null)
    {
        Message = message;
        CodeError = codeError;
        ErrorKey = errorKey;
        Errors = errors;
        ErrorKeys = errorKeys;
    }
}
