using Spirantis.Sequencing.Abstraction;

namespace Spirantis.Sequencing;

/// <summary>Entry point for constructing sequences. Use <see cref="Create{TSequenceContext, TSequenceData}"/>.</summary>
public static class SequenceBuilder
{
    /// <summary>Creates a new builder for a sequence over the given context and data types.</summary>
    /// <typeparam name="TSequenceContext">The ambient context type shared across the sequence.</typeparam>
    /// <typeparam name="TSequenceData">The per-invocation data type flowing through the sequence.</typeparam>
    /// <param name="sequenceName">An optional name for the sequence, used as the root branch name.</param>
    public static SequenceBuilder<TSequenceContext, TSequenceData> Create<
        TSequenceContext,
        TSequenceData
    >(string? sequenceName = null)
        where TSequenceContext : ISequenceContext
        where TSequenceData : ISequenceData => new(sequenceName);
}

/// <summary>
/// Builds an executable sequence by registering an initial function and its continuations, then
/// resolving them into a tree of branches via <see cref="Build"/>.
/// </summary>
/// <typeparam name="TSequenceContext">The ambient context type shared across the sequence.</typeparam>
/// <typeparam name="TSequenceData">The per-invocation data type flowing through the sequence.</typeparam>
public class SequenceBuilder<TSequenceContext, TSequenceData>
    where TSequenceContext : ISequenceContext
    where TSequenceData : ISequenceData
{
    private readonly Dictionary<
        string,
        SequenceBranchDefinition<TSequenceContext, TSequenceData>
    > branchDefinitions = [];
    private readonly Dictionary<
        string,
        Func<TSequenceContext, TSequenceData, ValueTask<FunctionResult>>
    > functions = [];

    private readonly string? sequenceName;
    private string? initialFunctionName;

    internal SequenceBuilder(string? sequenceName = null)
    {
        this.sequenceName = sequenceName;
    }

    /// <summary>Registers the initial function from an existing sequence function instance.</summary>
    /// <typeparam name="TSequenceFunction">The sequence function type.</typeparam>
    /// <param name="sequenceFunction">The function instance to start the sequence with.</param>
    /// <param name="functionName">An optional suffix appended to the function name to disambiguate the node.</param>
    public SequenceBranchDefinition<TSequenceContext, TSequenceData> Run<TSequenceFunction>(
        TSequenceFunction sequenceFunction,
        string? functionName = null
    )
        where TSequenceFunction : ISequenceFunction<TSequenceContext, TSequenceData> =>
        Register(sequenceFunction, functionName);

    /// <summary>Registers the initial function by instantiating a parameterless sequence function class.</summary>
    /// <typeparam name="TSequenceFunction">The parameterless sequence function type to instantiate.</typeparam>
    /// <param name="functionName">An optional suffix appended to the function name to disambiguate the node.</param>
    public SequenceBranchDefinition<TSequenceContext, TSequenceData> Run<TSequenceFunction>(
        string? functionName = null
    )
        where TSequenceFunction : ISequenceFunction<TSequenceContext, TSequenceData>, new() =>
        Register<TSequenceFunction>(functionName);

    /// <summary>Registers the initial function from a delegate, named after its method.</summary>
    /// <param name="function">The function to start the sequence with.</param>
    public SequenceBranchDefinition<TSequenceContext, TSequenceData> Run(
        Func<TSequenceContext, TSequenceData, ValueTask<FunctionResult>> function
    ) => Register(function);

    /// <summary>Registers the initial function from a delegate under an explicit name.</summary>
    /// <param name="function">The function to start the sequence with.</param>
    /// <param name="functionName">The name to register the function under.</param>
    public SequenceBranchDefinition<TSequenceContext, TSequenceData> Run(
        Func<TSequenceContext, TSequenceData, ValueTask<FunctionResult>> function,
        string functionName
    ) => Register(function, functionName);

    internal SequenceBranchDefinition<TSequenceContext, TSequenceData> Register<TSequenceFunction>(
        TSequenceFunction sequenceFunction,
        string? functionName = null
    )
        where TSequenceFunction : ISequenceFunction<TSequenceContext, TSequenceData> =>
        Register(
            sequenceFunction.Invoke,
            sequenceFunction.GetFunctionName() + (functionName ?? string.Empty)
        );

    internal SequenceBranchDefinition<TSequenceContext, TSequenceData> Register<TSequenceFunction>(
        string? functionName
    )
        where TSequenceFunction : ISequenceFunction<TSequenceContext, TSequenceData>, new()
    {
        var sequenceFunction = new TSequenceFunction();
        return Register(
            sequenceFunction.Invoke,
            sequenceFunction.GetFunctionName() + (functionName ?? string.Empty)
        );
    }

    internal SequenceBranchDefinition<TSequenceContext, TSequenceData> Register(
        Func<TSequenceContext, TSequenceData, ValueTask<FunctionResult>> function
    )
    {
        ArgumentNullException.ThrowIfNull(function);

        return Register(function, function.Method.Name);
    }

    internal SequenceBranchDefinition<TSequenceContext, TSequenceData> Register(
        Func<TSequenceContext, TSequenceData, ValueTask<FunctionResult>> function,
        string functionName
    )
    {
        ArgumentNullException.ThrowIfNull(function);

        if (functions.Count == 0)
        {
            initialFunctionName = functionName;
        }

        functions.TryAdd(functionName, function);

        if (!branchDefinitions.TryGetValue(functionName, out var branch))
        {
            branch = new SequenceBranchDefinition<TSequenceContext, TSequenceData>(this);
            branchDefinitions.Add(functionName, branch);
        }

        return branch;
    }

    /// <summary>Resolves the registered functions and continuations into an executable sequence.</summary>
    /// <returns>The root sequence function to invoke.</returns>
    /// <exception cref="InvalidOperationException">No initial function has been registered.</exception>
    public ISequenceFunction<TSequenceContext, TSequenceData> Build()
    {
        if (
            string.IsNullOrWhiteSpace(initialFunctionName)
            || !functions.TryGetValue(initialFunctionName, out var initialFunction)
        )
        {
            throw new InvalidOperationException("No initial function present");
        }

        return BuildBranch(initialFunctionName, initialFunction, sequenceName);
    }

    private SequenceBranch<TSequenceContext, TSequenceData> BuildBranch(
        string functionName,
        Func<TSequenceContext, TSequenceData, ValueTask<FunctionResult>> function,
        string? branchName = null
    )
    {
        var branch = new SequenceBranch<TSequenceContext, TSequenceData>(function, branchName);

        if (branchDefinitions.TryGetValue(functionName, out var sequenceBuilderBranch))
        {
            branch.OnTrueFunction = GetFunctionAndBuildBranch(
                sequenceBuilderBranch.OnTrueFunctionName
            );
            branch.OnFalseFunction = GetFunctionAndBuildBranch(
                sequenceBuilderBranch.OnFalseFunctionName
            );
            branch.OnAbortFunction = GetFunctionAndBuildBranch(
                sequenceBuilderBranch.OnAbortFunctionName
            );
            branch.OnAnyFunction = GetFunctionAndBuildBranch(
                sequenceBuilderBranch.OnAnyFunctionName
            );

            foreach (var keyValuePair in sequenceBuilderBranch.OnValueFunctionNames)
            {
                var onValueFunction = GetFunctionAndBuildBranch(keyValuePair.Value);

                if (onValueFunction != null)
                {
                    branch.OnValueFunctions.Add(keyValuePair.Key, onValueFunction);
                }
            }
        }

        return branch;
    }

    private SequenceBranch<TSequenceContext, TSequenceData>? GetFunctionAndBuildBranch(
        string? functionName
    )
    {
        if (
            !string.IsNullOrWhiteSpace(functionName)
            && functions.TryGetValue(functionName, out var function)
        )
        {
            return BuildBranch(functionName, function);
        }

        return null;
    }
}
