// Roslyn analyzers/generators must target netstandard2.0 to load in every supported version
// of Visual Studio/the Roslyn compiler host, but this project's own model types (Models/*.cs)
// are `record`s - and C# records rely on `init`-only accessors under the hood even when you
// only ever use their positional-parameter syntax. `IsExternalInit` is what the C# 9+ compiler
// looks for to allow `init` accessors to compile at all, and it only ships with .NET 5+'s
// runtime, not netstandard2.0 - so it has to be hand-declared here for this project to build.
// Purely a compile-time marker type; never constructed or used at runtime.
#if NETSTANDARD2_0
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
#endif
