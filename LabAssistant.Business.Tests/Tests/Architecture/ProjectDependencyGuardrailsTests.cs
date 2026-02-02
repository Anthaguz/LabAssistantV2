using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace LabAssistant.Business.Tests.Tests.Architecture;

public class ProjectDependencyGuardrailsTests
{
    [Fact]
    public void ProjectsFollowDependencyRules()
    {
        var repoRoot = FindRepoRoot();
        var projects = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["UI"] = Path.Combine(repoRoot, "LabAssistant", "LabAssistant.csproj"),
            ["Business"] = Path.Combine(repoRoot, "LabAssistant.Business", "LabAssistant.Business.csproj"),
            ["Data"] = Path.Combine(repoRoot, "LabAssistant.Data", "LabAssistant.Data.csproj"),
            ["Services"] = Path.Combine(repoRoot, "LabAssistant.Services", "LabAssistant.Services.csproj"),
            ["Models"] = Path.Combine(repoRoot, "LabAssistant.Models", "LabAssistant.Models.csproj"),
            ["Business.Tests"] = Path.Combine(repoRoot, "LabAssistant.Business.Tests", "LabAssistant.Business.Tests.csproj")
        };

        var allowedRefs = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Business"] = new[] { projects["Data"], projects["Models"], projects["Services"] },
            ["Data"] = new[] { projects["Models"] },
            ["Services"] = new[] { projects["Models"] },
            ["Models"] = Array.Empty<string>(),
            ["Business.Tests"] = new[] { projects["Business"], projects["Models"], projects["Services"], projects["Data"] }
        };

        foreach (var (name, projectPath) in projects)
        {
            if (!allowedRefs.TryGetValue(name, out var allowed))
            {
                continue; // skip UI or untracked projects
            }

            var references = GetProjectReferences(projectPath);
            foreach (var reference in references)
            {
                Assert.Contains(reference, allowed);
            }
        }
    }

    [Fact]
    public void NonUiProjectsMustNotReferenceUi()
    {
        var repoRoot = FindRepoRoot();
        var uiProject = Path.Combine(repoRoot, "LabAssistant", "LabAssistant.csproj");
        var otherProjects = new[]
        {
            Path.Combine(repoRoot, "LabAssistant.Business", "LabAssistant.Business.csproj"),
            Path.Combine(repoRoot, "LabAssistant.Data", "LabAssistant.Data.csproj"),
            Path.Combine(repoRoot, "LabAssistant.Services", "LabAssistant.Services.csproj"),
            Path.Combine(repoRoot, "LabAssistant.Models", "LabAssistant.Models.csproj"),
            Path.Combine(repoRoot, "LabAssistant.Business.Tests", "LabAssistant.Business.Tests.csproj")
        };

        foreach (var project in otherProjects)
        {
            var references = GetProjectReferences(project);
            Assert.DoesNotContain(uiProject, references);
        }
    }

    [Fact]
    public void ModelsMustNotReferenceAnyProject()
    {
        var repoRoot = FindRepoRoot();
        var modelsProject = Path.Combine(repoRoot, "LabAssistant.Models", "LabAssistant.Models.csproj");
        var references = GetProjectReferences(modelsProject);
        Assert.Empty(references);
    }

    private static IReadOnlyCollection<string> GetProjectReferences(string csprojPath)
    {
        var document = XDocument.Load(csprojPath);
        var projectDirectory = Path.GetDirectoryName(csprojPath) ?? string.Empty;
        var references = document
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Path.GetFullPath(Path.Combine(projectDirectory, value!)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return references;
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "LabAssistant.sln")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate LabAssistant.sln from test directory.");
    }
}
