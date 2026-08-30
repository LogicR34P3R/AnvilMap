using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace AnvilMap.Generator;

// Every [MapTo]/[MapFrom] declaration, keyed by (source, destination) type name. MappingResolver
// needs the full graph since a nested/element type may be mapped by a separate declaration
// elsewhere.
internal sealed class MappingGraph
{
    private readonly Dictionary<(string Source, string Destination), MappingDeclaration> _mappings = new();

    public void Add(MappingDeclaration declaration, Action<Diagnostic>? report = null)
    {
        Insert(declaration, report);

        if (!declaration.GenerateReverse)
        {
            return;
        }

        if (declaration.ExplicitIncludes is { Count: > 0 })
        {
            report?.Invoke(Diagnostic.Create(
                Diagnostics.GenerateReverseWithMapIncludeUnsupported,
                declaration.MethodHostSymbol.Locations.FirstOrDefault() ?? Location.None,
                declaration.Source.DisplayName,
                declaration.Destination.DisplayName));
            return;
        }

        // Not routed back through Add() - GenerateReverse itself is never true on a synthesized
        // reverse (see MappingDeclaration.ToReverse), so this can't recurse.
        Insert(declaration.ToReverse(), report);
    }

    // Last-write-wins on a key collision (unchanged behavior), but now surfaced as AM011 -
    // e.g. the same pair declared via both [MapTo] and [MapFrom], two [MapTo]s to the same
    // destination, or a [GenerateReverse]-implied pair colliding with an explicit declaration.
    private void Insert(MappingDeclaration declaration, Action<Diagnostic>? report)
    {
        var key = (declaration.Source.FullyQualifiedName, declaration.Destination.FullyQualifiedName);

        if (_mappings.ContainsKey(key))
        {
            report?.Invoke(Diagnostic.Create(
                Diagnostics.DuplicateMappingDeclaration,
                declaration.MethodHostSymbol.Locations.FirstOrDefault() ?? Location.None,
                declaration.Source.DisplayName,
                declaration.Destination.DisplayName));
        }

        _mappings[key] = declaration;
    }

    public bool TryGetMapping(
        string source,
        string destination,
        out MappingDeclaration declaration)
        => _mappings.TryGetValue((source, destination), out declaration!);

    public IReadOnlyCollection<MappingDeclaration> GetMappings()
        => _mappings.Values;
}
