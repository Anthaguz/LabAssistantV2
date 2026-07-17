using System.Collections.Immutable;
using System.Linq;
using System.Text;
using LabAssistant.Diagnostics.CodeGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace LabAssistant.Diagnostics.CodeGen.Tests;

/// <summary>
/// Drives <see cref="StatusCodeGenerator"/> directly against synthetic registry YAML to verify
/// the emit path and the registry validation (duplicate detection, malformed input) that guard
/// the append-only status-code contract.
/// </summary>
public sealed class StatusCodeGeneratorTests
{
    private const string ValidRegistry = """
        severities:
          - { value: 0x3, name: Info, level: info }
          - { value: 0x6, name: Error, level: error }
        flags:
          - { bit: 0x4, name: UserActionable }
        phases:
          - atomic
        facilities:
          - value: 0x41
            name: deploy.guest
            title: Deploy Guest
            operations:
              - { value: 0x01, name: bootstrap }
        codes:
          - { facility: 0x41, operation: 0x01, status: 0x00, severity: Info, phase: atomic, title: Bootstrap ready, message: "Guest bootstrap ready." }
          - { facility: 0x41, operation: 0x01, status: 0x03, severity: Error, flags: [ UserActionable ], phase: atomic, title: Bootstrap credential rejected, message: "Guest rejected the bootstrap credential." }
        """;

    [Fact]
    public void ValidRegistry_EmitsBothSourcesWithNoDiagnostics()
    {
        var result = Run(ValidRegistry);

        Assert.Empty(result.Diagnostics);
        var names = result.GeneratedTrees.Select(t => System.IO.Path.GetFileName(t.FilePath)).ToArray();
        Assert.Contains("LaStatus.g.cs", names);
        Assert.Contains("StatusCodeCatalog.Generated.g.cs", names);

        var laStatus = result.GeneratedTrees.Single(t => t.FilePath.EndsWith("LaStatus.g.cs")).ToString();
        Assert.Contains("0x30410100u", laStatus); // Info(0x3) facility 0x41 op 0x01 status 0x00
        Assert.Contains("0x64410103u", laStatus); // Error(0x6)+UserActionable(0x4) facility 0x41 op 0x01 status 0x03
    }

    [Fact]
    public void DuplicateFacilityValue_ReportsRegistryError()
    {
        var yaml = """
            severities:
              - { value: 0x3, name: Info, level: info }
            flags: []
            phases:
              - atomic
            facilities:
              - value: 0x41
                name: deploy.guest
                operations:
                  - { value: 0x01, name: bootstrap }
              - value: 0x41
                name: deploy.other
                operations:
                  - { value: 0x01, name: thing }
            codes:
              - { facility: 0x41, operation: 0x01, status: 0x00, severity: Info, phase: atomic, message: "ok" }
            """;

        var result = Run(yaml);

        var error = Assert.Single(result.Diagnostics);
        Assert.Equal("LASTATUS001", error.Id);
        Assert.Contains("defined more than once", error.GetMessage());
        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void DuplicateOperationValue_ReportsRegistryError()
    {
        var yaml = """
            severities:
              - { value: 0x3, name: Info, level: info }
            flags: []
            phases:
              - atomic
            facilities:
              - value: 0x41
                name: deploy.guest
                operations:
                  - { value: 0x01, name: bootstrap }
                  - { value: 0x01, name: other }
            codes:
              - { facility: 0x41, operation: 0x01, status: 0x00, severity: Info, phase: atomic, message: "ok" }
            """;

        var result = Run(yaml);

        Assert.Contains(result.Diagnostics, d => d.Id == "LASTATUS001" && d.GetMessage().Contains("defined more than once"));
        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public void DuplicateSeverityName_ReportsRegistryError()
    {
        var yaml = """
            severities:
              - { value: 0x3, name: Info, level: info }
              - { value: 0x4, name: Info, level: info }
            flags: []
            phases:
              - atomic
            facilities:
              - value: 0x41
                name: deploy.guest
                operations:
                  - { value: 0x01, name: bootstrap }
            codes:
              - { facility: 0x41, operation: 0x01, status: 0x00, severity: Info, phase: atomic, message: "ok" }
            """;

        var result = Run(yaml);

        Assert.Contains(result.Diagnostics, d => d.Id == "LASTATUS001" && d.GetMessage().Contains("defined more than once"));
    }

    [Fact]
    public void EmptyCodesSection_DoesNotThrowAndEmitsEmptyCatalog()
    {
        // A key present-but-empty deserializes to null under YamlDotNet; the generator must not NRE.
        var yaml = """
            severities:
              - { value: 0x3, name: Info, level: info }
            flags:
            phases:
              - atomic
            facilities:
              - value: 0x41
                name: deploy.guest
                operations:
                  - { value: 0x01, name: bootstrap }
            codes:
            """;

        var result = Run(yaml);

        Assert.Empty(result.Diagnostics);
        Assert.Contains(result.GeneratedTrees, t => t.FilePath.EndsWith("LaStatus.g.cs"));
    }

    [Fact]
    public void MultiLineTitle_ProducesCompilableGeneratedSource()
    {
        var yaml = """
            severities:
              - { value: 0x3, name: Info, level: info }
            flags: []
            phases:
              - atomic
            facilities:
              - value: 0x41
                name: deploy.guest
                operations:
                  - { value: 0x01, name: bootstrap }
            codes:
              - { facility: 0x41, operation: 0x01, status: 0x00, severity: Info, phase: atomic, title: "line one\nline two", message: "msg" }
            """;

        var result = Run(yaml);

        Assert.Empty(result.Diagnostics);
        var laStatus = result.GeneratedTrees.Single(t => t.FilePath.EndsWith("LaStatus.g.cs"));

        // The doc-comment must stay on a single line so the generated C# still compiles.
        var compilation = CSharpCompilation.Create(
            "gen-check",
            new[] { laStatus },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.Empty(errors);
    }

    private static GeneratorDriverRunResult Run(string yaml)
    {
        var compilation = CSharpCompilation.Create(
            "test-compilation",
            System.Array.Empty<SyntaxTree>(),
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var additionalText = new InMemoryAdditionalText("status-codes.yaml", yaml);
        var driver = CSharpGeneratorDriver.Create(
            generators: new[] { new StatusCodeGenerator().AsSourceGenerator() },
            additionalTexts: new[] { (AdditionalText)additionalText });

        return driver.RunGenerators(compilation).GetRunResult();
    }

    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText _text;

        public InMemoryAdditionalText(string path, string content)
        {
            Path = path;
            _text = SourceText.From(content, Encoding.UTF8);
        }

        public override string Path { get; }

        public override SourceText GetText(System.Threading.CancellationToken cancellationToken = default) => _text;
    }
}
