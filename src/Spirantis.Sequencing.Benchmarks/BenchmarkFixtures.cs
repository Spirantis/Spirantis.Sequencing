using Spirantis.Sequencing.Abstraction;

namespace Spirantis.Sequencing.Benchmarks;

/// <summary>Minimal sequence data: a shared counter the steps bump so the work isn't elided.</summary>
public sealed class BenchData
{
    public int Counter { get; set; }
}

internal static class BenchMarkers
{
    /// <summary>The value carried by Indeterminate results in the value-routing benchmarks.</summary>
    public static readonly object Value = new();
}

/// <summary>A configurable step: bumps the shared counter and returns the requested result type.</summary>
internal sealed class BenchStep(string name, FunctionResultType type = FunctionResultType.True)
    : ISequenceFunction<DefaultSequenceContext, BenchData>
{
    public string GetFunctionName() => name;

    public ValueTask<FunctionResult> Invoke(
        DefaultSequenceContext context,
        BenchData data,
        CancellationToken cancellationToken
    )
    {
        data.Counter++;
        return type switch
        {
            FunctionResultType.False => FunctionResult.False(),
            FunctionResultType.Abort => FunctionResult.Abort(),
            FunctionResultType.Indeterminate => FunctionResult.Indeterminate(BenchMarkers.Value),
            _ => FunctionResult.True(),
        };
    }
}

/// <summary>Parameterless step (returns True), registered via the generic <c>&lt;T&gt;</c> overloads.</summary>
internal sealed class BenchStepClass : ISequenceFunction<DefaultSequenceContext, BenchData>
{
    public ValueTask<FunctionResult> Invoke(
        DefaultSequenceContext context,
        BenchData data,
        CancellationToken cancellationToken
    )
    {
        data.Counter++;
        return FunctionResult.True();
    }
}

/// <summary>Method-group style step.</summary>
internal static class BenchFunctions
{
    public static ValueTask<FunctionResult> Step(
        DefaultSequenceContext context,
        BenchData data,
        CancellationToken cancellationToken
    )
    {
        data.Counter++;
        return FunctionResult.True();
    }
}

/// <summary>Builders for the sequence shapes the benchmarks run against.</summary>
internal static class BenchSequences
{
    // --- Execution shapes ---------------------------------------------------------------------

    /// <summary>OnTrue chain via instances: n0 → n1 → … → n(depth-1).</summary>
    public static ISequenceFunction<DefaultSequenceContext, BenchData> LinearChain(int depth)
    {
        var nodes = TrueNodes(depth, "n");

        var def = SequenceBuilder.Create<DefaultSequenceContext, BenchData>().Run(nodes[0]);

        for (var i = 1; i < depth; i++)
        {
            def = def.IfTrueRun(nodes[i]).After(nodes[i]);
        }

        return def.Build();
    }

    /// <summary>OnFalse chain: confirms False-routing costs the same per node as the True path.</summary>
    public static ISequenceFunction<DefaultSequenceContext, BenchData> FalseChain(int depth)
    {
        var nodes = new BenchStep[depth];
        for (var i = 0; i < depth; i++)
        {
            nodes[i] = new BenchStep($"f{i}", FunctionResultType.False);
        }

        var def = SequenceBuilder.Create<DefaultSequenceContext, BenchData>().Run(nodes[0]);

        for (var i = 1; i < depth; i++)
        {
            def = def.IfFalseRun(nodes[i]).After(nodes[i]);
        }

        return def.Build();
    }

    /// <summary>
    /// A single Indeterminate node with <paramref name="predicateCount"/> value predicates, all of
    /// which fail — so every predicate is evaluated. Measures the value-dispatch path.
    /// </summary>
    public static ISequenceFunction<DefaultSequenceContext, BenchData> ValueDispatch(
        int predicateCount
    )
    {
        var def = SequenceBuilder
            .Create<DefaultSequenceContext, BenchData>()
            .Run(new BenchStep("start", FunctionResultType.Indeterminate));

        for (var i = 0; i < predicateCount; i++)
        {
            var local = i; // fresh closure per iteration → distinct predicate delegate (dict key)
            def.IfValueRun(_ => local < 0, BenchFunctions.Step, $"miss{i}");
        }

        return def.Build();
    }

    /// <summary>An outer OnTrue chain whose nodes are each a small nested sequence.</summary>
    public static ISequenceFunction<DefaultSequenceContext, BenchData> NestedChain(int outerCount)
    {
        const int innerDepth = 3;
        var inners = new ISequenceFunction<DefaultSequenceContext, BenchData>[outerCount];
        for (var i = 0; i < outerCount; i++)
        {
            inners[i] = LinearChain(innerDepth);
        }

        var def = SequenceBuilder.Create<DefaultSequenceContext, BenchData>().Run(inners[0]);

        for (var i = 1; i < outerCount; i++)
        {
            def = def.IfTrueRun(inners[i]).After(inners[i]);
        }

        return def.Build();
    }

    // --- Registration styles (build cost) -----------------------------------------------------

    /// <summary>Linear chain built via the generic class overloads (instantiates each node type).</summary>
    public static ISequenceFunction<DefaultSequenceContext, BenchData> LinearViaClass(int depth)
    {
        var def = SequenceBuilder.Create<DefaultSequenceContext, BenchData>().Run<BenchStepClass>();

        for (var i = 1; i < depth; i++)
        {
            def = def.IfTrueRun<BenchStepClass>($"n{i}").After<BenchStepClass>($"n{i}");
        }

        return def.Build();
    }

    /// <summary>Linear chain built via a method group.</summary>
    public static ISequenceFunction<DefaultSequenceContext, BenchData> LinearViaMethodGroup(
        int depth
    )
    {
        var def = SequenceBuilder
            .Create<DefaultSequenceContext, BenchData>()
            .Run(BenchFunctions.Step, "n0");

        for (var i = 1; i < depth; i++)
        {
            def = def.IfTrueRun(BenchFunctions.Step, $"n{i}").After(BenchFunctions.Step, $"n{i}");
        }

        return def.Build();
    }

    /// <summary>Linear chain built via a single (non-capturing) lambda.</summary>
    public static ISequenceFunction<DefaultSequenceContext, BenchData> LinearViaLambda(int depth)
    {
        Func<DefaultSequenceContext, BenchData, CancellationToken, ValueTask<FunctionResult>> step =
            (context, data, cancellationToken) =>
            {
                data.Counter++;
                return FunctionResult.True();
            };

        var def = SequenceBuilder.Create<DefaultSequenceContext, BenchData>().Run(step, "n0");

        for (var i = 1; i < depth; i++)
        {
            def = def.IfTrueRun(step, $"n{i}").After(step, $"n{i}");
        }

        return def.Build();
    }

    /// <summary>
    /// Each node routes both True and False to the same next node, so a naive build re-expands the
    /// downstream subtree twice per level (exponential). Exercises build cost / motivates memoization.
    /// </summary>
    public static ISequenceFunction<DefaultSequenceContext, BenchData> DiamondChain(int depth)
    {
        var nodes = TrueNodes(depth, "d");

        var def = SequenceBuilder.Create<DefaultSequenceContext, BenchData>().Run(nodes[0]);

        for (var i = 1; i < depth; i++)
        {
            def.IfTrueRun(nodes[i]);
            def.IfFalseRun(nodes[i]);
            def = def.After(nodes[i]);
        }

        return def.Build();
    }

    private static BenchStep[] TrueNodes(int depth, string prefix)
    {
        var nodes = new BenchStep[depth];
        for (var i = 0; i < depth; i++)
        {
            nodes[i] = new BenchStep($"{prefix}{i}");
        }

        return nodes;
    }
}
