using System.Reflection;
using BenchmarkDotNet.Running;

// Discovers every [Benchmark] in this assembly. Run, e.g.:
//   dotnet run -c Release --project src/Spirantis.Sequencing.Benchmarks
//   dotnet run -c Release --project src/Spirantis.Sequencing.Benchmarks -- --filter *Execution*
BenchmarkSwitcher.FromAssembly(Assembly.GetExecutingAssembly()).Run(args);
