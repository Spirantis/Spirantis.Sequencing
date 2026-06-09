namespace Spirantis.Sequencing.Abstraction;

/// <summary>
/// A unit of work in a sequence. Implementations produce a <see cref="FunctionResult"/> whose
/// <see cref="FunctionResult.Type"/> determines which continuation branch the sequence runs next.
/// </summary>
/// <typeparam name="TSequenceContext">The ambient context type shared across the sequence.</typeparam>
/// <typeparam name="TSequenceData">The per-invocation data type flowing through the sequence.</typeparam>
public interface ISequenceFunction<in TSequenceContext, in TSequenceData>
    where TSequenceContext : ISequenceContext
    where TSequenceData : ISequenceData
{
    /// <summary>
    /// The name used to identify this function within a sequence; defaults to the runtime type name.
    /// </summary>
    string GetFunctionName() => GetType().Name;

    /// <summary>Executes the function.</summary>
    /// <param name="sequenceContext">The ambient context.</param>
    /// <param name="sequenceData">The data being processed.</param>
    /// <param name="cancellationToken">
    /// Signals cancellation. The engine also checks it between steps; honor it in any async work.
    /// </param>
    /// <returns>The result that drives branch selection.</returns>
    ValueTask<FunctionResult> Invoke(
        TSequenceContext sequenceContext,
        TSequenceData sequenceData,
        CancellationToken cancellationToken = default
    );
}
