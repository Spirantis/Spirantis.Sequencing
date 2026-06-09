namespace Spirantis.Sequencing.Test;

/// <summary>Tests for how a result type selects the next branch, including the OnAny fallback.</summary>
public class BranchRoutingTests
{
    [Fact]
    public async Task OnTrue_RunsWhenResultIsTrue()
    {
        var sequence = SequenceBuilder
            .Create<DefaultSequenceContext, TestSequenceData>()
            .Run<ReturnsTrue>()
            .IfTrueRun<ReturnsFalse>()
            .Build();

        var data = Sequence.Data();
        var result = await sequence.Invoke(Sequence.Context(), data);

        Assert.Equal(FunctionResultType.False, result.Type);
        Assert.Equal(new[] { nameof(ReturnsTrue), nameof(ReturnsFalse) }, data.ExecutionLog);
    }

    [Fact]
    public async Task OnFalse_RunsWhenResultIsFalse()
    {
        var sequence = SequenceBuilder
            .Create<DefaultSequenceContext, TestSequenceData>()
            .Run<ReturnsFalse>()
            .IfFalseRun<StepA>()
            .Build();

        var data = Sequence.Data();
        var result = await sequence.Invoke(Sequence.Context(), data);

        Assert.Equal(FunctionResultType.True, result.Type);
        Assert.Equal(new[] { nameof(ReturnsFalse), nameof(StepA) }, data.ExecutionLog);
    }

    [Fact]
    public async Task OnAbort_RunsWhenResultIsAbort()
    {
        var sequence = SequenceBuilder
            .Create<DefaultSequenceContext, TestSequenceData>()
            .Run<ReturnsAbort>()
            .IfAbortRun<StepA>()
            .Build();

        var data = Sequence.Data();
        var result = await sequence.Invoke(Sequence.Context(), data);

        Assert.Equal(FunctionResultType.True, result.Type);
        Assert.Equal(new[] { nameof(ReturnsAbort), nameof(StepA) }, data.ExecutionLog);
    }

    [Fact]
    public async Task Abort_WithoutHandler_StopsAndReturnsAbort()
    {
        var sequence = SequenceBuilder
            .Create<DefaultSequenceContext, TestSequenceData>()
            .Run<ReturnsAbort>()
            .IfTrueRun<StepA>()
            .Build();

        var data = Sequence.Data();
        var result = await sequence.Invoke(Sequence.Context(), data);

        Assert.Equal(FunctionResultType.Abort, result.Type);
        Assert.Equal(new[] { nameof(ReturnsAbort) }, data.ExecutionLog);
    }

    [Fact]
    public async Task OnAny_RunsWhenNoSpecificBranchMatches()
    {
        var sequence = SequenceBuilder
            .Create<DefaultSequenceContext, TestSequenceData>()
            .Run<ReturnsTrue>()
            .IfFalseRun<StepA>() // does not match a True result
            .IfAnyRun<StepB>() // fallback
            .Build();

        var data = Sequence.Data();
        var result = await sequence.Invoke(Sequence.Context(), data);

        Assert.Equal(FunctionResultType.True, result.Type);
        Assert.Equal(new[] { nameof(ReturnsTrue), nameof(StepB) }, data.ExecutionLog);
    }

    [Fact]
    public async Task OnAny_DoesNotOverrideAMatchingSpecificBranch()
    {
        var sequence = SequenceBuilder
            .Create<DefaultSequenceContext, TestSequenceData>()
            .Run<ReturnsTrue>()
            .IfTrueRun<StepA>() // matches first
            .IfAnyRun<StepB>() // must be ignored
            .Build();

        var data = Sequence.Data();
        var result = await sequence.Invoke(Sequence.Context(), data);

        Assert.Equal(FunctionResultType.True, result.Type);
        Assert.Equal(new[] { nameof(ReturnsTrue), nameof(StepA) }, data.ExecutionLog);
        Assert.DoesNotContain(nameof(StepB), data.ExecutionLog);
    }
}
