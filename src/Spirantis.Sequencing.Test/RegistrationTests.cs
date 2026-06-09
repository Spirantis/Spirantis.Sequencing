namespace Spirantis.Sequencing.Test;

/// <summary>Tests for the different ways functions are registered and named in the builder.</summary>
public class RegistrationTests
{
    [Fact]
    public async Task DelegateRegistration_Works()
    {
        var sequence = SequenceBuilder
            .Create<DefaultSequenceContext, TestSequenceData>()
            .StartWith(Delegates.TrueDelegate)
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
            .StartWith(entry)
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
            .StartWith<StepA>("First")
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
            .StartWith<ReturnsTrue>()
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
}
