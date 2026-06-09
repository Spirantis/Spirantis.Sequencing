using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Spirantis.Sequencing;

/// <summary>
/// Ambient, run-scoped environment shared by every function in a sequence. Implement this — or a
/// richer interface deriving from it — to surface cross-cutting services (logging, correlation,
/// timing, and any application-specific services such as an HTTP client) to sequence functions.
/// </summary>
public interface ISequenceContext
{
    /// <summary>The logger available to sequence functions.</summary>
    ILogger Logger { get; }

    /// <summary>A key correlating all functions executed within a single sequence run.</summary>
    string CorrelationKey { get; }

    /// <summary>Tracks elapsed time across the sequence run.</summary>
    Stopwatch Stopwatch { get; }
}
