// This project targets netstandard2.0, but its record types (Models/*.cs) need
// IsExternalInit to compile `init` accessors - not present in netstandard2.0's BCL, so it's
// hand-declared here. Compile-time marker only, unused at runtime.
#if NETSTANDARD2_0
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
#endif
