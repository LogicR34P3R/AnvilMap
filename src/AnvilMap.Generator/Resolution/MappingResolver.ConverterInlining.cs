using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AnvilMap.Generator;

// [MapUsing] InlineInProjection: splices an eligible converter's body into a projection
// expression instead of calling it. Only a single-expression body qualifies; everything else
// (or anything this rewriter can't safely qualify/substitute) falls back to a plain call.
internal static partial class MappingResolver
{
    // Replaced with the real call-site expression once known, in MappingEmitter.Projection.cs.
    internal const string InlineConverterSourcePlaceholder = "__AnvilMapInlineConverterSource__";

    private static string? TryInlineConverterForProjection(
        Compilation compilation,
        IMethodSymbol converterMethod,
        IPropertySymbol destinationProperty,
        INamedTypeSymbol source,
        INamedTypeSymbol destination,
        Action<Diagnostic>? report)
    {
        var methodSyntax = converterMethod.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax())
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.ExpressionBody is not null || m.Body is not null);

        if (methodSyntax is null)
        {
            ReportIneligible(destinationProperty, converterMethod, source, destination,
                "its declaration has no available source to inline (e.g. it's declared in a referenced assembly, or is a partial method with no implementing body)",
                report);
            return null;
        }

        var bodyExpression = methodSyntax.ExpressionBody?.Expression;

        if (bodyExpression is null && methodSyntax.Body is { } block && block.Statements.Count == 1 &&
            block.Statements[0] is ReturnStatementSyntax { Expression: { } returnExpression })
        {
            bodyExpression = returnExpression;
        }

        if (bodyExpression is null)
        {
            ReportIneligible(destinationProperty, converterMethod, source, destination,
                "its body isn't a single expression (an expression-bodied member, or a block with exactly one return statement is required)",
                report);
            return null;
        }

        var semanticModel = compilation.GetSemanticModel(methodSyntax.SyntaxTree);
        var rewriter = new ParameterInliningRewriter(semanticModel, converterMethod.Parameters[0]);
        var rewritten = rewriter.Visit(bodyExpression) ?? bodyExpression;

        if (rewriter.FailureReason is { } reason)
        {
            ReportIneligible(destinationProperty, converterMethod, source, destination, reason, report);
            return null;
        }

        return rewritten.ToString();
    }

    private static void ReportIneligible(
        IPropertySymbol destinationProperty,
        IMethodSymbol converterMethod,
        INamedTypeSymbol source,
        INamedTypeSymbol destination,
        string reason,
        Action<Diagnostic>? report)
    {
        report?.Invoke(Diagnostic.Create(
            Diagnostics.MapUsingInlineNotEligible,
            destinationProperty.Locations.FirstOrDefault() ?? Location.None,
            destinationProperty.Name,
            $"{source.ToDisplayString()}.{converterMethod.Name}",
            reason));
    }

    // Converter methods are always static, so there's no implicit `this` receiver to handle.
    private sealed class ParameterInliningRewriter : CSharpSyntaxRewriter
    {
        private readonly SemanticModel _semanticModel;
        private readonly IParameterSymbol _parameter;

        public string? FailureReason { get; private set; }

        public ParameterInliningRewriter(SemanticModel semanticModel, IParameterSymbol parameter)
        {
            _semanticModel = semanticModel;
            _parameter = parameter;
        }

        public override SyntaxNode? VisitGenericName(GenericNameSyntax node)
        {
            FailureReason ??= "its body references a generic type or method, which inlining doesn't support yet";
            return node;
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (FailureReason is not null)
            {
                return node;
            }

            // Right-hand side of a dot/named-argument name - not a symbol lookup root.
            if (IsNonRootNamePosition(node))
            {
                return node;
            }

            // nameof(x.Y) needs a simple designator, not an arbitrary substituted expression.
            if (IsInsideNameofArgument(node))
            {
                FailureReason = "its body contains a nameof(...) expression, which can't be safely inlined";
                return node;
            }

            var symbolInfo = _semanticModel.GetSymbolInfo(node);
            var symbol = symbolInfo.Symbol
                ?? (symbolInfo.CandidateSymbols.Length == 1 ? symbolInfo.CandidateSymbols[0] : null);

            if (symbol is null)
            {
                FailureReason = $"a reference to '{node.Identifier.Text}' in its body couldn't be resolved to exactly one symbol";
                return node;
            }

            // Bare identifier standing in for another bare identifier - no parens needed here;
            // MappingEmitter.Projection.cs wraps the real substituted expression once, later.
            if (SymbolEqualityComparer.Default.Equals(symbol, _parameter))
            {
                return SyntaxFactory.IdentifierName(InlineConverterSourcePlaceholder).WithTriviaFrom(node);
            }

            switch (symbol)
            {
                // Self-contained wherever the expression is spliced - needs no qualification.
                case ILocalSymbol:
                case IParameterSymbol:
                case IRangeVariableSymbol:
                case INamespaceSymbol:
                    return node;

                // Not addressable outside the method that declares it.
                case ITypeParameterSymbol:
                    FailureReason = $"its body references the type parameter '{symbol.Name}', which can't be inlined outside the converter method itself";
                    return node;

                default:
                    // Private/protected members aren't reachable from the generated file.
                    if (symbol.DeclaredAccessibility is Accessibility.Private or Accessibility.Protected or Accessibility.ProtectedAndInternal)
                    {
                        FailureReason = $"its body references '{symbol.Name}', which is {symbol.DeclaredAccessibility.ToString().ToLowerInvariant()} and wouldn't be accessible from the generated projection code";
                        return node;
                    }

                    // Fully qualify so the reference still resolves once spliced elsewhere.
                    // FullyQualifiedFormat doesn't prepend a containing type for a member symbol
                    // (only for a type symbol), so that part is added by hand below.
                    var qualified = symbol is INamedTypeSymbol namedType
                        ? namedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                        : symbol.ContainingType is { } containingType
                            ? $"{containingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{symbol.Name}"
                            : symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    return SyntaxFactory.ParseExpression(qualified).WithTriviaFrom(node);
            }
        }

        private static bool IsNonRootNamePosition(IdentifierNameSyntax node)
            => node.Parent switch
            {
                MemberAccessExpressionSyntax memberAccess => memberAccess.Name == node,
                QualifiedNameSyntax qualifiedName => qualifiedName.Right == node,
                MemberBindingExpressionSyntax memberBinding => memberBinding.Name == node,
                NameColonSyntax nameColon => nameColon.Name == node,
                NameEqualsSyntax nameEquals => nameEquals.Name == node,
                _ => false
            };

        private static bool IsInsideNameofArgument(SyntaxNode node)
        {
            for (var current = node.Parent; current is not null; current = current.Parent)
            {
                if (current is InvocationExpressionSyntax
                    {
                        Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" }
                    })
                {
                    return true;
                }
            }

            return false;
        }
    }
}
