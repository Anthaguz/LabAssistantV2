using LabAssistant.UITesting.Scenarios;
using Xunit;

namespace LabAssistant.UITesting.Tests;

/// <summary>
/// Behaviour tests for the host-side orphan-disk enumeration used by the forest-trust rollback verb's
/// zero-orphans gate (finding 89). These pin the fix for a self-contradictory gate: the run-tagged disk
/// enumeration used to include the harness-SEEDED shared base image (LAT-&lt;runId&gt;-base.vhdx), which is
/// provision-created (not a runtime residual) and is asserted to SURVIVE separately (baseImageIntact). Counting
/// it as an orphan made a CORRECT rollback - where the base correctly survives - always false-fail as a 1-orphan
/// leak. <see cref="TemplateDeployForestTrustRollbackScenario.FindOrphanDisks"/> must return only genuine
/// runtime-created leftovers, never the seeded base. The tests run against a real temp directory (no app, no
/// Hyper-V), mirroring the on-disk layout the probe walks.
/// </summary>
public sealed class ForestTrustRollbackOrphanProbeTests
{
    private const string GlobalPrefix = "LAT-20260729-052901-";

    private static string NewTempRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "la-orphan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string Seed(string root, string fileName)
    {
        string path = Path.Combine(root, fileName);
        File.WriteAllText(path, "vhdx");
        return path;
    }

    [Fact]
    public void Seeded_base_disk_alone_is_not_an_orphan()
    {
        string root = NewTempRoot();
        try
        {
            // A clean teardown: the ONLY tagged file left under the disk root is the seeded base image, which
            // MUST survive. The gate must see zero run-created orphans here (the finding-89 false-fail).
            string basePath = Seed(root, GlobalPrefix + "base.vhdx");

            var orphans = TemplateDeployForestTrustRollbackScenario.FindOrphanDisks(
                new[] { root }, GlobalPrefix, basePath);

            Assert.Empty(orphans);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Genuine_runtime_leftover_is_still_reported_even_with_base_present()
    {
        string root = NewTempRoot();
        try
        {
            // The base survives (excluded) but a run-created differencing disk leaked (a REAL orphan) - it must
            // still be reported so the gate can never be blinded to an actual leak by the base-exclusion.
            string basePath = Seed(root, GlobalPrefix + "base.vhdx");
            string leftover = Seed(root, GlobalPrefix + "SourceDc.vhdx");

            var orphans = TemplateDeployForestTrustRollbackScenario.FindOrphanDisks(
                new[] { root }, GlobalPrefix, basePath);

            Assert.Equal(new[] { leftover }, orphans);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Base_exclusion_matches_case_insensitively_by_full_path()
    {
        string root = NewTempRoot();
        try
        {
            // The runtime/harness may hand the base path with different casing or a non-normalized form; the
            // exclusion is by normalized full path, case-insensitive (Windows), so it still excludes the base.
            string basePath = Seed(root, GlobalPrefix + "base.vhdx");
            string mixedCase = basePath.ToUpperInvariant();

            var orphans = TemplateDeployForestTrustRollbackScenario.FindOrphanDisks(
                new[] { root }, GlobalPrefix, mixedCase);

            Assert.Empty(orphans);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Null_or_empty_base_path_excludes_nothing()
    {
        string root = NewTempRoot();
        try
        {
            // Defensive: with no base path to protect, every tagged file is a candidate orphan (the guard must
            // not silently swallow leftovers). Documents that the exclusion is opt-in on a real base path.
            string tagged = Seed(root, GlobalPrefix + "base.vhdx");

            var withNull = TemplateDeployForestTrustRollbackScenario.FindOrphanDisks(new[] { root }, GlobalPrefix, null);
            var withEmpty = TemplateDeployForestTrustRollbackScenario.FindOrphanDisks(new[] { root }, GlobalPrefix, string.Empty);

            Assert.Equal(new[] { tagged }, withNull);
            Assert.Equal(new[] { tagged }, withEmpty);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Untagged_files_and_missing_roots_are_ignored()
    {
        string root = NewTempRoot();
        try
        {
            // A file NOT carrying the run prefix belongs to another run/tenant and is never this run's orphan;
            // a non-existent root is skipped without throwing.
            string basePath = Seed(root, GlobalPrefix + "base.vhdx");
            Seed(root, "OTHER-run-disk.vhdx");
            string missingRoot = Path.Combine(root, "does-not-exist");

            var orphans = TemplateDeployForestTrustRollbackScenario.FindOrphanDisks(
                new[] { root, missingRoot }, GlobalPrefix, basePath);

            Assert.Empty(orphans);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
