using System.Collections.Generic;

namespace AnvilMap.Generator;

// C# 14 interceptor-based direct dispatch. Redirects a call to
// GeneratedMappings.Map<TSource,TDestination>(...) that the discovery stage in
// MappingSourceGenerator.cs found syntactically visible in the consumer's own source straight to
// the concrete To{Dest}() method, skipping the FrozenDictionary/Dictionary lookup entirely for
// that one call site. Deliberately additive only: the dispatcher in MappingEmitter.Dispatcher.cs
// is untouched and still handles everything not intercepted here (behind IMapper, reflection, a
// call site outside the discovery stage's reach, etc.) - and, by design, never anything called
// through the IMapper interface even when statically visible, since interceptors redirect by
// source location regardless of the runtime type behind an interface receiver, which would
// silently defeat mocking/DI substitution for IMapper.
internal static partial class MappingEmitter
{
    // Returns whether anything was actually emitted, so the caller knows whether the separate
    // InterceptsLocation polyfill (a top-level type outside `namespace AnvilMap`) is needed.
    private static bool EmitInterceptors(
        CodeWriter writer,
        IReadOnlyCollection<InterceptedMapCall> interceptedCalls,
        ConsumerCapabilities capabilities,
        IReadOnlyDictionary<(string Source, string Destination), MappingModel> byPair)
    {
        // UseCSharp14 is a conservative proxy for "toolchain new enough" - see
        // MappingSourceGenerator.cs's own comment on this flag. Interceptors themselves are gated
        // purely by the InterceptorsNamespaces compiler switch (confirmed empirically), not by
        // LanguageVersion, but emitting them for a consumer this flag says isn't ready risks
        // relying on toolchain behavior nothing here has verified.
        if (!capabilities.UseCSharp14 || interceptedCalls.Count == 0)
        {
            return false;
        }

        // Group by (source, destination, arg-shape) so every call site sharing the same shape
        // stacks onto one generated method via multiple [InterceptsLocation] attributes, rather
        // than emitting one method per call site (confirmed AllowMultiple stacking works).
        var groups = new Dictionary<(string Source, string Destination, bool TwoArg), List<InterceptedMapCall>>();

        foreach (var call in interceptedCalls)
        {
            if (!byPair.TryGetValue((call.SourceTypeName, call.DestinationTypeName), out var mapping))
            {
                continue; // No resolved mapping for this pair - leave it to the dictionary dispatcher.
            }

            if (call.IsTwoArgOverload && HasInitOnlyProperty(mapping))
            {
                continue; // Mirrors _mapInto's own skip in MappingEmitter.Dispatcher.cs - no two-arg To{Dest}() exists to redirect to.
            }

            var key = (call.SourceTypeName, call.DestinationTypeName, call.IsTwoArgOverload);
            if (!groups.TryGetValue(key, out var list))
            {
                groups[key] = list = new List<InterceptedMapCall>();
            }

            list.Add(call);
        }

        if (groups.Count == 0)
        {
            return false;
        }

        writer.WriteLine();
        writer.WriteLine("// Intercepts direct GeneratedMappings.Map<TSource,TDestination>(...) calls the generator");
        writer.WriteLine("// found statically visible in this consumer's source, redirecting them straight to the concrete");
        writer.WriteLine("// To{Dest}() method - additive only, the dispatcher above still handles everything else.");
        using (writer.Block("file static class Interceptors"))
        {
            var index = 0;
            foreach (var group in groups)
            {
                var (sourceTypeName, destinationTypeName, isTwoArg) = group.Key;
                var mapping = byPair[(sourceTypeName, destinationTypeName)];
                var destinationSimpleName = mapping.Destination.SimpleName;

                foreach (var call in group.Value)
                {
                    writer.WriteLine($"[System.Runtime.CompilerServices.InterceptsLocation({call.LocationVersion}, \"{call.LocationData}\")]");
                }

                var methodName = $"Intercepted_{destinationSimpleName}_{index++}";

                if (isTwoArg)
                {
                    writer.WriteLine($"public static {destinationTypeName} {methodName}({sourceTypeName} source, {destinationTypeName} destination)");
                    using (writer.Indent())
                    {
                        writer.WriteLine($"=> source.To{destinationSimpleName}(destination);");
                    }
                }
                else
                {
                    writer.WriteLine($"public static {destinationTypeName} {methodName}({sourceTypeName} source)");
                    using (writer.Indent())
                    {
                        writer.WriteLine($"=> source.To{destinationSimpleName}();");
                    }
                }

                writer.WriteLine();
            }
        }

        return true;
    }

    private static void EmitInterceptsLocationPolyfill(CodeWriter writer)
    {
        // The BCL never exposes InterceptsLocationAttribute publicly, on any TFM (confirmed
        // empirically - CS0246 even on a plain net10.0 console app) - the compiler recognizes it
        // purely by this namespace+name, so a `file`-scoped self-declared definition is both
        // necessary and safe (invisible outside this generated file, so it can never collide with
        // another generator's own polyfill of the same attribute in a different generated file).
        writer.WriteLine();
        using (writer.Block("namespace System.Runtime.CompilerServices"))
        {
            writer.WriteLine("[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]");
            using (writer.Block("file sealed class InterceptsLocationAttribute : Attribute"))
            {
                writer.WriteLine("public InterceptsLocationAttribute(int version, string data) { }");
            }
        }
    }
}
