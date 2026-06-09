using Spirantis.Sequencing.Abstraction;

namespace Spirantis.Sequencing;

internal class SequenceBranch<TSequenceContext, TSequenceData>(
    Func<TSequenceContext, TSequenceData, ValueTask<FunctionResult>> function,
    string? branchName = null
) : ISequenceFunction<TSequenceContext, TSequenceData>
    where TSequenceContext : ISequenceContext
    where TSequenceData : ISequenceData
{
    private readonly string branchName = branchName ?? Guid.NewGuid().ToString();

    public SequenceBranch<TSequenceContext, TSequenceData>? OnTrueFunction { get; set; }
    public SequenceBranch<TSequenceContext, TSequenceData>? OnFalseFunction { get; set; }
    public SequenceBranch<TSequenceContext, TSequenceData>? OnAbortFunction { get; set; }
    public SequenceBranch<TSequenceContext, TSequenceData>? OnAnyFunction { get; set; }
    public Dictionary<
        Func<object?, bool>,
        SequenceBranch<TSequenceContext, TSequenceData>
    > OnValueFunctions { get; } = [];

    public string GetFunctionName() => branchName;

    public async ValueTask<FunctionResult> Invoke(
        TSequenceContext sequenceContext,
        TSequenceData sequenceData
    )
    {
        var result = await function.Invoke(sequenceContext, sequenceData);

        var continuationFunction = result.Type switch
        {
            FunctionResultType.True => OnTrueFunction,
            FunctionResultType.False => OnFalseFunction,
            FunctionResultType.Abort => OnAbortFunction,
            FunctionResultType.Indeterminate => GetOnValueContinuation(result.Value),
            _ => null,
        };

        continuationFunction ??= OnAnyFunction;

        return continuationFunction != null
            ? await continuationFunction.Invoke(sequenceContext, sequenceData)
            : result;
    }

    private SequenceBranch<TSequenceContext, TSequenceData>? GetOnValueContinuation(object? value)
    {
        foreach (var onValueContinuation in OnValueFunctions)
        {
            if (onValueContinuation.Key(value))
            {
                return onValueContinuation.Value;
            }
        }

        return null;
    }
}
