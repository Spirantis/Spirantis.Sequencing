using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Spirantis.Sequencing;

/// <summary>
/// Default <see cref="ISequenceContext"/>: exposes a logger, a correlation key (a fresh GUID unless
/// set), and a stopwatch started when the context is created.
/// </summary>
/// <param name="logger">The logger made available to functions executing within the sequence.</param>
public class DefaultSequenceContext(ILogger logger) : ISequenceContext
{
    /// <inheritdoc />
    public ILogger Logger { get; } = logger;

    /// <inheritdoc />
    public string CorrelationKey { get; init; } = Guid.NewGuid().ToString();

    /// <inheritdoc />
    public Stopwatch Stopwatch { get; } = Stopwatch.StartNew();
}
