using Microsoft.Extensions.Logging.Abstractions;

namespace Spirantis.Sequencing.Test;

/// <summary>
/// The context is contravariant too: a function targeting the base <see cref="ISequenceContext"/>
/// is reusable in a sequence whose concrete context is richer (adds an ambient service).
/// </summary>
public class ContravariantContextTests
{
    [Fact]
    public async Task BaseContextFunction_RunsInARicherContextSequence()
    {
        // LogStep targets ISequenceContext (base); CallApiStep targets IApiContext (adds a client).
        // Both run in an IApiContext sequence — LogStep is reused via context contravariance.
        var sequence = SequenceBuilder
            .Create<IApiContext, TestSequenceData>()
            .Run<LogStep>()
            .IfTrueRun<CallApiStep>()
            .Build();

        var context = new ApiContext(NullLogger.Instance);
        var data = Sequence.Data();
        var result = await sequence.Invoke(context, data);

        Assert.Equal(FunctionResultType.True, result.Type);
        Assert.Equal(new[] { nameof(LogStep), nameof(CallApiStep) }, data.ExecutionLog);
        Assert.Equal(new[] { "GET /thing" }, context.ApiCalls);
    }
}
