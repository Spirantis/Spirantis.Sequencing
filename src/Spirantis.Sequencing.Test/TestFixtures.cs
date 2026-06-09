using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Spirantis.Sequencing.Abstraction;

namespace Spirantis.Sequencing.Test;

/// <summary>
/// Sequence data that records, in order, the name each function logs as it runs. Because the same
/// instance flows through the whole sequence, <see cref="ExecutionLog"/> is the exact execution path.
/// </summary>
internal sealed class TestSequenceData
{
    public List<string> ExecutionLog { get; } = [];

    /// <summary>Lets a <see cref="Gate"/> pick its True/False branch from the data.</summary>
    public bool TakeTrue { get; set; }
}

/// <summary>A value payload used to exercise <c>IfValueRun</c> predicates.</summary>
internal sealed record TestPayload(string Kind);

/// <summary>Factory helpers for the context/data passed into a sequence.</summary>
internal static class Sequence
{
    public static DefaultSequenceContext Context() => new(NullLogger.Instance);

    public static TestSequenceData Data() => new();
}

/// <summary>Delegate-style functions (named methods so the engine derives a stable name from them).</summary>
internal static class Delegates
{
    public static ValueTask<FunctionResult> TrueDelegate(
        DefaultSequenceContext context,
        TestSequenceData data,
        CancellationToken cancellationToken
    )
    {
        data.ExecutionLog.Add(nameof(TrueDelegate));
        return FunctionResult.True();
    }

    public static ValueTask<FunctionResult> FalseDelegate(
        DefaultSequenceContext context,
        TestSequenceData data,
        CancellationToken cancellationToken
    )
    {
        data.ExecutionLog.Add(nameof(FalseDelegate));
        return FunctionResult.False();
    }
}

// --- Class-based building blocks. Each logs its own type name, then returns a fixed result. ---

internal sealed class ReturnsTrue : ISequenceFunction<DefaultSequenceContext, TestSequenceData>
{
    public ValueTask<FunctionResult> Invoke(
        DefaultSequenceContext context,
        TestSequenceData data,
        CancellationToken cancellationToken
    )
    {
        data.ExecutionLog.Add(nameof(ReturnsTrue));
        return FunctionResult.True();
    }
}

internal sealed class ReturnsFalse : ISequenceFunction<DefaultSequenceContext, TestSequenceData>
{
    public ValueTask<FunctionResult> Invoke(
        DefaultSequenceContext context,
        TestSequenceData data,
        CancellationToken cancellationToken
    )
    {
        data.ExecutionLog.Add(nameof(ReturnsFalse));
        return FunctionResult.False();
    }
}

internal sealed class ReturnsAbort : ISequenceFunction<DefaultSequenceContext, TestSequenceData>
{
    public ValueTask<FunctionResult> Invoke(
        DefaultSequenceContext context,
        TestSequenceData data,
        CancellationToken cancellationToken
    )
    {
        data.ExecutionLog.Add(nameof(ReturnsAbort));
        return FunctionResult.Abort();
    }
}

/// <summary>Branches on <see cref="TestSequenceData.TakeTrue"/>: returns True or False accordingly.</summary>
internal sealed class Gate : ISequenceFunction<DefaultSequenceContext, TestSequenceData>
{
    public ValueTask<FunctionResult> Invoke(
        DefaultSequenceContext context,
        TestSequenceData data,
        CancellationToken cancellationToken
    )
    {
        data.ExecutionLog.Add(nameof(Gate));
        return data.TakeTrue ? FunctionResult.True() : FunctionResult.False();
    }
}

internal sealed class ReturnsTrueWithValue
    : ISequenceFunction<DefaultSequenceContext, TestSequenceData>
{
    public ValueTask<FunctionResult> Invoke(
        DefaultSequenceContext context,
        TestSequenceData data,
        CancellationToken cancellationToken
    )
    {
        data.ExecutionLog.Add(nameof(ReturnsTrueWithValue));
        return FunctionResult.True(new TestPayload("ok"));
    }
}

internal sealed class ReturnsIndeterminateAdmin
    : ISequenceFunction<DefaultSequenceContext, TestSequenceData>
{
    public ValueTask<FunctionResult> Invoke(
        DefaultSequenceContext context,
        TestSequenceData data,
        CancellationToken cancellationToken
    )
    {
        data.ExecutionLog.Add(nameof(ReturnsIndeterminateAdmin));
        return FunctionResult.Indeterminate(new TestPayload("admin"));
    }
}

internal sealed class ReturnsIndeterminateGuest
    : ISequenceFunction<DefaultSequenceContext, TestSequenceData>
{
    public ValueTask<FunctionResult> Invoke(
        DefaultSequenceContext context,
        TestSequenceData data,
        CancellationToken cancellationToken
    )
    {
        data.ExecutionLog.Add(nameof(ReturnsIndeterminateGuest));
        return FunctionResult.Indeterminate(new TestPayload("guest"));
    }
}

internal sealed class AsyncReturnsTrue : ISequenceFunction<DefaultSequenceContext, TestSequenceData>
{
    public async ValueTask<FunctionResult> Invoke(
        DefaultSequenceContext context,
        TestSequenceData data,
        CancellationToken cancellationToken
    )
    {
        await Task.Delay(1);
        data.ExecutionLog.Add(nameof(AsyncReturnsTrue));
        return FunctionResult.True();
    }
}

// Distinctly-named pass-through steps (all return True) for tracing multi-step pipelines.

internal sealed class StepA : ISequenceFunction<DefaultSequenceContext, TestSequenceData>
{
    public ValueTask<FunctionResult> Invoke(
        DefaultSequenceContext context,
        TestSequenceData data,
        CancellationToken cancellationToken
    )
    {
        data.ExecutionLog.Add(nameof(StepA));
        return FunctionResult.True();
    }
}

internal sealed class StepB : ISequenceFunction<DefaultSequenceContext, TestSequenceData>
{
    public ValueTask<FunctionResult> Invoke(
        DefaultSequenceContext context,
        TestSequenceData data,
        CancellationToken cancellationToken
    )
    {
        data.ExecutionLog.Add(nameof(StepB));
        return FunctionResult.True();
    }
}

internal sealed class StepC : ISequenceFunction<DefaultSequenceContext, TestSequenceData>
{
    public ValueTask<FunctionResult> Invoke(
        DefaultSequenceContext context,
        TestSequenceData data,
        CancellationToken cancellationToken
    )
    {
        data.ExecutionLog.Add(nameof(StepC));
        return FunctionResult.True();
    }
}

internal sealed class StepD : ISequenceFunction<DefaultSequenceContext, TestSequenceData>
{
    public ValueTask<FunctionResult> Invoke(
        DefaultSequenceContext context,
        TestSequenceData data,
        CancellationToken cancellationToken
    )
    {
        data.ExecutionLog.Add(nameof(StepD));
        return FunctionResult.True();
    }
}

/// <summary>
/// A function whose node name is supplied explicitly (overriding the default type name), used to
/// exercise instance registration and the <see cref="ISequenceFunction{T1, T2}.GetFunctionName"/> hook.
/// </summary>
internal sealed class NamedStep(string name, FunctionResult result)
    : ISequenceFunction<DefaultSequenceContext, TestSequenceData>
{
    public string GetFunctionName() => name;

    public ValueTask<FunctionResult> Invoke(
        DefaultSequenceContext context,
        TestSequenceData data,
        CancellationToken cancellationToken
    )
    {
        data.ExecutionLog.Add(name);
        return result;
    }
}

/// <summary>Logs, then cancels the supplied token source — used to test mid-sequence cancellation.</summary>
internal sealed class CancelStep(CancellationTokenSource source)
    : ISequenceFunction<DefaultSequenceContext, TestSequenceData>
{
    public ValueTask<FunctionResult> Invoke(
        DefaultSequenceContext context,
        TestSequenceData data,
        CancellationToken cancellationToken
    )
    {
        data.ExecutionLog.Add(nameof(CancelStep));
        source.Cancel();
        return FunctionResult.True();
    }
}

// --- Contravariant data slices --------------------------------------------------------------
// ISequenceFunction is contravariant in its data type (`in TSequenceData`). Each function below
// targets a different *narrow view* of the data (a composite of fine-grained slices), and the
// concrete PipelineData implements them all. So every function sees only the pieces it needs,
// adjacent functions share the overlapping piece, and they hand data down the chain through the
// single instance every function in a run receives.
//
//   slices:  RawInput(A)   Parsed(B)   Computed(C)   Output(D)
//   views:   IParseStage = A+B,  IComputeStage = B+C,  IFormatStage = C+D
//   PipelineData implements IParseStage, IComputeStage, IFormatStage (and IValidateStage = B)

// Fine-grained data slices. A function never references these directly; it uses a composite view.
internal interface IHasRawInput
{
    string RawInput { get; }
}

internal interface IHasParsed
{
    int Parsed { get; set; }
}

internal interface IHasComputed
{
    int Computed { get; set; }
}

internal interface IHasOutput
{
    string Output { get; set; }
}

// Composite views: each is the slice one function works with (the "AB" / "BC" / "CD" of the ask).
internal interface IParseStage : IHasRawInput, IHasParsed;

internal interface IComputeStage : IHasParsed, IHasComputed;

internal interface IFormatStage : IHasComputed, IHasOutput;

internal interface IValidateStage : IHasParsed;

/// <summary>The concrete data: implements every stage view, so it can flow through all of them.</summary>
internal sealed class PipelineData : IParseStage, IComputeStage, IFormatStage, IValidateStage
{
    public string RawInput { get; init; } = "";
    public int Parsed { get; set; }
    public int Computed { get; set; }
    public string Output { get; set; } = "";
}

/// <summary>Sees RawInput + Parsed (A+B) only: reads the input, writes the parsed value.</summary>
internal sealed class ParseInput : ISequenceFunction<DefaultSequenceContext, IParseStage>
{
    public ValueTask<FunctionResult> Invoke(
        DefaultSequenceContext context,
        IParseStage data,
        CancellationToken cancellationToken
    )
    {
        data.Parsed = int.Parse(data.RawInput, CultureInfo.InvariantCulture);
        return FunctionResult.True();
    }
}

/// <summary>Sees Parsed + Computed (B+C) only: reads the parsed value, writes the computed value.</summary>
internal sealed class ComputeValue : ISequenceFunction<DefaultSequenceContext, IComputeStage>
{
    public ValueTask<FunctionResult> Invoke(
        DefaultSequenceContext context,
        IComputeStage data,
        CancellationToken cancellationToken
    )
    {
        data.Computed = data.Parsed * 2;
        return FunctionResult.True();
    }
}

/// <summary>Sees Computed + Output (C+D) only: reads the computed value, writes the output.</summary>
internal sealed class FormatOutput : ISequenceFunction<DefaultSequenceContext, IFormatStage>
{
    public ValueTask<FunctionResult> Invoke(
        DefaultSequenceContext context,
        IFormatStage data,
        CancellationToken cancellationToken
    )
    {
        data.Output = $"result={data.Computed}";
        return FunctionResult.True();
    }
}

/// <summary>Sees Parsed (B) only: a single-slice gate that aborts the sequence on a negative value.</summary>
internal sealed class RejectNegative : ISequenceFunction<DefaultSequenceContext, IValidateStage>
{
    public ValueTask<FunctionResult> Invoke(
        DefaultSequenceContext context,
        IValidateStage data,
        CancellationToken cancellationToken
    ) => data.Parsed >= 0 ? FunctionResult.True() : FunctionResult.Abort();
}

/// <summary>
/// A different concrete data type implementing only the compute slice, so the very same
/// <see cref="ComputeValue"/> function can be reused in a completely different sequence.
/// </summary>
internal sealed class MiniData : IComputeStage
{
    public int Parsed { get; set; }
    public int Computed { get; set; }
}

// --- Contravariant context ------------------------------------------------------------------
// ISequenceContext is also contravariant (`in TSequenceContext`). A richer context can add extra
// ambient services (the classic example: an HttpClient), and a function targeting the base context
// is reusable in a sequence whose context is the richer one.

/// <summary>A richer context that adds a fake API client (stands in for an HttpClient).</summary>
internal interface IApiContext : ISequenceContext
{
    List<string> ApiCalls { get; }
}

internal sealed class ApiContext(ILogger logger) : DefaultSequenceContext(logger), IApiContext
{
    public List<string> ApiCalls { get; } = [];
}

/// <summary>Targets the base <see cref="ISequenceContext"/>: reusable in any richer-context sequence.</summary>
internal sealed class LogStep : ISequenceFunction<ISequenceContext, TestSequenceData>
{
    public ValueTask<FunctionResult> Invoke(
        ISequenceContext context,
        TestSequenceData data,
        CancellationToken cancellationToken
    )
    {
        data.ExecutionLog.Add(nameof(LogStep));
        return FunctionResult.True();
    }
}

/// <summary>Targets the richer <see cref="IApiContext"/>: uses the service only that context provides.</summary>
internal sealed class CallApiStep : ISequenceFunction<IApiContext, TestSequenceData>
{
    public ValueTask<FunctionResult> Invoke(
        IApiContext context,
        TestSequenceData data,
        CancellationToken cancellationToken
    )
    {
        data.ExecutionLog.Add(nameof(CallApiStep));
        context.ApiCalls.Add("GET /thing");
        return FunctionResult.True();
    }
}
