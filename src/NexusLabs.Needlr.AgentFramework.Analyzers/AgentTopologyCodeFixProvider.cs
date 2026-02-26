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

namespace NexusLabs.Needlr.AgentFramework.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AgentTopologyCodeFixProvider))]
[Shared]
public sealed class AgentTopologyCodeFixProvider : CodeFixProvider
{
    private const string Title = "Add [NeedlrAiAgent]";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(
            MafDiagnosticIds.HandoffsToTargetNotNeedlrAgent,
            MafDiagnosticIds.HandoffsToSourceNotNeedlrAgent);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        foreach (var diagnostic in context.Diagnostics)
        {
            if (diagnostic.Id == MafDiagnosticIds.HandoffsToSourceNotNeedlrAgent)
                RegisterSourceFix(context, root, diagnostic);
            else if (diagnostic.Id == MafDiagnosticIds.HandoffsToTargetNotNeedlrAgent)
                await RegisterTargetFixAsync(context, root, diagnostic).ConfigureAwait(false);
        }
    }

    private static void RegisterSourceFix(CodeFixContext context, SyntaxNode root, Diagnostic diagnostic)
    {
        var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
        var classDeclaration = token.Parent?.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (classDeclaration is null)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedDocument: ct => AddAttributeToDocumentAsync(context.Document, classDeclaration, ct),
                equivalenceKey: Title + MafDiagnosticIds.HandoffsToSourceNotNeedlrAgent),
            diagnostic);
    }

    private static async Task RegisterTargetFixAsync(CodeFixContext context, SyntaxNode root, Diagnostic diagnostic)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        var attributeSyntax = node.AncestorsAndSelf().OfType<AttributeSyntax>().FirstOrDefault();
        if (attributeSyntax is null)
            return;

        var typeOfExpr = attributeSyntax.ArgumentList?.Arguments
            .Select(a => a.Expression)
            .OfType<TypeOfExpressionSyntax>()
            .FirstOrDefault();
        if (typeOfExpr is null)
            return;

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
            return;

        var targetType = semanticModel.GetTypeInfo(typeOfExpr.Type, context.CancellationToken).Type as INamedTypeSymbol;
        if (targetType is null)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedSolution: ct => AddAttributeToTargetAsync(
                    context.Document.Project.Solution, targetType, ct),
                equivalenceKey: Title + MafDiagnosticIds.HandoffsToTargetNotNeedlrAgent),
            diagnostic);
    }

    private static async Task<Document> AddAttributeToDocumentAsync(
        Document document,
        ClassDeclarationSyntax classDeclaration,
        CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
            return document;

        var updated = PrependNeedlrAiAgent(classDeclaration);
        return document.WithSyntaxRoot(root.ReplaceNode(classDeclaration, updated));
    }

    private static async Task<Solution> AddAttributeToTargetAsync(
        Solution solution,
        INamedTypeSymbol targetType,
        CancellationToken ct)
    {
        var syntaxRef = targetType.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef is null)
            return solution;

        var syntax = await syntaxRef.GetSyntaxAsync(ct).ConfigureAwait(false);
        if (syntax is not ClassDeclarationSyntax classDeclaration)
            return solution;

        var documentId = solution.GetDocumentId(syntaxRef.SyntaxTree);
        if (documentId is null)
            return solution;

        var document = solution.GetDocument(documentId);
        if (document is null)
            return solution;

        var updated = await AddAttributeToDocumentAsync(document, classDeclaration, ct).ConfigureAwait(false);
        return updated.Project.Solution;
    }

    private static ClassDeclarationSyntax PrependNeedlrAiAgent(ClassDeclarationSyntax classDeclaration)
    {
        var attribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("NeedlrAiAgent"));
        var firstToken = classDeclaration.GetFirstToken();
        var leadingTrivia = firstToken.LeadingTrivia;

        var endOfLine = leadingTrivia.FirstOrDefault(t => t.IsKind(SyntaxKind.EndOfLineTrivia));
        if (endOfLine == default)
            endOfLine = SyntaxFactory.LineFeed;

        var attrList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute))
            .WithLeadingTrivia(leadingTrivia)
            .WithTrailingTrivia(SyntaxFactory.TriviaList(endOfLine));

        var stripped = classDeclaration.ReplaceToken(
            firstToken,
            firstToken.WithLeadingTrivia(SyntaxFactory.TriviaList()));

        return stripped.WithAttributeLists(stripped.AttributeLists.Insert(0, attrList));
    }
}
