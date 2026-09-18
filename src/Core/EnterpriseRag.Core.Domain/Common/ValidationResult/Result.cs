
using EnterpriseRag.Core.Domain.Common.Errors;

namespace EnterpriseRag.Core.Domain.Common;

public class Result
{
    public IReadOnlyCollection<Error> Errors { get; }
    public bool IsSuccess => Errors == null || Errors.Count == 0;
    public bool IsFailure => !IsSuccess;
    public string Error => Errors.FirstOrDefault()?.Description ?? string.Empty;

    public Result(IReadOnlyCollection<Error> errors)
    {
        Errors = errors ?? new List<Error>();
    }

    public static Result Success()
        => new(new List<Error>());

    public static Result Failure(params Error[] errors)
        => new(errors);

    public static Result Failure(IEnumerable<Error> errors)
        => new(errors.ToList());

    public static Result Failure(string errorMessage)
        => new(new List<Error> { new("General.Error", errorMessage) });
}

public class Result<T> : Result
{
    public T? Value { get; }

    public Result(T? value, IReadOnlyCollection<Error> errors) : base(errors)
    {
        Value = value;
    }

    public static Result<T> Success(T value)
        => new(value, new List<Error>());

    public static new Result<T> Failure(params Error[] errors)
        => new(default, errors);

    public static new Result<T> Failure(IEnumerable<Error> errors)
        => new(default, errors.ToList());

    public static new Result<T> Failure(string errorMessage)
        => new(default, new List<Error> { new("General.Error", errorMessage) });
}
