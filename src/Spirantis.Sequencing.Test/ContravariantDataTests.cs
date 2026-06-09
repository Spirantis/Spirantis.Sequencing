namespace Spirantis.Sequencing.Test;

/// <summary>
/// Tests showing functions that each target a narrow contravariant data slice, share the overlapping
/// pieces through the single data instance, and stay independent and reusable across sequences.
/// </summary>
public class ContravariantDataTests
{
    [Fact]
    public async Task ContravariantSlices_EachFunctionSeesOnlyItsSliceYetSharesData()
    {
        // ParseInput targets IParseStage (RawInput+Parsed), ComputeValue targets IComputeStage
        // (Parsed+Computed), FormatOutput targets IFormatStage (Computed+Output). None of them can
        // even reference the others' private slices, yet because PipelineData implements all three
        // views they hand data down the chain through the single shared instance.
        var sequence = SequenceBuilder
            .Create<DefaultSequenceContext, PipelineData>()
            .StartWith<ParseInput>()
            .IfTrueRun<ComputeValue>()
            .After<ComputeValue>()
            .IfTrueRun<FormatOutput>()
            .Build();

        var data = new PipelineData { RawInput = "21" };
        var result = await sequence.Invoke(Sequence.Context(), data);

        Assert.Equal(FunctionResultType.True, result.Type);
        Assert.Equal(21, data.Parsed); // ParseInput read RawInput, wrote Parsed (shared with compute)
        Assert.Equal(42, data.Computed); // ComputeValue read Parsed, wrote Computed (shared with format)
        Assert.Equal("result=42", data.Output); // FormatOutput read Computed, wrote Output
    }

    [Fact]
    public async Task ContravariantSlices_SingleSliceGateCanAbortTheChain()
    {
        // RejectNegative targets only the Parsed slice (IValidateStage). When it aborts, the
        // downstream compute/format stages never run. The same built sequence is reused twice.
        var sequence = SequenceBuilder
            .Create<DefaultSequenceContext, PipelineData>()
            .StartWith<ParseInput>()
            .IfTrueRun<RejectNegative>()
            .After<RejectNegative>()
            .IfTrueRun<ComputeValue>()
            .After<ComputeValue>()
            .IfTrueRun<FormatOutput>()
            .Build();

        var rejected = new PipelineData { RawInput = "-5" };
        var rejectedResult = await sequence.Invoke(Sequence.Context(), rejected);

        Assert.Equal(FunctionResultType.Abort, rejectedResult.Type);
        Assert.Equal(-5, rejected.Parsed);
        Assert.Equal(0, rejected.Computed); // compute never ran
        Assert.Equal("", rejected.Output); // format never ran

        var accepted = new PipelineData { RawInput = "21" };
        var acceptedResult = await sequence.Invoke(Sequence.Context(), accepted);

        Assert.Equal(FunctionResultType.True, acceptedResult.Type);
        Assert.Equal("result=42", accepted.Output);
    }

    [Fact]
    public async Task ContravariantSlices_TheSameFunctionIsReusableAcrossDifferentSequences()
    {
        // ComputeValue (typed to the IComputeStage slice) is reused here in a different sequence
        // over a different concrete data type, MiniData, which implements only that slice. The
        // function is fully independent of which sequence or data class it runs in.
        var sequence = SequenceBuilder
            .Create<DefaultSequenceContext, MiniData>()
            .StartWith<ComputeValue>()
            .Build();

        var data = new MiniData { Parsed = 5 };
        var result = await sequence.Invoke(Sequence.Context(), data);

        Assert.Equal(FunctionResultType.True, result.Type);
        Assert.Equal(10, data.Computed);
    }
}
