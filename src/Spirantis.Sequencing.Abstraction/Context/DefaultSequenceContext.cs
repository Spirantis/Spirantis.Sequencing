using Microsoft.Extensions.Logging;

namespace Spirantis.Sequencing;

/// <summary>
/// Default <see cref="ISequenceContext"/> implementation that exposes a logger to sequence functions.
/// </summary>
/// <param name="logger">The logger made available to functions executing within the sequence.</param>
public class DefaultSequenceContext(ILogger logger) : ISequenceContext
{
    /// <inheritdoc />
    public ILogger Logger { get; } = logger;
}
