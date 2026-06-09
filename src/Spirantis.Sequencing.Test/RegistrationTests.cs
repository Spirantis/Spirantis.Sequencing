namespace Spirantis.Sequencing.Test;

/// <summary>Tests for the different ways functions are registered and named in the builder.</summary>
public class RegistrationTests
{
    [Fact]
    public async Task DelegateRegistration_Works()
    {
        var sequence = SequenceBuilder
            .Create<DefaultSequenceContext, TestSequenceData>()
            .Run(Delegates.TrueDelegate)
            .IfTrueRun(Delegates.FalseDelegate)
            .Build();

        var data = Sequence.Data();
        var result = await sequence.Invoke(Sequence.Context(), data);

        Assert.Equal(FunctionResultType.False, result.Type);
        Assert.Equal(
            new[] { nameof(Delegates.TrueDelegate), nameof(Delegates.FalseDelegate) },
            data.ExecutionLog
        );
    }

    [Fact]
    public async Task InstanceRegistration_UsesGetFunctionNameAndRuns()
    {
        var entry = new NamedStep("entry", FunctionResult.True());
        var next = new NamedStep("next", FunctionResult.False());

        var sequence = SequenceBuilder
            .Create<DefaultSequenceContext, TestSequenceData>()
            .Run(entry)
            .IfTrueRun(next)
            .Build();

        var data = Sequence.Data();
        var result = await sequence.Invoke(Sequence.Context(), data);

        Assert.Equal(FunctionResultType.False, result.Type);
        Assert.Equal(new[] { "entry", "next" }, data.ExecutionLog);
    }

    [Fact]
    public async Task NameSuffix_DisambiguatesTwoNodesOfTheSameType()
    {
        // Without a suffix both registrations would collapse to one node (and self-reference);
        // the suffix produces two distinct nodes, so the type runs twice.
        var sequence = SequenceBuilder
            .Create<DefaultSequenceContext, TestSequenceData>()
            .Run<StepA>("First")
            .IfTrueRun<StepA>("Second")
            .Build();

        var data = Sequence.Data();
        var result = await sequence.Invoke(Sequence.Context(), data);

        Assert.Equal(FunctionResultType.True, result.Type);
        Assert.Equal(new[] { nameof(StepA), nameof(StepA) }, data.ExecutionLog);
    }

    [Fact]
    public async Task IfAnyRun_WithName_WiresTheContinuation()
    {
        // Guards the fix where the named IfAnyRun overload registered the function under a different
        // key than it later looked up, silently dropping the continuation.
        var sequence = SequenceBuilder
            .Create<DefaultSequenceContext, TestSequenceData>()
            .Run<ReturnsTrue>()
            .IfAnyRun(Delegates.FalseDelegate, "Suffix")
            .Build();

        var data = Sequence.Data();
        var result = await sequence.Invoke(Sequence.Context(), data);

        Assert.Equal(FunctionResultType.False, result.Type);
        Assert.Equal(
            new[] { nameof(ReturnsTrue), nameof(Delegates.FalseDelegate) },
            data.ExecutionLog
        );
    }

    [Fact]
    public async Task AnonymousMethods_WorkAsStartAndReaction()
    {
        // A sequence function can also be an inline lambda. The engine derives the node name from the
        // delegate, so an explicit name (or suffix) keeps anonymous nodes addressable.
        var sequence = SequenceBuilder
            .Create<DefaultSequenceContext, TestSequenceData>()
            .Run(
                (context, data) =>
                {
                    data.ExecutionLog.Add("start");
                    return FunctionResult.True();
                },
                "start"
            )
            .IfTrueRun(
                (context, data) =>
                {
                    data.ExecutionLog.Add("reaction");
                    return FunctionResult.False();
                },
                "reaction"
            )
            .Build();

        var data = Sequence.Data();
        var result = await sequence.Invoke(Sequence.Context(), data);

        Assert.Equal(FunctionResultType.False, result.Type);
        Assert.Equal(new[] { "start", "reaction" }, data.ExecutionLog);
    }

    [Fact]
    public async Task SameFunctionReusedUnderDifferentNames_HasIndependentReactions()
    {
        // StepA is registered as two distinct nodes (StepAYes / StepANo) via name suffixes, and each
        // named node carries its own reaction. The data flag selects which one runs.
        var sequence = SequenceBuilder
            .Create<DefaultSequenceContext, TestSequenceData>()
            .Run<Gate>()
            .IfTrueRun<StepA>("Yes") // node "StepAYes"
            .IfFalseRun<StepA>("No") // node "StepANo"
            .After<StepA>("Yes")
            .IfTrueRun<StepB>() // StepAYes.OnTrue = StepB
            .After<StepA>("No")
            .IfTrueRun<StepC>() // StepANo.OnTrue = StepC
            .Build();

        var yes = new TestSequenceData { TakeTrue = true };
        var yesResult = await sequence.Invoke(Sequence.Context(), yes);
        Assert.Equal(FunctionResultType.True, yesResult.Type);
        Assert.Equal(new[] { nameof(Gate), nameof(StepA), nameof(StepB) }, yes.ExecutionLog);

        var no = new TestSequenceData { TakeTrue = false };
        var noResult = await sequence.Invoke(Sequence.Context(), no);
        Assert.Equal(FunctionResultType.True, noResult.Type);
        Assert.Equal(new[] { nameof(Gate), nameof(StepA), nameof(StepC) }, no.ExecutionLog);
    }

    [Fact]
    public async Task ASequenceCanBeUsedAsAFunctionInAnotherSequence()
    {
        // Build() returns an ISequenceFunction, so a whole sequence can be embedded as a node. The
        // shared data flows into the inner run, and the outer routes on the inner's final result.
        var inner = SequenceBuilder
            .Create<DefaultSequenceContext, TestSequenceData>("inner")
            .Run<StepA>()
            .IfTrueRun<StepB>()
            .Build();

        var outer = SequenceBuilder
            .Create<DefaultSequenceContext, TestSequenceData>("outer")
            .Run<ReturnsTrue>()
            .IfTrueRun(inner) // the inner sequence is itself a sequence function
            .After(inner)
            .IfTrueRun<StepC>() // runs after the inner sequence returns True
            .Build();

        var data = Sequence.Data();
        var result = await outer.Invoke(Sequence.Context(), data);

        Assert.Equal(FunctionResultType.True, result.Type);
        Assert.Equal(
            new[] { nameof(ReturnsTrue), nameof(StepA), nameof(StepB), nameof(StepC) },
            data.ExecutionLog
        );
    }
}
