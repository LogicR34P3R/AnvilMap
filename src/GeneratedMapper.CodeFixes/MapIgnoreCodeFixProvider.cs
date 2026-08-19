using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace GeneratedMapper.CodeFixes;

// GM001 (destination property with no matching source) - offers to add [MapIgnore] directly
// above the property, silencing the diagnostic by opting the property out of mapping.
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MapIgnoreCodeFixProvider)), Shared]
public sealed class MapIgnoreCodeFixProvider : CodeFixProvider
{
    private const string DiagnosticId = "GM001";

    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DiagnosticId);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        var diagnostic = context.Diagnostics[0];
        var property = root.FindNode(diagnostic.Location.SourceSpan)
            .FirstAncestorOrSelf<PropertyDeclarationSyntax>();

        if (property is null)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add [MapIgnore]",
                createChangedDocument: ct => AddMapIgnoreAsync(context.Document, property, ct),
                equivalenceKey: DiagnosticId + "_AddMapIgnore"),
            diagnostic);
    }

    private static async Task<Document> AddMapIgnoreAsync(Document document, PropertyDeclarationSyntax property, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);

        var attribute = SyntaxFactory.Attribute(SyntaxFactory.ParseName("global::GeneratedMapper.MapIgnoreAttribute"));
        var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute))
            .WithAdditionalAnnotations(Formatter.Annotation);

        var newProperty = property.WithAttributeLists(property.AttributeLists.Insert(0, attributeList));
        var newRoot = root!.ReplaceNode(property, newProperty);
        var newDocument = document.WithSyntaxRoot(newRoot);

        return await Formatter.FormatAsync(newDocument, Formatter.Annotation, cancellationToken: ct).ConfigureAwait(false);
    }
}
