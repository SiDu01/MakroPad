namespace MakroPad.Core.Configuration;

public sealed record ProfileConfigResult<T>
{
    private ProfileConfigResult(T? value, ProfileConfigErrorCode? errorCode, string errorMessage)
    {
        Value = value;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess => ErrorCode is null;

    public T? Value { get; }

    public ProfileConfigErrorCode? ErrorCode { get; }

    public string ErrorMessage { get; }

    public static ProfileConfigResult<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ProfileConfigResult<T>(value, null, "");
    }

    public static ProfileConfigResult<T> Failure(ProfileConfigErrorCode errorCode, string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        return new ProfileConfigResult<T>(default, errorCode, errorMessage);
    }
}
