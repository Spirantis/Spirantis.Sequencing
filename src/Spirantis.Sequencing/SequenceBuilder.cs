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
        where TSequenceContext : ISequenceContext => new(sequenceName);
}

/// <summary>
/// Builds an executable sequence by registering an initial function and its continuations, then
/// resolving them into a tree of branches via <see cref="Build"/>.
/// </summary>
/// <typeparam name="TSequenceContext">The ambient context type shared across the sequence.</typeparam>
/// <typeparam name="TSequenceData">The per-invocation data type flowing through the sequence.</typeparam>
public class SequenceBuilder<TSequenceContext, TSequenceData>
    where TSequenceContext : ISequenceContext
{
    private readonly Dictionary<
        string,
        SequenceBranchDefinition<TSequenceContext, TSequenceData>
    > branchDefinitions = [];
    private readonly Dictionary<
        string,
        Func<TSequenceContext, TSequenceData, CancellationToken, ValueTask<FunctionResult>>
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
        Func<TSequenceContext, TSequenceData, CancellationToken, ValueTask<FunctionResult>> function
    ) => Register(function);

    /// <summary>Registers the initial function from a delegate under an explicit name.</summary>
    /// <param name="function">The function to start the sequence with.</param>
    /// <param name="functionName">The name to register the function under.</param>
    public SequenceBranchDefinition<TSequenceContext, TSequenceData> Run(
        Func<
            TSequenceContext,
            TSequenceData,
            CancellationToken,
            ValueTask<FunctionResult>
        > function,
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
        Func<TSequenceContext, TSequenceData, CancellationToken, ValueTask<FunctionResult>> function
    )
    {
        ArgumentNullException.ThrowIfNull(function);

        return Register(function, function.Method.Name);
    }

    internal SequenceBranchDefinition<TSequenceContext, TSequenceData> Register(
        Func<
            TSequenceContext,
            TSequenceData,
            CancellationToken,
            ValueTask<FunctionResult>
        > function,
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

        return BuildBranch(
            initialFunctionName,
            initialFunction,
            [],
            new Dictionary<string, SequenceBranch<TSequenceContext, TSequenceData>>(),
            sequenceName
        );
    }

    private SequenceBranch<TSequenceContext, TSequenceData> BuildBranch(
        string functionName,
        Func<
            TSequenceContext,
            TSequenceData,
            CancellationToken,
            ValueTask<FunctionResult>
        > function,
        List<string> path,
        Dictionary<string, SequenceBranch<TSequenceContext, TSequenceData>> built,
        string? branchName = null
    )
    {
        // A name already built is a completed, acyclic subtree: reuse it so shared/diamond
        // continuations are built once instead of re-expanded per reference.
        if (built.TryGetValue(functionName, out var cached))
        {
            return cached;
        }

        // `path` is the current root-to-node chain. If this name is already on it, a continuation
        // points back at one of its own ancestors — a cycle the engine cannot execute. Report the
        // whole loop (e.g. "A -> B -> A") so the offending link is obvious.
        var cycleStart = path.IndexOf(functionName);
        if (cycleStart >= 0)
        {
            var cycle = path.GetRange(cycleStart, path.Count - cycleStart);
            cycle.Add(functionName);
            throw new InvalidOperationException(
                $"The sequence has a cycle: {string.Join(" -> ", cycle)}."
            );
        }

        path.Add(functionName);

        var branch = new SequenceBranch<TSequenceContext, TSequenceData>(function, branchName);

        if (branchDefinitions.TryGetValue(functionName, out var sequenceBuilderBranch))
        {
            branch.OnTrueFunction = GetFunctionAndBuildBranch(
                sequenceBuilderBranch.OnTrueFunctionName,
                path,
                built
            );
            branch.OnFalseFunction = GetFunctionAndBuildBranch(
                sequenceBuilderBranch.OnFalseFunctionName,
                path,
                built
            );
            branch.OnAbortFunction = GetFunctionAndBuildBranch(
                sequenceBuilderBranch.OnAbortFunctionName,
                path,
                built
            );
            branch.OnAnyFunction = GetFunctionAndBuildBranch(
                sequenceBuilderBranch.OnAnyFunctionName,
                path,
                built
            );

            foreach (var (predicate, name) in sequenceBuilderBranch.OnValueFunctionNames)
            {
                var onValueFunction = GetFunctionAndBuildBranch(name, path, built);

                if (onValueFunction != null)
                {
                    branch.OnValueFunctions.Add((predicate, onValueFunction));
                }
            }
        }

        // Pop before returning so sibling branches may legitimately reuse this name (a DAG/diamond
        // is fine; only a name appearing on its own ancestor path is a cycle).
        path.RemoveAt(path.Count - 1);
        built[functionName] = branch;
        return branch;
    }

    private SequenceBranch<TSequenceContext, TSequenceData>? GetFunctionAndBuildBranch(
        string? functionName,
        List<string> path,
        Dictionary<string, SequenceBranch<TSequenceContext, TSequenceData>> built
    )
    {
        if (
            !string.IsNullOrWhiteSpace(functionName)
            && functions.TryGetValue(functionName, out var function)
        )
        {
            return BuildBranch(functionName, function, path, built);
        }

        return null;
    }
}
