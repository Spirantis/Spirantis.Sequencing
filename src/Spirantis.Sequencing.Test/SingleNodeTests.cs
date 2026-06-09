namespace Spirantis.Sequencing.Test;

/// <summary>Tests for a single function and how a sequence terminates.</summary>
public class SingleNodeTests
{
    [Fact]
    public async Task SingleFunction_ReturnsItsOwnResult()
    {
        var sequence = SequenceBuilder
            .Create<DefaultSequenceContext, TestSequenceData>()
            .Run<ReturnsTrue>()
            .Build();

        var data = Sequence.Data();
        var result = await sequence.Invoke(Sequence.Context(), data);

        Assert.Equal(FunctionResultType.True, result.Type);
        Assert.Equal(new[] { nameof(ReturnsTrue) }, data.ExecutionLog);
    }

    [Fact]
    public async Task SingleFunction_PropagatesValue()
    {
        var sequence = SequenceBuilder
            .Create<DefaultSequenceContext, TestSequenceData>()
            .Run<ReturnsTrueWithValue>()
            .Build();

        var result = await sequence.Invoke(Sequence.Context(), Sequence.Data());

        Assert.Equal(FunctionResultType.True, result.Type);
        Assert.True(result.IsSuccess);
        var payload = Assert.IsType<TestPayload>(result.Value);
        Assert.Equal("ok", payload.Kind);
    }

    [Fact]
    public async Task UnhandledResultType_ReturnsTheResultUnchanged()
    {
        // The start function returns True, but only a False reaction is wired: nothing matches,
        // there is no OnAny, so the sequence ends and returns the start function's own result.
        var sequence = SequenceBuilder
            .Create<DefaultSequenceContext, TestSequenceData>()
            .Run<ReturnsTrue>()
            .IfFalseRun<StepA>()
            .Build();

        var data = Sequence.Data();
        var result = await sequence.Invoke(Sequence.Context(), data);

        Assert.Equal(FunctionResultType.True, result.Type);
        Assert.Equal(new[] { nameof(ReturnsTrue) }, data.ExecutionLog);
    }
}
