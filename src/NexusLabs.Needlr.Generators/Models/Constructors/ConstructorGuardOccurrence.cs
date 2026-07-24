using Microsoft.CodeAnalysis;

namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Analyzer-only information for one constructor guard occurrence on a field or
/// property.
/// </summary>
internal readonly struct ConstructorGuardOccurrence
{
    public ConstructorGuardOccurrence(
        ISymbol member,
        ITypeSymbol memberType,
        string memberKind,
        AttributeData attribute,
        ConstructorGuardOccurrenceKind kind,
        string? ineligibilityReason,
        ITypeSymbol? guardType,
        string? methodName,
        bool methodNameExplicit,
        bool guardTypeUsageIsInSourceAlias)
    {
        Member = member;
        MemberType = memberType;
        MemberKind = memberKind;
        Attribute = attribute;
        Kind = kind;
        IneligibilityReason = ineligibilityReason;
        GuardType = guardType;
        MethodName = methodName;
        MethodNameExplicit = methodNameExplicit;
        GuardTypeUsageIsInSourceAlias = guardTypeUsageIsInSourceAlias;
    }

    public ISymbol Member { get; }

    public ITypeSymbol MemberType { get; }

    public string MemberKind { get; }

    public AttributeData Attribute { get; }

    public ConstructorGuardOccurrenceKind Kind { get; }

    public string? IneligibilityReason { get; }

    public ITypeSymbol? GuardType { get; }

    public string? MethodName { get; }

    public bool MethodNameExplicit { get; }

    public bool GuardTypeUsageIsInSourceAlias { get; }
}
