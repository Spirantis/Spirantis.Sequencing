using Microsoft.Extensions.Logging;

namespace Spirantis.Sequencing;

/// <summary>
/// Ambient context shared by every function in a sequence. Implement this to surface
/// cross-cutting services (logging, and any application-specific state) to sequence functions.
/// </summary>
public interface ISequenceContext
{
    /// <summary>The logger available to sequence functions.</summary>
    ILogger Logger { get; }
}
