namespace Spirantis.Sequencing.Test;

/// <summary>Tests that the engine observes the cancellation token between steps.</summary>
public class CancellationTests
{
    [Fact]
    public async Task PreCancelledToken_ThrowsBeforeRunningAnything()
    {
        var sequence = SequenceBuilder
            .Create<DefaultSequenceContext, TestSequenceData>()
            .Run<StepA>()
            .IfTrueRun<StepB>()
            .Build();

        var data = Sequence.Data();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await sequence.Invoke(Sequence.Context(), data, cts.Token)
        );
        Assert.Empty(data.ExecutionLog);
    }

    [Fact]
    public async Task CancellationDuringASequence_StopsBeforeTheNextStep()
    {
        using var cts = new CancellationTokenSource();
        var sequence = SequenceBuilder
            .Create<DefaultSequenceContext, TestSequenceData>()
            .Run(new CancelStep(cts)) // runs, then cancels the token
            .IfTrueRun<StepB>() // must not run
            .Build();

        var data = Sequence.Data();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await sequence.Invoke(Sequence.Context(), data, cts.Token)
        );
        Assert.Equal(new[] { nameof(CancelStep) }, data.ExecutionLog);
    }
}
