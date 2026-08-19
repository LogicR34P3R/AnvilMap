using System.Collections.Generic;

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

        var reverse = declaration.ToReverse();
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
