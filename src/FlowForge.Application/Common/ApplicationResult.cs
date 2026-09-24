namespace FlowForge.Application.Common;

/// <summary>
/// Represents the outcome of an application use case.
/// </summary>
/// <typeparam name="T">The successful result value type.</typeparam>
public sealed class ApplicationResult<T>
{
    private ApplicationResult(
        bool isSuccess,
        T? value,
        IEnumerable<ApplicationError> errors)
    {
        IsSuccess = isSuccess;
        Value = value;
        Errors = Array.AsReadOnly(errors.ToArray());
    }

    /// <summary>
    /// Gets a value indicating whether the use case succeeded.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the successful value, or the default value when the use case failed.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Gets the application-level errors.
    /// </summary>
    public IReadOnlyList<ApplicationError> Errors { get; }

    /// <summary>
    /// Creates a successful application result.
    /// </summary>
    /// <param name="value">The successful value.</param>
    /// <returns>A successful result.</returns>
    public static ApplicationResult<T> Success(T value) =>
        new(true, value, Array.Empty<ApplicationError>());

    /// <summary>
    /// Creates a failed application result.
    /// </summary>
    /// <param name="errors">The application-level errors.</param>
    /// <returns>A failed result.</returns>
    public static ApplicationResult<T> Failure(IEnumerable<ApplicationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return new ApplicationResult<T>(false, default, errors);
    }

    /// <summary>
    /// Creates a failed application result containing one error.
    /// </summary>
    /// <param name="error">The application-level error.</param>
    /// <returns>A failed result.</returns>
    public static ApplicationResult<T> Failure(ApplicationError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Failure([error]);
    }
}
