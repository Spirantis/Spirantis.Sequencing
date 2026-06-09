namespace Spirantis.Sequencing.Test;

/// <summary>Tests for builder-level behavior independent of any single sequence run.</summary>
public class BuilderTests
{
    [Fact]
    public void Build_WithoutAnyFunction_Throws()
    {
        var builder = SequenceBuilder.Create<DefaultSequenceContext, TestSequenceData>();

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public async Task ReactionsCanBeDeclaredInAnyOrder()
    {
        // Wiring is resolved by name at Build time, so declaration order does not matter: here the
        // deeper link (StepB -> StepC) is declared BEFORE the earlier link (StepA -> StepB).
        var builder = SequenceBuilder.Create<DefaultSequenceContext, TestSequenceData>();

        var a = builder.Run<StepA>();
        var b = a.After<StepB>();
        b.IfTrueRun<StepC>(); // StepB -> StepC declared first
        a.IfTrueRun<StepB>(); // StepA -> StepB declared afterward

        var sequence = builder.Build();

        var data = Sequence.Data();
        var result = await sequence.Invoke(Sequence.Context(), data);

        Assert.Equal(FunctionResultType.True, result.Type);
        Assert.Equal(new[] { nameof(StepA), nameof(StepB), nameof(StepC) }, data.ExecutionLog);
    }

    [Fact]
    public void Build_WithASelfCycle_Throws()
    {
        // StepA's True reaction points back at StepA. Before cycle detection this recursed forever
        // (StackOverflow); now it fails fast with a clear error.
        var builder = SequenceBuilder.Create<DefaultSequenceContext, TestSequenceData>();
        var a = builder.Run<StepA>();
        a.IfTrueRun<StepA>();

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains($"{nameof(StepA)} -> {nameof(StepA)}", ex.Message);
    }

    [Fact]
    public void Build_WithAMultiNodeCycle_ReportsTheFullChain()
    {
        // StepA -> StepB -> StepA.
        var builder = SequenceBuilder.Create<DefaultSequenceContext, TestSequenceData>();
        var a = builder.Run<StepA>();
        var b = a.After<StepB>();
        a.IfTrueRun<StepB>();
        b.IfTrueRun<StepA>();

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains($"{nameof(StepA)} -> {nameof(StepB)} -> {nameof(StepA)}", ex.Message);
    }
}
