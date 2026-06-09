namespace Spirantis.Sequencing.Test;

/// <summary>Tests for value-predicate routing on <see cref="FunctionResultType.Indeterminate"/> results.</summary>
public class ValuePredicateTests
{
    [Fact]
    public async Task ValuePredicate_RoutesByTheProducedValue()
    {
        // Guards the fix where predicates now receive result.Value (the payload) rather than the
        // wrapping FunctionResult: matching on TestPayload.Kind must select the right branch.
        var sequence = SequenceBuilder
            .Create<DefaultSequenceContext, TestSequenceData>()
            .StartWith<ReturnsIndeterminateAdmin>()
            .IfValueRun<StepA>(value => value is TestPayload { Kind: "admin" })
            .IfValueRun<StepB>(value => value is TestPayload { Kind: "guest" })
            .Build();

        var data = Sequence.Data();
        var result = await sequence.Invoke(Sequence.Context(), data);

        Assert.Equal(FunctionResultType.True, result.Type);
        Assert.Equal(new[] { nameof(ReturnsIndeterminateAdmin), nameof(StepA) }, data.ExecutionLog);
    }

    [Fact]
    public async Task ValuePredicate_SelectsTheMatchingBranchAmongSeveral()
    {
        var sequence = SequenceBuilder
            .Create<DefaultSequenceContext, TestSequenceData>()
            .StartWith<ReturnsIndeterminateGuest>()
            .IfValueRun<StepA>(value => value is TestPayload { Kind: "admin" })
            .IfValueRun<StepB>(value => value is TestPayload { Kind: "guest" })
            .Build();

        var data = Sequence.Data();
        var result = await sequence.Invoke(Sequence.Context(), data);

        Assert.Equal(FunctionResultType.True, result.Type);
        Assert.Equal(new[] { nameof(ReturnsIndeterminateGuest), nameof(StepB) }, data.ExecutionLog);
    }

    [Fact]
    public async Task IfValueElseRun_ActsAsCatchAllForAnyValue()
    {
        var sequence = SequenceBuilder
            .Create<DefaultSequenceContext, TestSequenceData>()
            .StartWith<ReturnsIndeterminateGuest>()
            .IfValueElseRun(Delegates.FalseDelegate)
            .Build();

        var data = Sequence.Data();
        var result = await sequence.Invoke(Sequence.Context(), data);

        Assert.Equal(FunctionResultType.False, result.Type);
        Assert.Equal(
            new[] { nameof(ReturnsIndeterminateGuest), nameof(Delegates.FalseDelegate) },
            data.ExecutionLog
        );
    }

    [Fact]
    public async Task Indeterminate_WithNoMatchingPredicate_FallsBackToOnAny()
    {
        var sequence = SequenceBuilder
            .Create<DefaultSequenceContext, TestSequenceData>()
            .StartWith<ReturnsIndeterminateGuest>()
            .IfValueRun<StepA>(value => value is TestPayload { Kind: "admin" }) // never matches
            .IfAnyRun<StepB>()
            .Build();

        var data = Sequence.Data();
        var result = await sequence.Invoke(Sequence.Context(), data);

        Assert.Equal(FunctionResultType.True, result.Type);
        Assert.Equal(new[] { nameof(ReturnsIndeterminateGuest), nameof(StepB) }, data.ExecutionLog);
    }

    [Fact]
    public async Task ValuePredicates_AreIgnoredForNonIndeterminateResults()
    {
        // A True result that carries a value still routes via OnTrue; value predicates are only
        // consulted for Indeterminate results (documented behavior).
        var sequence = SequenceBuilder
            .Create<DefaultSequenceContext, TestSequenceData>()
            .StartWith<ReturnsTrueWithValue>()
            .IfTrueRun<StepA>()
            .IfValueRun<StepB>(_ => true)
            .Build();

        var data = Sequence.Data();
        var result = await sequence.Invoke(Sequence.Context(), data);

        Assert.Equal(FunctionResultType.True, result.Type);
        Assert.Equal(new[] { nameof(ReturnsTrueWithValue), nameof(StepA) }, data.ExecutionLog);
        Assert.DoesNotContain(nameof(StepB), data.ExecutionLog);
    }
}
