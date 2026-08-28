using AnvilMap.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;

namespace AnvilMap.CodeFixes.Tests;

// Drives the real MappingSourceGenerator over an AdhocWorkspace project (rather than a bare
// Compilation, as AnvilMap.Generator.Tests does) so diagnostics carry real Locations and
// Properties tied to actual Documents - what a CodeFixProvider needs to operate on.
internal static class CodeFixTestHelper
{
    private static readonly MetadataReference[] PlatformReferences =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToArray();

    // sources: one file per entry, named Source0.cs, Source1.cs, etc.
    public static async Task<Solution> ApplyFixAsync(CodeFixProvider provider, string diagnosticId, params string[] sources)
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();

        var references = PlatformReferences
            .Append(MetadataReference.CreateFromFile(typeof(MapToAttribute).Assembly.Location))
            .ToArray();

        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "Test",
            "Test",
            LanguageNames.CSharp,
            compilationOptions: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable),
            parseOptions: new CSharpParseOptions(LanguageVersion.Latest),
            metadataReferences: references);

        var solution = workspace.CurrentSolution.AddProject(projectInfo);

        for (var i = 0; i < sources.Length; i++)
        {
            var documentId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(documentId, $"Source{i}.cs", sources[i]);
        }

        var project = solution.GetProject(projectId)!;
        var compilation = (CSharpCompilation)(await project.GetCompilationAsync().ConfigureAwait(false))!;

        var treeToDocument = new Dictionary<SyntaxTree, Document>();
        foreach (var document in project.Documents)
        {
            var tree = await document.GetSyntaxTreeAsync().ConfigureAwait(false);
            treeToDocument[tree!] = document;
        }

        var driver = CSharpGeneratorDriver.Create(new[] { new MappingSourceGenerator().AsSourceGenerator() })
            .RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        var runResult = driver.GetRunResult();

        var diagnostic = runResult.Diagnostics.FirstOrDefault(d => d.Id == diagnosticId)
            ?? throw new InvalidOperationException(
                $"No {diagnosticId} diagnostic was reported. Reported: {string.Join(", ", runResult.Diagnostics.Select(d => d.Id))}");

        if (!treeToDocument.TryGetValue(diagnostic.Location.SourceTree!, out var diagnosticDocument))
        {
            throw new InvalidOperationException($"{diagnosticId}'s location doesn't belong to any test document.");
        }

        CodeAction? registeredAction = null;
        var context = new CodeFixContext(
            diagnosticDocument,
            diagnostic,
            (action, _) => registeredAction ??= action,
            CancellationToken.None);

        await provider.RegisterCodeFixesAsync(context).ConfigureAwait(false);

        if (registeredAction is null)
        {
            throw new InvalidOperationException($"{provider.GetType().Name} did not register a fix for {diagnosticId}.");
        }

        var operations = await registeredAction.GetOperationsAsync(CancellationToken.None).ConfigureAwait(false);
        var applyOperation = operations.OfType<ApplyChangesOperation>().Single();
        applyOperation.Apply(workspace, CancellationToken.None);

        return workspace.CurrentSolution;
    }

    public static async Task<string> GetDocumentTextAsync(Solution solution, string fileName)
    {
        var document = solution.Projects.Single().Documents.Single(d => d.Name == fileName);
        var text = await document.GetTextAsync().ConfigureAwait(false);
        return text.ToString();
    }
}
