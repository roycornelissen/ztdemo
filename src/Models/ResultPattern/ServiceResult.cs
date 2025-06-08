using System.Diagnostics.CodeAnalysis;

namespace Models.ResultPattern;

public class ServiceResult<T>
{
    public ErrorResponse? Error { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess { get; }
    
    public T? Value { get; }
    
    private ServiceResult(T value)
    {
        Value = value;
        IsSuccess = true;
    }

    private ServiceResult(ErrorResponse error)
    {
        Error = error;
        IsSuccess = false;
    }
    
    public static ServiceResult<T> Ok(T value) => new(value);
    private static ServiceResult<T> Fail(ErrorType type, string error) => new(new ErrorResponse(error, type));
    public static ServiceResult<T> NotFound(string error) => Fail(ErrorType.NotFound, error);
    public static ServiceResult<T> Forbidden(string error) => Fail(ErrorType.Forbidden, error);
    public static ServiceResult<T> Invalid(string error) => Fail(ErrorType.Invalid, error);
    public static ServiceResult<T> InternalServerError(string error) => Fail(ErrorType.InternalServerError, error);
    public static implicit operator ServiceResult<T>(T value) => Ok(value);
    
}

public record ErrorResponse(string Message, ErrorType ErrorType);

public enum ErrorType
{
    NotFound,
    Invalid,
    Unauthorized,
    Forbidden,
    Conflict,
    InternalServerError
}