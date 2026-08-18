using System;
using System.Collections.Generic;
using System.Linq;

namespace GeneratedMapper.Generator;

// The full set of every [MapTo] declaration discovered across the whole compilation, keyed by
// (source, destination) fully-qualified type name pair. MappingResolver needs this - not just
// the single declaration it's currently resolving - because a property's nested or element
// type might be mapped by a completely different [MapTo] declared on a different type; without
// the full graph there'd be no way to tell "this nested property's type happens to also be
// mappable" from "it isn't". Built once per generator run in MappingSourceGenerator.Initialize,
// after every file's declarations have been discovered.
internal sealed class MappingGraph
{
    private readonly Dictionary<(string Source, string Destination), MappingDeclaration> _mappings = new();

    public void Add(MappingDeclaration declaration)
    {
        _mappings[(declaration.Source.FullyQualifiedName, declaration.Destination.FullyQualifiedName)] = declaration;

        if (!declaration.GenerateReverse)
            return;

        // Conditions and converters are not auto-reversed: their method signatures are tied
        // to the original source type, which wouldn't type-check against the swapped source
        // type here. A reverse mapping needs its own explicit [MapCondition]/[MapUsing].
        var reverse = new MappingDeclaration(
            declaration.Destination,
            declaration.Source,
            declaration.DestinationSymbol,
            declaration.SourceSymbol,
            false,
            declaration.ExplicitProperties
                .Select(x => new ExplicitPropertyMapping(
                    x.DestinationProperty,
                    x.SourceProperty))
                .ToArray(),
            Array.Empty<ExplicitConditionMapping>(),
            Array.Empty<ExplicitConverterMapping>(),
            declaration.MaxDepth);

        _mappings[(reverse.Source.FullyQualifiedName, reverse.Destination.FullyQualifiedName)] = reverse;
    }

    public bool TryGetMapping(
        string source,
        string destination,
        out MappingDeclaration declaration)
        => _mappings.TryGetValue((source, destination), out declaration!);

    public IReadOnlyCollection<MappingDeclaration> GetMappings()
        => _mappings.Values;
}
