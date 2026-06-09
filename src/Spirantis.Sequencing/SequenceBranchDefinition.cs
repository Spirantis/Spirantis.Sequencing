using Spirantis.Sequencing.Abstraction;

namespace Spirantis.Sequencing;

/// <summary>
/// Fluent description of a single node in a sequence and the reactions that follow it. Returned by the
/// <see cref="SequenceBuilder{TSequenceContext, TSequenceData}"/> registration methods; chain
/// <c>If*Run</c> / <c>After</c> calls to declare continuations, then call <see cref="Build"/>.
/// </summary>
/// <typeparam name="TSequenceContext">The ambient context type shared across the sequence.</typeparam>
/// <typeparam name="TSequenceData">The per-invocation data type flowing through the sequence.</typeparam>
public class SequenceBranchDefinition<TSequenceContext, TSequenceData>
    where TSequenceContext : ISequenceContext
    where TSequenceData : ISequenceData
{
    private readonly SequenceBuilder<TSequenceContext, TSequenceData> builder;

    internal SequenceBranchDefinition(SequenceBuilder<TSequenceContext, TSequenceData> builder)
    {
        this.builder = builder;
    }

    /// <summary>Name of the function to run when this node returns <see cref="FunctionResultType.Abort"/>.</summary>
    public string? OnAbortFunctionName { get; private set; }

    /// <summary>Name of the function to run on any result type when no specific branch matched.</summary>
    public string? OnAnyFunctionName { get; private set; }

    /// <summary>Name of the function to run when this node returns <see cref="FunctionResultType.False"/>.</summary>
    public string? OnFalseFunctionName { get; private set; }

    /// <summary>Name of the function to run when this node returns <see cref="FunctionResultType.True"/>.</summary>
    public string? OnTrueFunctionName { get; private set; }

    /// <summary>
    /// Value predicates mapped to the function name to run when this node returns
    /// <see cref="FunctionResultType.Indeterminate"/> and the predicate matches the produced value.
    /// </summary>
    public Dictionary<Func<object?, bool>, string> OnValueFunctionNames { get; } = [];

    /// <summary>Builds the executable sequence rooted at the builder's initial function.</summary>
    public ISequenceFunction<TSequenceContext, TSequenceData> Build() => builder.Build();

    #region After

    /// <summary>
    /// Adds a function to the sequence using any properly defined method.
    /// The Method creates a sequence branch that will only be invoked when specified as a reaction to a result of another sequence function.
    /// </summary>
    /// <param name="function">The function to add.</param>
    public SequenceBranchDefinition<TSequenceContext, TSequenceData> After(
        Func<TSequenceContext, TSequenceData, ValueTask<FunctionResult>> function
    ) => builder.Register(function);

    /// <summary>Adds a function to the sequence under a name derived from the method and <paramref name="functionName"/>.</summary>
    /// <param name="function">The function to add.</param>
    /// <param name="functionName">An optional suffix appended to the method name to disambiguate the node.</param>
    public SequenceBranchDefinition<TSequenceContext, TSequenceData> After(
        Func<TSequenceContext, TSequenceData, ValueTask<FunctionResult>> function,
        string? functionName
    )
    {
        ArgumentNullException.ThrowIfNull(function);

        return builder.Register(function, function.Method.Name + functionName);
    }

    /// <summary>
    /// Adds a function to the sequence by defining a sequence function class to be instantiated.
    /// The Method creates a sequence branch that will only be invoked when specified as a reaction to a result of another sequence function.
    /// </summary>
    /// <typeparam name="TSequenceFunction">The parameterless sequence function type to instantiate.</typeparam>
    /// <param name="functionName">An optional suffix appended to the function name to disambiguate the node.</param>
    public SequenceBranchDefinition<TSequenceContext, TSequenceData> After<TSequenceFunction>(
        string? functionName = null
    )
        where TSequenceFunction : ISequenceFunction<TSequenceContext, TSequenceData>, new() =>
        builder.Register<TSequenceFunction>(functionName);

    /// <summary>Adds the specified sequence function instance.</summary>
    /// <param name="sequence">The sequence function to add.</param>
    /// <param name="functionName">An optional suffix appended to the function name to disambiguate the node.</param>
    public SequenceBranchDefinition<TSequenceContext, TSequenceData> After(
        ISequenceFunction<TSequenceContext, TSequenceData> sequence,
        string? functionName = null
    )
    {
        return sequence == null
            ? throw new ArgumentNullException(nameof(sequence))
            : builder.Register(
                sequence.Invoke,
                sequence.GetFunctionName() + (functionName ?? string.Empty)
            );
    }

    #endregion

    #region On True

    /// <summary>Runs <paramref name="function"/> when this node returns <see cref="FunctionResultType.True"/>.</summary>
    /// <param name="function">The reaction function.</param>
    /// <param name="functionName">An optional suffix appended to the method name to disambiguate the node.</param>
    public SequenceBranchDefinition<TSequenceContext, TSequenceData> IfTrueRun(
        Func<TSequenceContext, TSequenceData, ValueTask<FunctionResult>> function,
        string? functionName = null
    )
    {
        ArgumentNullException.ThrowIfNull(function);

        OnTrueFunctionName = function.Method.Name + (functionName ?? string.Empty);
        builder.Register(function, OnTrueFunctionName);
        return this;
    }

    /// <summary>
    /// Adds a reaction function to the sequence branch by defining a sequence function class to be instantiated.
    /// The specified function will be called only if the parent function's result type is "True".
    /// </summary>
    /// <typeparam name="TSequenceFunction">The parameterless sequence function type to instantiate.</typeparam>
    /// <param name="functionName">An optional suffix appended to the function name to disambiguate the node.</param>
    public SequenceBranchDefinition<TSequenceContext, TSequenceData> IfTrueRun<TSequenceFunction>(
        string? functionName = null
    )
        where TSequenceFunction : ISequenceFunction<TSequenceContext, TSequenceData>, new()
    {
        var sequenceFunction = new TSequenceFunction();
        builder.Register(sequenceFunction, functionName);
        OnTrueFunctionName = sequenceFunction.GetFunctionName() + (functionName ?? string.Empty);
        return this;
    }

    /// <summary>Runs <paramref name="sequence"/> when this node returns <see cref="FunctionResultType.True"/>.</summary>
    /// <param name="sequence">The reaction sequence function.</param>
    /// <param name="functionName">An optional suffix appended to the function name to disambiguate the node.</param>
    public SequenceBranchDefinition<TSequenceContext, TSequenceData> IfTrueRun(
        ISequenceFunction<TSequenceContext, TSequenceData> sequence,
        string? functionName = null
    )
    {
        ArgumentNullException.ThrowIfNull(sequence);

        OnTrueFunctionName = sequence.GetFunctionName() + (functionName ?? string.Empty);
        builder.Register(sequence.Invoke, OnTrueFunctionName);
        return this;
    }

    #endregion On True

    #region On False

    /// <summary>Runs <paramref name="function"/> when this node returns <see cref="FunctionResultType.False"/>.</summary>
    /// <param name="function">The reaction function.</param>
    /// <param name="functionName">An optional suffix appended to the method name to disambiguate the node.</param>
    public SequenceBranchDefinition<TSequenceContext, TSequenceData> IfFalseRun(
        Func<TSequenceContext, TSequenceData, ValueTask<FunctionResult>> function,
        string? functionName = null
    )
    {
        ArgumentNullException.ThrowIfNull(function);

        OnFalseFunctionName = function.Method.Name + (functionName ?? string.Empty);
        builder.Register(function, OnFalseFunctionName);
        return this;
    }

    /// <summary>
    /// Adds a reaction function to the sequence branch by defining a sequence function class to be instantiated.
    /// The specified function will be called only if the parent function's result type is "False".
    /// </summary>
    /// <typeparam name="TSequenceFunction">The parameterless sequence function type to instantiate.</typeparam>
    /// <param name="functionName">An optional suffix appended to the function name to disambiguate the node.</param>
    public SequenceBranchDefinition<TSequenceContext, TSequenceData> IfFalseRun<TSequenceFunction>(
        string? functionName = null
    )
        where TSequenceFunction : ISequenceFunction<TSequenceContext, TSequenceData>, new()
    {
        var sequenceFunction = new TSequenceFunction();
        builder.Register(sequenceFunction, functionName);
        OnFalseFunctionName = sequenceFunction.GetFunctionName() + (functionName ?? string.Empty);
        return this;
    }

    /// <summary>Runs <paramref name="sequence"/> when this node returns <see cref="FunctionResultType.False"/>.</summary>
    /// <param name="sequence">The reaction sequence function.</param>
    /// <param name="functionName">An optional suffix appended to the function name to disambiguate the node.</param>
    public SequenceBranchDefinition<TSequenceContext, TSequenceData> IfFalseRun(
        ISequenceFunction<TSequenceContext, TSequenceData> sequence,
        string? functionName = null
    )
    {
        ArgumentNullException.ThrowIfNull(sequence);

        OnFalseFunctionName = sequence.GetFunctionName() + (functionName ?? string.Empty);
        builder.Register(sequence.Invoke, OnFalseFunctionName);
        return this;
    }

    #endregion On False

    #region On Abort

    /// <summary>Runs <paramref name="function"/> when this node returns <see cref="FunctionResultType.Abort"/>.</summary>
    /// <param name="function">The reaction function.</param>
    /// <param name="functionName">An optional suffix appended to the method name to disambiguate the node.</param>
    public SequenceBranchDefinition<TSequenceContext, TSequenceData> IfAbortRun(
        Func<TSequenceContext, TSequenceData, ValueTask<FunctionResult>> function,
        string? functionName = null
    )
    {
        ArgumentNullException.ThrowIfNull(function);

        OnAbortFunctionName = function.Method.Name + (functionName ?? string.Empty);
        builder.Register(function, OnAbortFunctionName);
        return this;
    }

    /// <summary>
    /// Adds a reaction function to the sequence branch by defining a sequence function class to be instantiated.
    /// The specified function will be called only if the parent function's result type is "Abort".
    /// </summary>
    /// <typeparam name="TSequenceFunction">The parameterless sequence function type to instantiate.</typeparam>
    /// <param name="functionName">An optional suffix appended to the function name to disambiguate the node.</param>
    public SequenceBranchDefinition<TSequenceContext, TSequenceData> IfAbortRun<TSequenceFunction>(
        string? functionName = null
    )
        where TSequenceFunction : ISequenceFunction<TSequenceContext, TSequenceData>, new()
    {
        var sequenceFunction = new TSequenceFunction();
        builder.Register(sequenceFunction, functionName);
        OnAbortFunctionName = sequenceFunction.GetFunctionName() + (functionName ?? string.Empty);
        return this;
    }

    /// <summary>Runs <paramref name="sequence"/> when this node returns <see cref="FunctionResultType.Abort"/>.</summary>
    /// <param name="sequence">The reaction sequence function.</param>
    /// <param name="functionName">An optional suffix appended to the function name to disambiguate the node.</param>
    public SequenceBranchDefinition<TSequenceContext, TSequenceData> IfAbortRun(
        ISequenceFunction<TSequenceContext, TSequenceData> sequence,
        string? functionName = null
    )
    {
        ArgumentNullException.ThrowIfNull(sequence);

        OnAbortFunctionName = sequence.GetFunctionName() + (functionName ?? string.Empty);
        builder.Register(sequence.Invoke, OnAbortFunctionName);
        return this;
    }

    #endregion On Abort

    #region On Value

    /// <summary>
    /// Adds a reaction function to the sequence branch using any properly defined method.
    /// The specified function will be called only if the parent function returns
    /// <see cref="FunctionResultType.Indeterminate"/> and <paramref name="predicate"/> matches the produced value.
    /// </summary>
    /// <param name="predicate">Tests the produced value (which may be <c>null</c>) to decide whether this reaction runs.</param>
    /// <param name="function">The reaction function.</param>
    /// <param name="functionName">An optional name override; defaults to the method name.</param>
    public SequenceBranchDefinition<TSequenceContext, TSequenceData> IfValueRun(
        Func<object?, bool> predicate,
        Func<TSequenceContext, TSequenceData, ValueTask<FunctionResult>> function,
        string? functionName = null
    )
    {
        ArgumentNullException.ThrowIfNull(function);

        OnValueFunctionNames[predicate] = functionName ?? function.Method.Name;
        builder.Register(function, OnValueFunctionNames[predicate]);
        return this;
    }

    /// <summary>
    /// Adds a reaction function to the sequence branch by defining a sequence function class to be instantiated.
    /// The specified function will be called only if the parent function returns
    /// <see cref="FunctionResultType.Indeterminate"/> and <paramref name="predicate"/> matches the produced value.
    /// </summary>
    /// <typeparam name="TSequenceFunction">The parameterless sequence function type to instantiate.</typeparam>
    /// <param name="predicate">Tests the produced value (which may be <c>null</c>) to decide whether this reaction runs.</param>
    /// <param name="functionName">An optional suffix appended to the function name to disambiguate the node.</param>
    public SequenceBranchDefinition<TSequenceContext, TSequenceData> IfValueRun<TSequenceFunction>(
        Func<object?, bool> predicate,
        string? functionName = null
    )
        where TSequenceFunction : ISequenceFunction<TSequenceContext, TSequenceData>, new()
    {
        var sequenceFunction = new TSequenceFunction();
        builder.Register(sequenceFunction, functionName);
        OnValueFunctionNames[predicate] =
            sequenceFunction.GetFunctionName() + (functionName ?? string.Empty);
        return this;
    }

    /// <summary>
    /// Adds a class-based reaction that runs for any value when the parent returns
    /// <see cref="FunctionResultType.Indeterminate"/>.
    /// </summary>
    /// <typeparam name="TSequenceFunction">The parameterless sequence function type to instantiate.</typeparam>
    /// <param name="functionName">An optional suffix appended to the function name to disambiguate the node.</param>
    public SequenceBranchDefinition<TSequenceContext, TSequenceData> IfValueRun<TSequenceFunction>(
        string? functionName = null
    )
        where TSequenceFunction : ISequenceFunction<TSequenceContext, TSequenceData>, new() =>
        IfValueRun<TSequenceFunction>(_ => true, functionName);

    /// <summary>
    /// Adds a catch-all value reaction that runs for any value when the parent returns
    /// <see cref="FunctionResultType.Indeterminate"/>.
    /// </summary>
    /// <param name="function">The reaction function.</param>
    /// <param name="functionName">An optional name override; defaults to the method name.</param>
    public SequenceBranchDefinition<TSequenceContext, TSequenceData> IfValueElseRun(
        Func<TSequenceContext, TSequenceData, ValueTask<FunctionResult>> function,
        string? functionName = null
    ) => IfValueRun(_ => true, function, functionName);

    #endregion On Value

    #region On Any

    /// <summary>Runs <paramref name="function"/> on any result type when no specific branch matched.</summary>
    /// <param name="function">The reaction function.</param>
    public SequenceBranchDefinition<TSequenceContext, TSequenceData> IfAnyRun(
        Func<TSequenceContext, TSequenceData, ValueTask<FunctionResult>> function
    )
    {
        builder.Register(function);
        OnAnyFunctionName = function.Method.Name;
        return this;
    }

    /// <summary>Runs <paramref name="function"/> on any result type, under a disambiguated name.</summary>
    /// <param name="function">The reaction function.</param>
    /// <param name="functionName">A suffix appended to the method name to disambiguate the node.</param>
    public SequenceBranchDefinition<TSequenceContext, TSequenceData> IfAnyRun(
        Func<TSequenceContext, TSequenceData, ValueTask<FunctionResult>> function,
        string functionName
    )
    {
        OnAnyFunctionName = function.Method.Name + (functionName ?? string.Empty);
        builder.Register(function, OnAnyFunctionName);
        return this;
    }

    /// <summary>
    /// Adds a reaction function to the sequence branch by defining a sequence function class to be instantiated.
    /// The specified function will be called on any parent function result type.
    /// </summary>
    /// <typeparam name="TSequenceFunction">The parameterless sequence function type to instantiate.</typeparam>
    /// <param name="functionName">An optional suffix appended to the function name to disambiguate the node.</param>
    public SequenceBranchDefinition<TSequenceContext, TSequenceData> IfAnyRun<TSequenceFunction>(
        string? functionName = null
    )
        where TSequenceFunction : ISequenceFunction<TSequenceContext, TSequenceData>, new()
    {
        var sequenceFunction = new TSequenceFunction();
        builder.Register(sequenceFunction, functionName);
        OnAnyFunctionName = sequenceFunction.GetFunctionName() + (functionName ?? string.Empty);
        return this;
    }

    /// <summary>Runs <paramref name="sequence"/> on any result type when no specific branch matched.</summary>
    /// <param name="sequence">The reaction sequence function.</param>
    /// <param name="functionName">An optional suffix appended to the function name to disambiguate the node.</param>
    public SequenceBranchDefinition<TSequenceContext, TSequenceData> IfAnyRun(
        ISequenceFunction<TSequenceContext, TSequenceData> sequence,
        string? functionName = null
    )
    {
        ArgumentNullException.ThrowIfNull(sequence);

        OnAnyFunctionName = sequence.GetFunctionName() + (functionName ?? string.Empty);
        builder.Register(sequence.Invoke, OnAnyFunctionName);
        return this;
    }

    #endregion On Any
}
