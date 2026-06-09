using Spirantis.Result;

namespace Spirantis.Sequencing;

/// <summary>
/// The result of a sequence function: a <see cref="Result{T}"/> over <see cref="object"/> tagged with a
/// <see cref="FunctionResultType"/> that determines which continuation branch the sequence runs next.
/// </summary>
public class FunctionResult : Result<object>
{
    /// <summary>Creates a result with the given <paramref name="type"/> and success state, carrying no value.</summary>
    /// <param name="type">The outcome category that drives branch selection.</param>
    /// <param name="isSuccess"><c>true</c> for success; <c>false</c> for failure.</param>
    public FunctionResult(FunctionResultType type, bool isSuccess)
        : base(isSuccess)
    {
        Type = type;
    }

    /// <summary>Creates a successful result with the given <paramref name="type"/>, carrying <paramref name="value"/>.</summary>
    /// <param name="type">The outcome category that drives branch selection.</param>
    /// <param name="value">The produced value, inspected by value predicates.</param>
    public FunctionResult(FunctionResultType type, object value)
        : base(value)
    {
        Type = type;
    }

    /// <summary>Creates a failed result with the given <paramref name="type"/>, carrying <paramref name="error"/>.</summary>
    /// <param name="type">The outcome category that drives branch selection.</param>
    /// <param name="error">The error describing the failure.</param>
    public FunctionResult(FunctionResultType type, Error error)
        : base(error)
    {
        Type = type;
    }

    /// <summary>The outcome category that determines which continuation branch runs next.</summary>
    public FunctionResultType Type { get; init; }

    /// <summary>Creates a failed <see cref="FunctionResultType.Abort"/> result with no value.</summary>
    public static FunctionResult Abort() => new(FunctionResultType.Abort, isSuccess: false);

    /// <summary>Creates an <see cref="FunctionResultType.Abort"/> result carrying <paramref name="value"/>.</summary>
    /// <param name="value">The produced value.</param>
    public static FunctionResult Abort(object value) => new(FunctionResultType.Abort, value);

    /// <summary>Creates a failed <see cref="FunctionResultType.Abort"/> result from <paramref name="error"/>.</summary>
    /// <param name="error">The error describing the failure.</param>
    public static FunctionResult Abort(Error error) => new(FunctionResultType.Abort, error);

    /// <summary>Creates a successful <see cref="FunctionResultType.False"/> result with no value.</summary>
    public static FunctionResult False() => new(FunctionResultType.False, isSuccess: true);

    /// <summary>Creates a <see cref="FunctionResultType.False"/> result carrying <paramref name="value"/>.</summary>
    /// <param name="value">The produced value.</param>
    public static FunctionResult False(object value) => new(FunctionResultType.False, value);

    /// <summary>Creates a successful <see cref="FunctionResultType.True"/> result with no value.</summary>
    public static FunctionResult True() => new(FunctionResultType.True, isSuccess: true);

    /// <summary>Creates a <see cref="FunctionResultType.True"/> result carrying <paramref name="value"/>.</summary>
    /// <param name="value">The produced value.</param>
    public static FunctionResult True(object value) => new(FunctionResultType.True, value);

    /// <summary>Creates a successful <see cref="FunctionResultType.Indeterminate"/> result with no value.</summary>
    public static FunctionResult Indeterminate() =>
        new(FunctionResultType.Indeterminate, isSuccess: true);

    /// <summary>
    /// Creates an <see cref="FunctionResultType.Indeterminate"/> result carrying <paramref name="value"/>,
    /// which value predicates registered via <c>IfValueRun</c> inspect to select the next branch.
    /// </summary>
    /// <param name="value">The produced value.</param>
    public static FunctionResult Indeterminate(object value) =>
        new(FunctionResultType.Indeterminate, value);

    /// <summary>Implicitly wraps a <see cref="FunctionResult"/> in a completed <see cref="ValueTask{T}"/>.</summary>
    /// <param name="functionResult">The result to wrap.</param>
    public static implicit operator ValueTask<FunctionResult>(FunctionResult functionResult) =>
        new(functionResult);
}
