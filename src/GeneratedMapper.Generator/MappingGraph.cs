using System;
using System.Collections.Generic;
using System.Linq;

namespace GeneratedMapper.Generator;

// Every [MapTo] declaration, keyed by (source, destination) type name. MappingResolver needs
// the full graph since a nested/element type may be mapped by a separate [MapTo] elsewhere.
internal sealed class MappingGraph
{
    private readonly Dictionary<(string Source, string Destination), MappingDeclaration> _mappings = new();

    public void Add(MappingDeclaration declaration)
    {
        _mappings[(declaration.Source.FullyQualifiedName, declaration.Destination.FullyQualifiedName)] = declaration;

        if (!declaration.GenerateReverse)
            return;

        // Conditions/converters aren't auto-reversed - their methods are tied to the original
        // source type. A reverse mapping needs its own [MapCondition]/[MapUsing].
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
