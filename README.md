# Spirantis.Sequencing

A small, fluent engine for composing asynchronous workflows ("sequences") out of independent
functions. Each function returns a typed result, and the result decides which function runs next —
so a sequence is a branching tree of steps rather than a fixed pipeline.

- **`Spirantis.Sequencing.Abstraction`** — the contracts: `ISequenceFunction`, `FunctionResult`,
  and `ISequenceContext`. Depends on [`Spirantis.Result`](https://www.nuget.org/packages/Spirantis.Result).
- **`Spirantis.Sequencing`** — the fluent `SequenceBuilder` and the execution engine.

Targets **.NET 10**.

## Concepts

| Type | Role |
| --- | --- |
| `ISequenceFunction<TContext, TData>` | A unit of work. Returns a `FunctionResult`. Both type parameters are **contravariant** (`in`). |
| `FunctionResult` | The outcome of a function — a `Result<object>` tagged with a `FunctionResultType`. Created via `FunctionResult.True()/False()/Abort()/Indeterminate(value)`. |
| `FunctionResultType` | `True`, `False`, `Indeterminate`, `Abort` — selects the next branch. |
| `ISequenceContext` | Ambient, run-scoped environment shared by every function: `Logger`, `CorrelationKey`, `Stopwatch`, plus any services a richer context adds. `DefaultSequenceContext` is provided. |
| *(data)* | The per-run payload handed step to step — **any type you like** (record, DTO, or a set of slice interfaces). No base type or interface required. |
| `SequenceBuilder` | Fluent builder that wires functions and their reactions, then `Build()`s an executable sequence. |

### How a result routes

After a function runs, the engine picks the next function by result type:

- `True` → the `IfTrueRun` reaction
- `False` → the `IfFalseRun` reaction
- `Abort` → the `IfAbortRun` reaction
- `Indeterminate` → the first matching `IfValueRun` predicate (evaluated against the produced value)
- if none of the above matched → the `IfAnyRun` fallback
- if there is still nothing → the sequence ends and returns that result

## Quick start

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using Spirantis.Sequencing;
using Spirantis.Sequencing.Abstraction;

// 1. Your per-run data — any type; no base class or interface required.
public sealed class OrderData
{
    public decimal Amount { get; set; }
    public bool Charged { get; set; }
}

// 2. Functions. Each returns a FunctionResult that drives branching.
public sealed class ValidateOrder : ISequenceFunction<DefaultSequenceContext, OrderData>
{
    public ValueTask<FunctionResult> Invoke(
        DefaultSequenceContext ctx, OrderData data, CancellationToken ct = default) =>
        data.Amount > 0 ? FunctionResult.True() : FunctionResult.False();
}

public sealed class ChargeCard : ISequenceFunction<DefaultSequenceContext, OrderData>
{
    public ValueTask<FunctionResult> Invoke(
        DefaultSequenceContext ctx, OrderData data, CancellationToken ct = default)
    {
        data.Charged = true;
        return FunctionResult.True();
    }
}

// 3. Compose and run.
var sequence = SequenceBuilder
    .Create<DefaultSequenceContext, OrderData>()
    .Run<ValidateOrder>()
        .IfTrueRun<ChargeCard>()
        .IfFalseRun<RejectOrder>()
    .After<ChargeCard>()
        .IfTrueRun<ShipOrder>()
    .Build();

var result = await sequence.Invoke(
    new DefaultSequenceContext(NullLogger.Instance),
    new OrderData { Amount = 42m },
    cancellationToken);
```

A built sequence is immutable and reusable across invocations (pass fresh data each time).

### Cancellation

`Invoke` takes a `CancellationToken`. The engine checks it **between steps** (throwing
`OperationCanceledException` before each function runs), so a sequence stops promptly even if an
individual function doesn't observe the token — and each function receives it to honor in its own
async work. The token is optional (`default` = `CancellationToken.None`).

### What a sequence function can be

Anywhere a step is registered (`Run`, `After`, `IfTrueRun`, `IfFalseRun`, `IfAbortRun`,
`IfValueRun`, `IfAnyRun`) it can be supplied as any of the following:

- **A class** implementing `ISequenceFunction<,>`, registered by type and instantiated for you:
  ```csharp
  .Run<ValidateOrder>()
  ```
- **A class instance** you create yourself (useful when the function has constructor state):
  ```csharp
  .Run(new ValidateOrder(threshold: 100m))
  ```
- **A method** with the matching signature (`(TContext, TData) => ValueTask<FunctionResult>`):
  ```csharp
  .Run(OrderFunctions.Validate)        // method group
  ```
- **An anonymous method / lambda**:
  ```csharp
  .Run((ctx, data) => data.Amount > 0 ? FunctionResult.True() : FunctionResult.False(), "validate")
  ```
- **Another sequence.** `Build()` returns an `ISequenceFunction`, so a whole sequence can be
  embedded as a single step in a larger one (the shared data flows into the inner run, and the
  outer routes on the inner's final result):
  ```csharp
  var charge = SequenceBuilder.Create<DefaultSequenceContext, OrderData>("charge")
      .Run<AuthorizeCard>().IfTrueRun<CaptureFunds>().Build();

  var checkout = SequenceBuilder.Create<DefaultSequenceContext, OrderData>("checkout")
      .Run<ValidateOrder>()
          .IfTrueRun(charge)        // the charge sequence is itself a function
      .After(charge)
          .IfTrueRun<ShipOrder>()
      .Build();
  ```

### Naming, reuse, and ordering

Nodes are keyed by **name** — `ISequenceFunction.GetFunctionName()`, which defaults to the type
name (and to the delegate's method name for method groups / lambdas).

- **Reuse a function under multiple names.** Pass a name suffix to register the same function as
  two distinct nodes, each with its own reactions:
  ```csharp
  .Run<Gate>()
      .IfTrueRun<Notify>("Approved")     // node "NotifyApproved"
      .IfFalseRun<Notify>("Rejected")    // node "NotifyRejected"
  .After<Notify>("Approved")
      .IfTrueRun<Archive>()              // reaction for the approved node only
  .After<Notify>("Rejected")
      .IfTrueRun<Escalate>()             // reaction for the rejected node only
  ```
  For anonymous methods, the generated method name is opaque, so pass an explicit name/suffix if
  you need to attach reactions to a lambda node.

- **Declare reactions in any order.** Wiring is resolved by name at `Build()` time, so the order in
  which you declare nodes and their reactions does not matter — you can wire a later step before an
  earlier one. (`Run` still designates the entry point.)

### Value-based routing

`Indeterminate(value)` results are routed by predicates over the value:

```csharp
.Run<Classify>()                                  // returns Indeterminate(payload)
    .IfValueRun<HandleAdmin>(v => v is User { Role: "admin" })
    .IfValueRun<HandleGuest>(v => v is User { Role: "guest" })
    .IfAnyRun<HandleEveryoneElse>()                     // fallback when no predicate matched
```

### Contravariance: narrow views of context and data

`ISequenceFunction<in TSequenceContext, in TSequenceData>` is contravariant in **both** type
parameters, so a function can target a narrow *view* and still be dropped into a sequence whose
concrete type is richer. Every function in a run receives the same context and data instances.

**Data slices** — a function declares only the pieces it touches; the concrete payload implements
them all (functions share the overlapping pieces and stay independently reusable):

```csharp
interface IParseStage   : IHasRawInput, IHasParsed { }
interface IComputeStage : IHasParsed, IHasComputed { }
sealed class PipelineData : IParseStage, IComputeStage { /* ... */ }

// ParseInput : ISequenceFunction<.., IParseStage> and ComputeValue : ISequenceFunction<.., IComputeStage>
// both run in a SequenceBuilder.Create<.., PipelineData>() sequence and share `Parsed`.
```

**Richer context** — the same idea applies to the context. A function targeting the base
`ISequenceContext` is reusable in a sequence whose context adds services (e.g. an HTTP client):

```csharp
interface IApiContext : ISequenceContext { HttpClient Http { get; } }

// LogStep : ISequenceFunction<ISequenceContext, T>  (only needs ambient services)
// CallApi : ISequenceFunction<IApiContext, T>       (uses the HTTP client)
// both run in a SequenceBuilder.Create<IApiContext, T>() sequence; LogStep is reused as-is.
```

**Data-less functions** — a function that needs no payload is just the extreme of a data slice: a
delegate discards the data parameter (`(context, _, ct) => …`), and a class uses `object?` as its
data type. Contravariance makes such a class run in *any* sequence and chain freely with data-ful
functions — no separate abstraction needed:

```csharp
// Runs in any Create<TContext, TData>() sequence; the next step can still be data-ful.
sealed class PingHealthCheck : ISequenceFunction<MyContext, object?>
{
    public ValueTask<FunctionResult> Invoke(MyContext ctx, object? _, CancellationToken ct) => ...;
}
```

## Repository layout

```
src/
  Spirantis.Sequencing.slnx            # solution
  Directory.Build.props                # shared build settings (versioning, CSharpier)
  Spirantis.Sequencing.Abstraction/    # contracts (NuGet package)
  Spirantis.Sequencing/                # builder + engine (NuGet package)
  Spirantis.Sequencing.Test/           # xUnit v3 tests
  Spirantis.Sequencing.Benchmarks/     # BenchmarkDotNet performance benchmarks
```

## Build & test

```bash
dotnet build src/Spirantis.Sequencing.slnx
dotnet test  src/Spirantis.Sequencing.slnx
```

Formatting is enforced at build time by [CSharpier](https://csharpier.com/) (`CSharpier_Check`).
To format locally:

```bash
dotnet tool restore
dotnet csharpier format src
```

## Benchmarks

Performance benchmarks live in `Spirantis.Sequencing.Benchmarks`, built on
[BenchmarkDotNet](https://benchmarkdotnet.org/). They are **not** part of `dotnet test` — they run
in Release as a separate console app so the numbers are meaningful.

```bash
# everything
dotnet run -c Release --project src/Spirantis.Sequencing.Benchmarks

# a subset
dotnet run -c Release --project src/Spirantis.Sequencing.Benchmarks -- --filter '*Execution*'

# quick, noisy pass while iterating (not for recording results)
dotnet run -c Release --project src/Spirantis.Sequencing.Benchmarks -- --job short
```

Results (and Markdown/CSV/JSON exports) are written to `BenchmarkDotNet.Artifacts/`.

**Execution** (cost of running a built sequence):

| Benchmark | Tracks |
| --- | --- |
| `ExecutionBenchmarks` (`InvokeLinearChain` / `InvokeFalseChain`) | per-node cost & allocation for True- vs False-routing (depth 10/100/1000) |
| `ValueRoutingBenchmarks.InvokeValueDispatch` | cost of dispatching an `Indeterminate` result across N value predicates (2/8/32) |
| `NestedSequenceBenchmarks.InvokeNestedChain` | overhead of running sequences nested as steps inside another sequence |

**Build** (cost of constructing a sequence):

| Benchmark | Tracks |
| --- | --- |
| `BuildByStyleBenchmarks` (`Class` / `Instance` / `MethodGroup` / `Lambda`) | build cost per registration style (`Class` is the baseline) |
| `DiamondBuildBenchmarks.BuildDiamondChain` | build cost when continuations are shared (exponential re-expansion) |

> Note: at runtime every registration style (class / instance / method / lambda) collapses to the
> same delegate call, so execution cost is style-independent — hence the build benchmark compares
> styles while the execution benchmarks vary by sequence *shape*.

**Tracking a change:** run the relevant filter on the base commit, keep the table from
`BenchmarkDotNet.Artifacts/`; run the same filter on the change; compare `Mean` and `Allocated`.
Use the default job (not `--job short`) for anything you record.

## License

[MIT](LICENSE)
