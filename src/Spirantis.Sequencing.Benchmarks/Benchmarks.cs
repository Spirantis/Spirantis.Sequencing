using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Spirantis.Sequencing.Abstraction;

namespace Spirantis.Sequencing.Benchmarks;

/// <summary>Per-node execution cost, comparing True-routed vs False-routed chains.</summary>
[MemoryDiagnoser]
public class ExecutionBenchmarks
{
    [Params(10, 100, 1000)]
    public int Depth;

    private ISequenceFunction<DefaultSequenceContext, BenchData> linear = null!;
    private ISequenceFunction<DefaultSequenceContext, BenchData> branching = null!;
    private DefaultSequenceContext context = null!;
    private BenchData data = null!;

    [GlobalSetup]
    public void Setup()
    {
        context = new DefaultSequenceContext(NullLogger.Instance);
        data = new BenchData();
        linear = BenchSequences.LinearChain(Depth);
        branching = BenchSequences.FalseChain(Depth);
    }

    [Benchmark(Baseline = true)]
    public ValueTask<FunctionResult> InvokeLinearChain() => linear.Invoke(context, data);

    [Benchmark]
    public ValueTask<FunctionResult> InvokeFalseChain() => branching.Invoke(context, data);
}

/// <summary>Cost of dispatching an Indeterminate result across N value predicates.</summary>
[MemoryDiagnoser]
public class ValueRoutingBenchmarks
{
    [Params(2, 8, 32)]
    public int PredicateCount;

    private ISequenceFunction<DefaultSequenceContext, BenchData> sequence = null!;
    private DefaultSequenceContext context = null!;
    private BenchData data = null!;

    [GlobalSetup]
    public void Setup()
    {
        context = new DefaultSequenceContext(NullLogger.Instance);
        data = new BenchData();
        sequence = BenchSequences.ValueDispatch(PredicateCount);
    }

    [Benchmark]
    public ValueTask<FunctionResult> InvokeValueDispatch() => sequence.Invoke(context, data);
}

/// <summary>Cost of running a sequence whose nodes are themselves nested sequences.</summary>
[MemoryDiagnoser]
public class NestedSequenceBenchmarks
{
    [Params(10, 50)]
    public int OuterCount;

    private ISequenceFunction<DefaultSequenceContext, BenchData> sequence = null!;
    private DefaultSequenceContext context = null!;
    private BenchData data = null!;

    [GlobalSetup]
    public void Setup()
    {
        context = new DefaultSequenceContext(NullLogger.Instance);
        data = new BenchData();
        sequence = BenchSequences.NestedChain(OuterCount);
    }

    [Benchmark]
    public ValueTask<FunctionResult> InvokeNestedChain() => sequence.Invoke(context, data);
}

/// <summary>Cost of building the same linear sequence via each registration style.</summary>
[MemoryDiagnoser]
public class BuildByStyleBenchmarks
{
    [Params(10, 100)]
    public int Depth;

    [Benchmark(Baseline = true)]
    public ISequenceFunction<DefaultSequenceContext, BenchData> Class() =>
        BenchSequences.LinearViaClass(Depth);

    [Benchmark]
    public ISequenceFunction<DefaultSequenceContext, BenchData> Instance() =>
        BenchSequences.LinearChain(Depth);

    [Benchmark]
    public ISequenceFunction<DefaultSequenceContext, BenchData> MethodGroup() =>
        BenchSequences.LinearViaMethodGroup(Depth);

    [Benchmark]
    public ISequenceFunction<DefaultSequenceContext, BenchData> Lambda() =>
        BenchSequences.LinearViaLambda(Depth);
}

/// <summary>
/// Build cost when continuations are shared, so a naive build re-expands the downstream subtree
/// twice per level (exponential). Watch this for any build-memoization change.
/// </summary>
[MemoryDiagnoser]
public class DiamondBuildBenchmarks
{
    [Params(8, 12, 16)]
    public int Depth;

    [Benchmark]
    public ISequenceFunction<DefaultSequenceContext, BenchData> BuildDiamondChain() =>
        BenchSequences.DiamondChain(Depth);
}
