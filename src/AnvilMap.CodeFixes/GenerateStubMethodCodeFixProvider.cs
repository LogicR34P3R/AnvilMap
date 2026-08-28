using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace AnvilMap.CodeFixes;

// AM004 ([MapCondition] method not found) and AM009 ([MapUsing] method not found) - both name
// a static method that doesn't exist yet. The stub is inserted onto the method-host type (the
// type carrying [MapTo]/[MapFrom] and its companion attributes - the source type for a
// [MapTo]-declared mapping, but possibly the destination type for a [MapFrom]-declared one),
// which is usually a different file than the one the diagnostic is reported against (the
// destination property) - but the stub's required first parameter is always the source type,
// even when the stub itself lands on the destination. Both types are located via the
// "MethodHostMetadataName"/"SourceMetadataName"/"MethodName"/"ReturnType" diagnostic
// properties set in MappingResolver, not by parsing the message text.
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(GenerateStubMethodCodeFixProvider)), Shared]
public sealed class GenerateStubMethodCodeFixProvider : CodeFixProvider
{
    private const string ConditionDiagnosticId = "AM004";
    private const string ConverterDiagnosticId = "AM009";

    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(ConditionDiagnosticId, ConverterDiagnosticId);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics[0];

        if (!diagnostic.Properties.TryGetValue("MethodHostMetadataName", out var methodHostMetadataName) || methodHostMetadataName is null ||
            !diagnostic.Properties.TryGetValue("SourceMetadataName", out var sourceMetadataName) || sourceMetadataName is null ||
            !diagnostic.Properties.TryGetValue("MethodName", out var methodName) || methodName is null)
        {
            return Task.CompletedTask;
        }

        var returnType = diagnostic.Id == ConditionDiagnosticId
            ? "bool"
            : diagnostic.Properties.TryGetValue("ReturnType", out var rt) ? rt : null;

        if (returnType is null)
        {
            return Task.CompletedTask;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Generate stub method '{methodName}'",
                createChangedSolution: ct => AddStubMethodAsync(context.Document, methodHostMetadataName, sourceMetadataName, methodName, returnType, ct),
                equivalenceKey: diagnostic.Id + "_GenerateStubMethod"),
            diagnostic);

        return Task.CompletedTask;
    }

    private static async Task<Solution> AddStubMethodAsync(
        Document document, string methodHostMetadataName, string sourceMetadataName, string methodName, string returnType, CancellationToken ct)
    {
        var solution = document.Project.Solution;
        var compilation = await document.Project.GetCompilationAsync(ct).ConfigureAwait(false);
        var methodHostType = compilation?.GetTypeByMetadataName(methodHostMetadataName);
        var sourceType = compilation?.GetTypeByMetadataName(sourceMetadataName);
        var syntaxRef = methodHostType?.DeclaringSyntaxReferences.FirstOrDefault();

        if (syntaxRef is null || sourceType is null)
        {
            return solution;
        }

        var hostDocument = solution.GetDocument(syntaxRef.SyntaxTree);
        if (hostDocument is null)
        {
            return solution;
        }

        var root = await hostDocument.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        var typeDecl = (TypeDeclarationSyntax)await syntaxRef.GetSyntaxAsync(ct).ConfigureAwait(false);

        var parameterType = SyntaxFactory.ParseTypeName(sourceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        var method = SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName(returnType), methodName)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword))
            .AddParameterListParameters(SyntaxFactory.Parameter(SyntaxFactory.Identifier("source")).WithType(parameterType))
            .WithBody(SyntaxFactory.Block(
                SyntaxFactory.ParseStatement("throw new global::System.NotImplementedException();")))
            .WithAdditionalAnnotations(Formatter.Annotation);

        var newRoot = root!.ReplaceNode(typeDecl, typeDecl.AddMembers(method));
        var newDocument = hostDocument.WithSyntaxRoot(newRoot);
        var formattedDocument = await Formatter.FormatAsync(newDocument, Formatter.Annotation, cancellationToken: ct).ConfigureAwait(false);

        return formattedDocument.Project.Solution;
    }
}
