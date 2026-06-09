namespace Spirantis.Sequencing.Test;

/// <summary>End-to-end example sequences mixing branch types, registration styles, and async.</summary>
public class ExampleSequenceTests
{
    [Fact]
    public async Task LinearPipeline_ChainsFourTrueSteps()
    {
        var sequence = SequenceBuilder
            .Create<DefaultSequenceContext, TestSequenceData>("pipeline")
            .StartWith<StepA>()
            .IfTrueRun<StepB>()
            .After<StepB>()
            .IfTrueRun<StepC>()
            .After<StepC>()
            .IfTrueRun<StepD>()
            .Build();

        var data = Sequence.Data();
        var result = await sequence.Invoke(Sequence.Context(), data);

        Assert.Equal(FunctionResultType.True, result.Type);
        Assert.Equal(
            new[] { nameof(StepA), nameof(StepB), nameof(StepC), nameof(StepD) },
            data.ExecutionLog
        );
    }

    [Fact]
    public async Task DecisionTree_WalksTrueThenFalseThenAbort()
    {
        // ReturnsTrue --True--> ReturnsFalse --False--> ReturnsAbort --Abort--> StepD
        var sequence = SequenceBuilder
            .Create<DefaultSequenceContext, TestSequenceData>("decision")
            .StartWith<ReturnsTrue>()
            .IfTrueRun<ReturnsFalse>()
            .After<ReturnsFalse>()
            .IfFalseRun<ReturnsAbort>()
            .After<ReturnsAbort>()
            .IfAbortRun<StepD>()
            .Build();

        var data = Sequence.Data();
        var result = await sequence.Invoke(Sequence.Context(), data);

        Assert.Equal(FunctionResultType.True, result.Type);
        Assert.Equal(
            new[]
            {
                nameof(ReturnsTrue),
                nameof(ReturnsFalse),
                nameof(ReturnsAbort),
                nameof(StepD),
            },
            data.ExecutionLog
        );
    }

    [Fact]
    public async Task ValueRoutedTree_DispatchesThenContinues()
    {
        // ReturnsIndeterminateAdmin --(value: admin)--> StepA --True--> StepB
        var sequence = SequenceBuilder
            .Create<DefaultSequenceContext, TestSequenceData>("value-tree")
            .StartWith<ReturnsIndeterminateAdmin>()
            .IfValueRun<StepA>(value => value is TestPayload { Kind: "admin" })
            .IfValueRun<StepC>(value => value is TestPayload { Kind: "guest" })
            .After<StepA>()
            .IfTrueRun<StepB>()
            .Build();

        var data = Sequence.Data();
        var result = await sequence.Invoke(Sequence.Context(), data);

        Assert.Equal(FunctionResultType.True, result.Type);
        Assert.Equal(
            new[] { nameof(ReturnsIndeterminateAdmin), nameof(StepA), nameof(StepB) },
            data.ExecutionLog
        );
    }

    [Fact]
    public async Task MixedRegistrationStyles_RunInOrder()
    {
        // class --True--> delegate (False) --False--> instance (custom name, returns True)
        var tail = new NamedStep("tail", FunctionResult.True());

        var sequence = SequenceBuilder
            .Create<DefaultSequenceContext, TestSequenceData>("mixed")
            .StartWith<ReturnsTrue>()
            .IfTrueRun(Delegates.FalseDelegate)
            .After(Delegates.FalseDelegate)
            .IfFalseRun(tail)
            .Build();

        var data = Sequence.Data();
        var result = await sequence.Invoke(Sequence.Context(), data);

        Assert.Equal(FunctionResultType.True, result.Type);
        Assert.Equal(
            new[] { nameof(ReturnsTrue), nameof(Delegates.FalseDelegate), "tail" },
            data.ExecutionLog
        );
    }

    [Fact]
    public async Task AsyncFunctions_AreAwaitedInOrder()
    {
        var sequence = SequenceBuilder
            .Create<DefaultSequenceContext, TestSequenceData>()
            .StartWith<AsyncReturnsTrue>()
            .IfTrueRun<StepA>()
            .Build();

        var data = Sequence.Data();
        var result = await sequence.Invoke(Sequence.Context(), data);

        Assert.Equal(FunctionResultType.True, result.Type);
        Assert.Equal(new[] { nameof(AsyncReturnsTrue), nameof(StepA) }, data.ExecutionLog);
    }
}
