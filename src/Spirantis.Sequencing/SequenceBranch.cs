using Spirantis.Sequencing.Abstraction;

namespace Spirantis.Sequencing;

internal sealed class SequenceBranch<TSequenceContext, TSequenceData>
    : ISequenceFunction<TSequenceContext, TSequenceData>
    where TSequenceContext : ISequenceContext
    where TSequenceData : ISequenceData
{
    private readonly Func<
        TSequenceContext,
        TSequenceData,
        CancellationToken,
        ValueTask<FunctionResult>
    > function;
    private readonly string branchName;

    public SequenceBranch(
        Func<
            TSequenceContext,
            TSequenceData,
            CancellationToken,
            ValueTask<FunctionResult>
        > function,
        string? branchName = null
    )
    {
        this.function = function;
        this.branchName = branchName ?? Guid.NewGuid().ToString();
    }

    public SequenceBranch<TSequenceContext, TSequenceData>? OnTrueFunction { get; set; }
    public SequenceBranch<TSequenceContext, TSequenceData>? OnFalseFunction { get; set; }
    public SequenceBranch<TSequenceContext, TSequenceData>? OnAbortFunction { get; set; }
    public SequenceBranch<TSequenceContext, TSequenceData>? OnAnyFunction { get; set; }
    public List<(
        Func<object?, bool> Predicate,
        SequenceBranch<TSequenceContext, TSequenceData> Branch
    )> OnValueFunctions { get; } = [];

    public string GetFunctionName() => branchName;

    public async ValueTask<FunctionResult> Invoke(
        TSequenceContext sequenceContext,
        TSequenceData sequenceData,
        CancellationToken cancellationToken = default
    )
    {
        // Each node has at most one continuation, so the sequence is a tail-walk: run it as a loop
        // (O(1) stack, a single async frame) rather than recursing once per node. Completed
        // ValueTasks are taken synchronously to avoid the await machinery on the common path.
        var current = this;
        while (true)
        {
            // Stop promptly between steps even if a function doesn't observe the token itself.
            cancellationToken.ThrowIfCancellationRequested();

            var pending = current.function.Invoke(sequenceContext, sequenceData, cancellationToken);
            var result = pending.IsCompletedSuccessfully ? pending.Result : await pending;

            var next = current.SelectContinuation(result);
            if (next is null)
            {
                return result;
            }

            current = next;
        }
    }

    private SequenceBranch<TSequenceContext, TSequenceData>? SelectContinuation(
        FunctionResult result
    )
    {
        var continuation = result.Type switch
        {
            FunctionResultType.True => OnTrueFunction,
            FunctionResultType.False => OnFalseFunction,
            FunctionResultType.Abort => OnAbortFunction,
            FunctionResultType.Indeterminate => GetOnValueContinuation(result.Value),
            _ => null,
        };

        return continuation ?? OnAnyFunction;
    }

    private SequenceBranch<TSequenceContext, TSequenceData>? GetOnValueContinuation(object? value)
    {
        foreach (var (predicate, branch) in OnValueFunctions)
        {
            if (predicate(value))
            {
                return branch;
            }
        }

        return null;
    }
}
