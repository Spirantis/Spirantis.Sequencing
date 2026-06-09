using System.Diagnostics;

namespace Spirantis.Sequencing;

/// <summary>
/// Per-invocation payload that flows through a sequence. Implement this to carry the data
/// a sequence operates on, along with correlation and timing information.
/// </summary>
public interface ISequenceData
{
    /// <summary>A key used to correlate all functions executed within a single sequence run.</summary>
    string CorrelationKey { get; set; }

    /// <summary>Tracks elapsed time across the sequence run.</summary>
    Stopwatch Stopwatch { get; set; }
}
