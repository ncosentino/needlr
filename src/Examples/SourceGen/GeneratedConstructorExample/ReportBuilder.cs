using NexusLabs.Needlr.Generators;

namespace GeneratedConstructorExample;

/// <summary>
/// DEMO 9 -- <c>[GenerateFactory]</c> composed with a generated constructor.
/// <c>_repository</c> is injectable and <c>_templateName</c> is a runtime
/// parameter, so <c>ReportBuilder</c> is excluded from Needlr's automatic
/// registration (it has a non-injectable parameter) but is fully supported by
/// <c>[GenerateFactory]</c>: the generated factory's
/// <c>Create(string templateName)</c> resolves <c>_repository</c> from the
/// container and forwards <c>templateName</c> straight through to the
/// generated constructor.
/// </summary>
[GenerateFactory]
[GenerateConstructor]
public partial class ReportBuilder
{
    private readonly IUserRepository _repository;
    private readonly string _templateName;

    /// <summary>The repository supplied to the generated constructor.</summary>
    public IUserRepository Repository => _repository;

    /// <summary>The runtime template name supplied to the generated constructor.</summary>
    public string TemplateName => _templateName;

    /// <summary>Builds a small report string using the injected repository and runtime template name.</summary>
    public string Build() => $"[{_templateName}] built by {_repository.Describe()}";
}
