using System.Text.Json;
using System.Text.Json.Nodes;
using LabAssistant.UITesting.Infrastructure.Cleanup;

namespace LabAssistant.UITesting.Infrastructure;

/// <summary>
/// Seeds a minimal, deployable V2 lab template onto disk in the app's Templates
/// folder (%APPDATA%\LabAssistant\Templates) so the From Template deploy flow has
/// something real to select and deploy. The harness is black-box: rather than
/// referencing the app's model types, it loads a captured V2 template fixture and
/// rewrites only the few fields that must be per-run - the template name, the VM
/// name, the base-disk catalog id, and the lab-network switch - then writes it
/// exactly as the app would read it.
///
/// Two things carry the run tag and are therefore sweepable: the VM name (which
/// becomes the deployed Hyper-V VM name, the hook the teardown sweeper matches)
/// and the template file name (matched by the same prefix). Everything the plan
/// needs to resolve without a bootable OS is baked into the fixture: a single
/// standalone Gen2 VM with a bare NIC (no static IP/gateway/DNS), no topology
/// role, no domain membership, no capability roles, and no credential slots, so
/// the V2 plan reports zero unresolved requirements.
/// </summary>
public sealed class TemplateSeeder
{
    private readonly AppDataLocations _appData;

    /// <summary>
    /// The local bootstrap credential-slot key authored into the DC fixture and referenced
    /// by the real base image's bootstrap profile. The DC scenario seeds the base-disk profile
    /// with this same key so the plan surfaces exactly one credential slot to resolve, which
    /// the planner then reuses for domain-admin and DSRM (credential-reuse policy).
    /// </summary>
    public const string DcLocalBootstrapSlotKey = "disk.winserver2022.local-admin";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public TemplateSeeder(AppDataLocations appData) => _appData = appData;

    /// <summary>
    /// Writes a tagged standalone V2 template that references the seeded base disk
    /// and switch, and returns its on-disk identity. The template and VM names both
    /// carry the run prefix so the file and the resulting VM are both sweepable.
    /// </summary>
    public SeededTemplate SeedStandaloneTemplate(ResourceTagger tagger, string baseDiskCatalogId, string switchName)
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Templates", "standalone-v2-template.json");
        if (!File.Exists(fixturePath))
        {
            throw new FileNotFoundException(
                $"Standalone V2 template fixture not found at '{fixturePath}'. Ensure Fixtures\\Templates\\standalone-v2-template.json is copied to output.");
        }

        var root = JsonNode.Parse(File.ReadAllText(fixturePath))?.AsObject()
            ?? throw new InvalidOperationException("Standalone V2 template fixture did not parse as a JSON object.");

        // The template file name carries the tag (sweep hook for the file); the id is a plain
        // GUID because template-file ownership is decided by the file name, not the id.
        var templateName = tagger.Name("tpl");
        var vmName = tagger.Name("vm");
        root["id"] = Guid.NewGuid().ToString("N");
        root["name"] = templateName;

        var vm = root["vmTemplates"]?.AsArray()?.FirstOrDefault()?.AsObject()
            ?? throw new InvalidOperationException("Standalone V2 template fixture is missing its first vmTemplates entry.");
        vm["vmId"] = Guid.NewGuid().ToString("N");
        vm["name"] = vmName;
        vm["vhdxId"] = baseDiskCatalogId;

        // The NIC names the existing host switch directly (no lab-network binding, no static
        // IP), so the switch attaches at provision time WITHOUT setting requiresGuestWork - the
        // proven bare-switch shape. If a networkId were used instead, the planner would demand a
        // bootstrap profile the seeded empty disk does not carry, and the deploy would never start.
        var nic = vm["nics"]?.AsArray()?.FirstOrDefault()?.AsObject()
            ?? throw new InvalidOperationException("Standalone V2 template fixture is missing its first NIC.");
        nic["switchName"] = switchName;

        Directory.CreateDirectory(_appData.TemplatesFolder);
        var filePath = Path.Combine(_appData.TemplatesFolder, templateName + ".json");
        File.WriteAllText(filePath, root.ToJsonString(JsonOptions));

        return new SeededTemplate(filePath, templateName, vmName);
    }

    /// <summary>
    /// Writes a tagged multi-VM V2 template that references the seeded base disk and
    /// switch, and returns its on-disk identity plus the per-VM ground truth. Every VM
    /// in the fixture is stamped with its own run-tagged name (so each deployed VM is
    /// sweepable) and its NIC names the host switch directly (the bare-switch shape, so
    /// no VM triggers guest work). The fixture's per-VM memory and cpu are left intact
    /// and read back into the returned list, so the scenario validates each deployed VM
    /// against the template's own values - the single source of truth - which proves the
    /// deploy applied each VM's OWN configuration rather than one shared config.
    /// </summary>
    public SeededMultiVmTemplate SeedMultiVmTemplate(ResourceTagger tagger, string baseDiskCatalogId, string switchName)
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Templates", "multivm-v2-template.json");
        if (!File.Exists(fixturePath))
        {
            throw new FileNotFoundException(
                $"Multi-VM V2 template fixture not found at '{fixturePath}'. Ensure Fixtures\\Templates\\multivm-v2-template.json is copied to output.");
        }

        var root = JsonNode.Parse(File.ReadAllText(fixturePath))?.AsObject()
            ?? throw new InvalidOperationException("Multi-VM V2 template fixture did not parse as a JSON object.");

        var templateName = tagger.Name("mtpl");
        root["id"] = Guid.NewGuid().ToString("N");
        root["name"] = templateName;

        var vmArray = root["vmTemplates"]?.AsArray()
            ?? throw new InvalidOperationException("Multi-VM V2 template fixture is missing its vmTemplates array.");
        if (vmArray.Count == 0)
        {
            throw new InvalidOperationException("Multi-VM V2 template fixture has no VM entries.");
        }

        var seededVms = new List<SeededVm>(vmArray.Count);
        for (int i = 0; i < vmArray.Count; i++)
        {
            var vm = vmArray[i]?.AsObject()
                ?? throw new InvalidOperationException($"Multi-VM V2 template fixture VM entry {i} is not an object.");

            // Each VM name carries the run prefix + an ordinal suffix so all of them are
            // sweepable and mutually distinct.
            var vmName = tagger.Name($"vm{i + 1}");
            vm["vmId"] = Guid.NewGuid().ToString("N");
            vm["name"] = vmName;
            vm["vhdxId"] = baseDiskCatalogId;

            var nic = vm["nics"]?.AsArray()?.FirstOrDefault()?.AsObject()
                ?? throw new InvalidOperationException($"Multi-VM V2 template fixture VM entry {i} is missing its first NIC.");
            nic["switchName"] = switchName;

            // Read the per-VM ground truth straight from the fixture we are about to write,
            // so validation compares against exactly what was deployed.
            int memoryMb = vm["memoryMb"]?.GetValue<int>()
                ?? throw new InvalidOperationException($"Multi-VM V2 template fixture VM entry {i} is missing memoryMb.");
            int cpuCount = vm["cpuCount"]?.GetValue<int>()
                ?? throw new InvalidOperationException($"Multi-VM V2 template fixture VM entry {i} is missing cpuCount.");

            seededVms.Add(new SeededVm(vmName, memoryMb, cpuCount));
        }

        Directory.CreateDirectory(_appData.TemplatesFolder);
        var filePath = Path.Combine(_appData.TemplatesFolder, templateName + ".json");
        File.WriteAllText(filePath, root.ToJsonString(JsonOptions));

        return new SeededMultiVmTemplate(filePath, templateName, seededVms);
    }

    /// <summary>
    /// Writes a tagged single first-domain-controller V2 template that references the REAL
    /// prepared base image (by catalog id) on the given switch, and returns its on-disk
    /// identity plus the forest ground truth. The DC VM name and the template file name both
    /// carry the run prefix so the deployed VM and the file are sweepable.
    ///
    /// Unlike the bare-switch scenarios this VM is a RootDomainController with a static-IP NIC,
    /// so the plan requires guest work (DC promotion) and therefore a bootstrap-capable base
    /// image plus a resolved local bootstrap credential slot. The fixture's directory topology
    /// (forest smoke.lab / SMOKE) and the VM's fixed vmId are stitched together here so
    /// firstDomainControllerVmId always matches the VM that promotes it.
    /// </summary>
    public SeededDcTemplate SeedDomainControllerTemplate(ResourceTagger tagger, string baseDiskCatalogId, string switchName)
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Templates", "dc-v2-template.json");
        if (!File.Exists(fixturePath))
        {
            throw new FileNotFoundException(
                $"DC V2 template fixture not found at '{fixturePath}'. Ensure Fixtures\\Templates\\dc-v2-template.json is copied to output.");
        }

        var root = JsonNode.Parse(File.ReadAllText(fixturePath))?.AsObject()
            ?? throw new InvalidOperationException("DC V2 template fixture did not parse as a JSON object.");

        var templateName = tagger.Name("dctpl");
        var vmName = tagger.Name("dc");
        var vmId = Guid.NewGuid().ToString("N");
        root["id"] = Guid.NewGuid().ToString("N");
        root["name"] = templateName;

        var vm = root["vmTemplates"]?.AsArray()?.FirstOrDefault()?.AsObject()
            ?? throw new InvalidOperationException("DC V2 template fixture is missing its first vmTemplates entry.");
        vm["vmId"] = vmId;
        vm["name"] = vmName;
        vm["vhdxId"] = baseDiskCatalogId;

        // The lab network names the (already-created) switch directly. Keep the switch out of the
        // NIC: the NIC binds by networkId so the static IP + self-DNS mark this as guest work.
        var network = root["labNetworks"]?.AsArray()?.FirstOrDefault()?.AsObject()
            ?? throw new InvalidOperationException("DC V2 template fixture is missing its first labNetworks entry.");
        network["switchName"] = switchName;

        // firstDomainControllerVmId must match the VM that promotes the forest, or the planner
        // cannot resolve which VM owns the root domain.
        var rootDomain = root["directoryTopology"]?["domains"]?.AsArray()?.FirstOrDefault()?.AsObject()
            ?? throw new InvalidOperationException("DC V2 template fixture is missing its root domain entry.");
        rootDomain["firstDomainControllerVmId"] = vmId;

        var dnsName = rootDomain["dnsName"]?.GetValue<string>()
            ?? throw new InvalidOperationException("DC V2 template fixture root domain is missing dnsName.");
        var netBiosName = rootDomain["netBiosName"]?.GetValue<string>()
            ?? throw new InvalidOperationException("DC V2 template fixture root domain is missing netBiosName.");

        Directory.CreateDirectory(_appData.TemplatesFolder);
        var filePath = Path.Combine(_appData.TemplatesFolder, templateName + ".json");
        File.WriteAllText(filePath, root.ToJsonString(JsonOptions));

        return new SeededDcTemplate(filePath, templateName, vmName, dnsName, netBiosName, DcLocalBootstrapSlotKey);
    }

    /// <summary>
    /// Writes a tagged single-router V2 template that references the REAL prepared base image
    /// (by catalog id) and returns its on-disk identity plus the LAN gateway IP to validate.
    /// The router VM name and the template file name both carry the run prefix so the deployed
    /// VM and the file are sweepable.
    ///
    /// The router is a standalone VM (topologyRole Router) with two NICs: a LAN NIC bound by
    /// networkId to a dedicated Internal switch and holding the segment's .1 gateway (a static
    /// IP, so the plan requires guest work - RRAS/NAT + address config - and therefore a
    /// bootstrap-capable base image plus a resolved local bootstrap credential slot), and an
    /// external NIC bridged onto the host's Default Switch for egress. Only the LAN switch is
    /// rewritten to the harness-provisioned switch; the external NIC keeps "Default Switch"
    /// (present on the host, not harness-owned, so never swept). The LAN static IP is read back
    /// from the fixture we are about to write, so live validation compares against exactly what
    /// was deployed.
    /// </summary>
    public SeededRouterTemplate SeedRouterTemplate(ResourceTagger tagger, string baseDiskCatalogId, string lanSwitchName)
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Templates", "router-v2-template.json");
        if (!File.Exists(fixturePath))
        {
            throw new FileNotFoundException(
                $"Router V2 template fixture not found at '{fixturePath}'. Ensure Fixtures\\Templates\\router-v2-template.json is copied to output.");
        }

        var root = JsonNode.Parse(File.ReadAllText(fixturePath))?.AsObject()
            ?? throw new InvalidOperationException("Router V2 template fixture did not parse as a JSON object.");

        var templateName = tagger.Name("rtrtpl");
        var vmName = tagger.Name("rtr");
        root["id"] = Guid.NewGuid().ToString("N");
        root["name"] = templateName;

        var vm = root["vmTemplates"]?.AsArray()?.FirstOrDefault()?.AsObject()
            ?? throw new InvalidOperationException("Router V2 template fixture is missing its first vmTemplates entry.");
        vm["vmId"] = Guid.NewGuid().ToString("N");
        vm["name"] = vmName;
        vm["vhdxId"] = baseDiskCatalogId;

        // Rewrite ONLY the LAN lab network onto the harness-provisioned switch. The external
        // network keeps "Default Switch" (a host-owned switch we must never sweep).
        var lanNetwork = root["labNetworks"]?.AsArray()
            ?.Select(n => n?.AsObject())
            .FirstOrDefault(n => n is not null && string.Equals(n["networkId"]?.GetValue<string>(), "lan-net", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Router V2 template fixture is missing its 'lan-net' labNetworks entry.");
        lanNetwork["switchName"] = lanSwitchName;

        // Read the LAN gateway IP straight from the fixture so validation checks exactly what we deploy.
        var lanNic = vm["nics"]?.AsArray()
            ?.Select(n => n?.AsObject())
            .FirstOrDefault(n => n is not null && string.Equals(n["networkId"]?.GetValue<string>(), "lan-net", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Router V2 template fixture is missing its 'lan-net' NIC.");
        var lanIp = lanNic["ipAddress"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Router V2 template fixture 'lan-net' NIC is missing ipAddress.");

        Directory.CreateDirectory(_appData.TemplatesFolder);
        var filePath = Path.Combine(_appData.TemplatesFolder, templateName + ".json");
        File.WriteAllText(filePath, root.ToJsonString(JsonOptions));

        return new SeededRouterTemplate(filePath, templateName, vmName, lanIp, DcLocalBootstrapSlotKey);
    }

    /// <summary>
    /// Writes a tagged single guest-static-IP V2 template that references the REAL prepared base
    /// image (by catalog id) on the given switch, and returns its on-disk identity plus the static
    /// IP to validate. The VM name and the template file name both carry the run prefix so the
    /// deployed VM and the file are sweepable.
    ///
    /// The VM is a plain standalone client with a single NIC bound by networkId to an Internal lab
    /// switch and holding a static IP, so the plan requires guest work (in-guest static-IP config)
    /// and therefore a bootstrap-capable base image plus a resolved local bootstrap credential slot.
    /// This is the lean, non-DC, non-router proof of the multi-NIC/static-IP fix for the simple case.
    /// Only the lab network's switch is rewritten; the static IP is read back from the fixture we are
    /// about to write, so live validation compares against exactly what was deployed.
    /// </summary>
    public SeededGuestStaticTemplate SeedGuestStaticTemplate(ResourceTagger tagger, string baseDiskCatalogId, string switchName)
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Templates", "guest-static-v2-template.json");
        if (!File.Exists(fixturePath))
        {
            throw new FileNotFoundException(
                $"Guest-static V2 template fixture not found at '{fixturePath}'. Ensure Fixtures\\Templates\\guest-static-v2-template.json is copied to output.");
        }

        var root = JsonNode.Parse(File.ReadAllText(fixturePath))?.AsObject()
            ?? throw new InvalidOperationException("Guest-static V2 template fixture did not parse as a JSON object.");

        var templateName = tagger.Name("gstpl");
        var vmName = tagger.Name("gs");
        root["id"] = Guid.NewGuid().ToString("N");
        root["name"] = templateName;

        var vm = root["vmTemplates"]?.AsArray()?.FirstOrDefault()?.AsObject()
            ?? throw new InvalidOperationException("Guest-static V2 template fixture is missing its first vmTemplates entry.");
        vm["vmId"] = Guid.NewGuid().ToString("N");
        vm["name"] = vmName;
        vm["vhdxId"] = baseDiskCatalogId;

        // The lab network names the (already-created) Internal switch directly. Keep the switch out
        // of the NIC: the NIC binds by networkId so the static IP marks this as guest work.
        var network = root["labNetworks"]?.AsArray()?.FirstOrDefault()?.AsObject()
            ?? throw new InvalidOperationException("Guest-static V2 template fixture is missing its first labNetworks entry.");
        network["switchName"] = switchName;

        // Read the static IP straight from the fixture so validation checks exactly what we deploy.
        var nic = vm["nics"]?.AsArray()?.FirstOrDefault()?.AsObject()
            ?? throw new InvalidOperationException("Guest-static V2 template fixture is missing its first NIC.");
        var staticIp = nic["ipAddress"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Guest-static V2 template fixture NIC is missing ipAddress.");

        Directory.CreateDirectory(_appData.TemplatesFolder);
        var filePath = Path.Combine(_appData.TemplatesFolder, templateName + ".json");
        File.WriteAllText(filePath, root.ToJsonString(JsonOptions));

        return new SeededGuestStaticTemplate(filePath, templateName, vmName, staticIp, DcLocalBootstrapSlotKey);
    }

    /// <summary>
    /// Writes a tagged two-VM guest-static-IP V2 template that references the REAL prepared base
    /// image (by catalog id) on the given switch, and returns its on-disk identity plus the per-VM
    /// static IP ground truth. Every VM name and the template file name carry the run prefix so each
    /// deployed VM and the file are sweepable.
    ///
    /// Both VMs are plain standalone clients on the SAME Internal lab switch, each with a single NIC
    /// bound by networkId and holding its OWN distinct static IP, so the plan requires guest work for
    /// each VM (a bootstrap-capable base image + a resolved local bootstrap credential slot). The
    /// per-VM static IP is read back from the fixture we are about to write, so live validation
    /// compares each VM against exactly what was deployed - the proof of per-adapter MAC binding.
    /// </summary>
    public SeededGuestStaticMultiVmTemplate SeedGuestStaticMultiVmTemplate(ResourceTagger tagger, string baseDiskCatalogId, string switchName)
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Templates", "guest-static-multivm-v2-template.json");
        if (!File.Exists(fixturePath))
        {
            throw new FileNotFoundException(
                $"Guest-static multi-VM V2 template fixture not found at '{fixturePath}'. Ensure Fixtures\\Templates\\guest-static-multivm-v2-template.json is copied to output.");
        }

        var root = JsonNode.Parse(File.ReadAllText(fixturePath))?.AsObject()
            ?? throw new InvalidOperationException("Guest-static multi-VM V2 template fixture did not parse as a JSON object.");

        var templateName = tagger.Name("gsmtpl");
        root["id"] = Guid.NewGuid().ToString("N");
        root["name"] = templateName;

        var network = root["labNetworks"]?.AsArray()?.FirstOrDefault()?.AsObject()
            ?? throw new InvalidOperationException("Guest-static multi-VM V2 template fixture is missing its first labNetworks entry.");
        network["switchName"] = switchName;

        var vmArray = root["vmTemplates"]?.AsArray()
            ?? throw new InvalidOperationException("Guest-static multi-VM V2 template fixture is missing its vmTemplates array.");
        if (vmArray.Count == 0)
        {
            throw new InvalidOperationException("Guest-static multi-VM V2 template fixture has no VM entries.");
        }

        var seededVms = new List<SeededGuestStaticVm>(vmArray.Count);
        for (int i = 0; i < vmArray.Count; i++)
        {
            var vm = vmArray[i]?.AsObject()
                ?? throw new InvalidOperationException($"Guest-static multi-VM V2 template fixture VM entry {i} is not an object.");

            var vmName = tagger.Name($"gsm{i + 1}");
            vm["vmId"] = Guid.NewGuid().ToString("N");
            vm["name"] = vmName;
            vm["vhdxId"] = baseDiskCatalogId;

            var nic = vm["nics"]?.AsArray()?.FirstOrDefault()?.AsObject()
                ?? throw new InvalidOperationException($"Guest-static multi-VM V2 template fixture VM entry {i} is missing its first NIC.");
            var staticIp = nic["ipAddress"]?.GetValue<string>()
                ?? throw new InvalidOperationException($"Guest-static multi-VM V2 template fixture VM entry {i} NIC is missing ipAddress.");

            seededVms.Add(new SeededGuestStaticVm(vmName, staticIp));
        }

        Directory.CreateDirectory(_appData.TemplatesFolder);
        var filePath = Path.Combine(_appData.TemplatesFolder, templateName + ".json");
        File.WriteAllText(filePath, root.ToJsonString(JsonOptions));

        return new SeededGuestStaticMultiVmTemplate(filePath, templateName, seededVms, DcLocalBootstrapSlotKey);
    }

    /// <summary>
    /// Writes a tagged minimal standalone V2 template whose only NIC names a switch the caller
    /// guarantees is ABSENT from the host (a run-tagged ghost name), and returns its on-disk identity.
    /// The template file name and VM name both carry the run prefix so the file and the resulting VM
    /// are both sweepable; the ghost switch name also carries the run prefix, so if the runtime creates
    /// it the gate's tag-based switch sweep removes it (no orphan).
    ///
    /// Unlike <see cref="SeedStandaloneTemplate"/> this fixture is intended to be DEPLOYED (not just
    /// planned): the deploy is supposed to create the missing switch as Internal, which the live
    /// scenario then asserts against Get-VMSwitch. The VM carries no credential slots and a bare disk,
    /// so the missing switch is the sole variable in the plan.
    /// </summary>
    public SeededTemplate SeedSwitchAutoCreateLiveTemplate(ResourceTagger tagger, string baseDiskCatalogId, string ghostSwitchName)
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Templates", "switch-autocreate-live-v2-template.json");
        if (!File.Exists(fixturePath))
        {
            throw new FileNotFoundException(
                $"Switch-auto-create-live V2 template fixture not found at '{fixturePath}'. Ensure Fixtures\\Templates\\switch-autocreate-live-v2-template.json is copied to output.");
        }

        var root = JsonNode.Parse(File.ReadAllText(fixturePath))?.AsObject()
            ?? throw new InvalidOperationException("Switch-auto-create-live V2 template fixture did not parse as a JSON object.");

        var templateName = tagger.Name("saltpl");
        var vmName = tagger.Name("sal");
        root["id"] = Guid.NewGuid().ToString("N");
        root["name"] = templateName;

        var vm = root["vmTemplates"]?.AsArray()?.FirstOrDefault()?.AsObject()
            ?? throw new InvalidOperationException("Switch-auto-create-live V2 template fixture is missing its first vmTemplates entry.");
        vm["vmId"] = Guid.NewGuid().ToString("N");
        vm["name"] = vmName;
        vm["vhdxId"] = baseDiskCatalogId;

        // The NIC names the ghost switch directly (bare-switch shape). It is deliberately absent from
        // the host, so the plan is startable only if the deploy is willing to create the missing switch.
        var nic = vm["nics"]?.AsArray()?.FirstOrDefault()?.AsObject()
            ?? throw new InvalidOperationException("Switch-auto-create-live V2 template fixture is missing its first NIC.");
        nic["switchName"] = ghostSwitchName;

        Directory.CreateDirectory(_appData.TemplatesFolder);
        var filePath = Path.Combine(_appData.TemplatesFolder, templateName + ".json");
        File.WriteAllText(filePath, root.ToJsonString(JsonOptions));

        return new SeededTemplate(filePath, templateName, vmName);
    }

    /// <summary>
    /// Writes a tagged DC + domain-joined member V2 template that references the REAL prepared base
    /// image (by catalog id) on the given switch, and returns its on-disk identity plus the DC/member
    /// VM names and the forest/domain ground truth to validate against. Both VM names and the template
    /// file name carry the run prefix so the deployed VMs and the file are sweepable.
    ///
    /// The template's directory topology (forest smoke.lab / SMOKE) has a FirstDomainController VM on
    /// a static NIC that serves its own DNS, and a DomainMember VM on a static NIC whose DNS points at
    /// the DC so it can locate and join the domain. Both reference a bootstrap-capable base image, so
    /// the plan requires guest work (DC promotion + a domain join) and therefore a resolved local
    /// bootstrap credential slot. The DC's fixed vmId is stitched into firstDomainControllerVmId here
    /// so the domain always points at the VM that promotes it. Only the lab network's switch is
    /// rewritten; the forest/domain DNS + NetBIOS names are read back from the fixture we are about to
    /// write, so live validation compares each guest against exactly what was deployed.
    /// </summary>
    public SeededDcMemberTemplate SeedDomainControllerMemberTemplate(ResourceTagger tagger, string baseDiskCatalogId, string switchName)
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Templates", "dc-member-v2-template.json");
        if (!File.Exists(fixturePath))
        {
            throw new FileNotFoundException(
                $"DC-member V2 template fixture not found at '{fixturePath}'. Ensure Fixtures\\Templates\\dc-member-v2-template.json is copied to output.");
        }

        var root = JsonNode.Parse(File.ReadAllText(fixturePath))?.AsObject()
            ?? throw new InvalidOperationException("DC-member V2 template fixture did not parse as a JSON object.");

        var templateName = tagger.Name("dcmtpl");
        root["id"] = Guid.NewGuid().ToString("N");
        root["name"] = templateName;

        // The lab network names the (already-created) Internal switch directly. The NICs bind by
        // networkId (static IPs), so the switch stays out of the NICs and both VMs need guest work.
        var network = root["labNetworks"]?.AsArray()?.FirstOrDefault()?.AsObject()
            ?? throw new InvalidOperationException("DC-member V2 template fixture is missing its first labNetworks entry.");
        network["switchName"] = switchName;

        var vmArray = root["vmTemplates"]?.AsArray()
            ?? throw new InvalidOperationException("DC-member V2 template fixture is missing its vmTemplates array.");

        // Identify the two roles by their authored intent, not by position: the DC is the
        // FirstDomainController, the member is the DomainMember. This keeps the seeder correct even if
        // the fixture's VM order changes.
        var dcVm = vmArray
            .Select(n => n?.AsObject())
            .FirstOrDefault(n => n is not null &&
                string.Equals(n["topologyRole"]?.GetValue<string>(), "FirstDomainController", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("DC-member V2 template fixture is missing its FirstDomainController VM.");
        var memberVm = vmArray
            .Select(n => n?.AsObject())
            .FirstOrDefault(n => n is not null &&
                string.Equals(n["membershipMode"]?.GetValue<string>(), "DomainMember", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("DC-member V2 template fixture is missing its DomainMember VM.");

        var dcVmId = Guid.NewGuid().ToString("N");
        var dcVmName = tagger.Name("dc");
        dcVm["vmId"] = dcVmId;
        dcVm["name"] = dcVmName;
        dcVm["vhdxId"] = baseDiskCatalogId;

        var memberVmName = tagger.Name("mem");
        memberVm["vmId"] = Guid.NewGuid().ToString("N");
        memberVm["name"] = memberVmName;
        memberVm["vhdxId"] = baseDiskCatalogId;

        // firstDomainControllerVmId must match the VM that promotes the forest, or the planner cannot
        // resolve which VM owns the root domain the member then joins.
        var rootDomain = root["directoryTopology"]?["domains"]?.AsArray()?.FirstOrDefault()?.AsObject()
            ?? throw new InvalidOperationException("DC-member V2 template fixture is missing its root domain entry.");
        rootDomain["firstDomainControllerVmId"] = dcVmId;

        var dnsName = rootDomain["dnsName"]?.GetValue<string>()
            ?? throw new InvalidOperationException("DC-member V2 template fixture root domain is missing dnsName.");
        var netBiosName = rootDomain["netBiosName"]?.GetValue<string>()
            ?? throw new InvalidOperationException("DC-member V2 template fixture root domain is missing netBiosName.");

        Directory.CreateDirectory(_appData.TemplatesFolder);
        var filePath = Path.Combine(_appData.TemplatesFolder, templateName + ".json");
        File.WriteAllText(filePath, root.ToJsonString(JsonOptions));

        return new SeededDcMemberTemplate(filePath, templateName, dcVmName, memberVmName, dnsName, netBiosName, DcLocalBootstrapSlotKey);
    }

    /// <summary>
    /// Writes a tagged two-forest + bidirectional forest-trust V2 template that references the REAL
    /// prepared base image (by catalog id) and returns its on-disk identity plus the per-forest ground
    /// truth to validate after deploy. Both DC VM names and the template file name carry the run prefix
    /// so the deployed VMs and the file are all sweepable.
    ///
    /// The topology has two root forests on one shared Internal switch: forest-alpha (alpha.lab / ALPHA)
    /// and forest-beta (beta.lab / BETA), each with a single FirstDomainController on a static NIC that
    /// serves its own DNS, plus one bidirectional Forest trust between the roots. Both DCs reference a
    /// bootstrap-capable base image, so the plan requires guest work (two DC promotions + the trust
    /// steps) and therefore a resolved local bootstrap credential slot; the planner reuses that single
    /// slot for each domain's domain-admin/DSRM and for the cross-forest trust credential.
    ///
    /// Each domain's firstDomainControllerVmId is stitched to the VM that owns that domain by matching
    /// vmTemplate.domainId to the domain id (not by array position), so the wiring stays correct even if
    /// the fixture's VM order changes. Only the lab network's switch is rewritten; the forest/domain DNS
    /// + NetBIOS names are read back from the fixture we are about to write, so live validation compares
    /// each guest against exactly what was deployed.
    /// </summary>
    public SeededForestTrustTemplate SeedForestTrustTemplate(ResourceTagger tagger, string baseDiskCatalogId, string switchName)
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Templates", "forest-trust-v2-template.json");
        if (!File.Exists(fixturePath))
        {
            throw new FileNotFoundException(
                $"Forest-trust V2 template fixture not found at '{fixturePath}'. Ensure Fixtures\\Templates\\forest-trust-v2-template.json is copied to output.");
        }

        var root = JsonNode.Parse(File.ReadAllText(fixturePath))?.AsObject()
            ?? throw new InvalidOperationException("Forest-trust V2 template fixture did not parse as a JSON object.");

        var templateName = tagger.Name("fttpl");
        root["id"] = Guid.NewGuid().ToString("N");
        root["name"] = templateName;

        // The lab network names the (already-created) Internal switch directly. The NICs bind by
        // networkId (static IPs), so the switch stays out of the NICs and both DCs need guest work.
        var network = root["labNetworks"]?.AsArray()?.FirstOrDefault()?.AsObject()
            ?? throw new InvalidOperationException("Forest-trust V2 template fixture is missing its first labNetworks entry.");
        network["switchName"] = switchName;

        var domains = root["directoryTopology"]?["domains"]?.AsArray()
            ?? throw new InvalidOperationException("Forest-trust V2 template fixture is missing its directoryTopology.domains array.");
        var vmArray = root["vmTemplates"]?.AsArray()
            ?? throw new InvalidOperationException("Forest-trust V2 template fixture is missing its vmTemplates array.");

        // Give each DC VM a fresh tagged identity, then stitch its new vmId into the domain it owns by
        // matching the VM's domainId to the domain id. Both VMs are FirstDomainController, so role alone
        // cannot disambiguate them - the domainId is the authoritative link.
        var seeded = new List<(string DomainId, string VmName, string DnsName, string NetBiosName)>();
        foreach (var domainNode in domains)
        {
            var domain = domainNode?.AsObject()
                ?? throw new InvalidOperationException("Forest-trust V2 template fixture has a malformed domain entry.");
            var domainId = domain["domainId"]?.GetValue<string>()
                ?? throw new InvalidOperationException("Forest-trust V2 template fixture domain is missing domainId.");

            var vm = vmArray
                .Select(n => n?.AsObject())
                .FirstOrDefault(n => n is not null &&
                    string.Equals(n["domainId"]?.GetValue<string>(), domainId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"Forest-trust V2 template fixture has no vmTemplate whose domainId is '{domainId}'.");

            var dnsName = domain["dnsName"]?.GetValue<string>()
                ?? throw new InvalidOperationException($"Forest-trust V2 template fixture domain '{domainId}' is missing dnsName.");
            var netBiosName = domain["netBiosName"]?.GetValue<string>()
                ?? throw new InvalidOperationException($"Forest-trust V2 template fixture domain '{domainId}' is missing netBiosName.");

            // Each DC gets a per-forest suffix (dc-<netbios>) so the two FirstDomainController VMs never
            // collide on the same run-tagged name; both remain harness-owned and sweepable by prefix.
            var vmId = Guid.NewGuid().ToString("N");
            var vmName = tagger.Name("dc-" + netBiosName.ToLowerInvariant());
            vm["vmId"] = vmId;
            vm["name"] = vmName;
            vm["vhdxId"] = baseDiskCatalogId;
            domain["firstDomainControllerVmId"] = vmId;

            seeded.Add((domainId, vmName, dnsName, netBiosName));
        }

        // Resolve the two anchors by the trust's source/target domain ids so the source DC (where the
        // runtime creates the trust) is unambiguous regardless of domain declaration order.
        var trust = root["directoryTopology"]?["trusts"]?.AsArray()?.FirstOrDefault()?.AsObject()
            ?? throw new InvalidOperationException("Forest-trust V2 template fixture is missing its trust entry.");
        var sourceDomainId = trust["sourceDomainId"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Forest-trust V2 template fixture trust is missing sourceDomainId.");
        var targetDomainId = trust["targetDomainId"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Forest-trust V2 template fixture trust is missing targetDomainId.");

        var source = seeded.FirstOrDefault(item => string.Equals(item.DomainId, sourceDomainId, StringComparison.OrdinalIgnoreCase));
        var target = seeded.FirstOrDefault(item => string.Equals(item.DomainId, targetDomainId, StringComparison.OrdinalIgnoreCase));
        if (source.VmName is null || target.VmName is null)
        {
            throw new InvalidOperationException(
                "Forest-trust V2 template fixture trust references a domain that has no seeded DC VM.");
        }

        Directory.CreateDirectory(_appData.TemplatesFolder);
        var filePath = Path.Combine(_appData.TemplatesFolder, templateName + ".json");
        File.WriteAllText(filePath, root.ToJsonString(JsonOptions));

        return new SeededForestTrustTemplate(
            filePath,
            templateName,
            source.VmName,
            source.DnsName,
            source.NetBiosName,
            target.VmName,
            target.DnsName,
            target.NetBiosName,
            DcLocalBootstrapSlotKey);
    }

    /// <summary>
    /// Writes a tagged ROUTED cross-forest + bidirectional forest-trust V2 template (the capstone rung):
    /// two root forests on SEPARATE gatewayed Internal switches bridged by a 3-NIC router, linked by one
    /// bidirectional Forest trust. It forks the same fixture shape as <see cref="SeedForestTrustTemplate"/>
    /// but rewrites TWO lab-network switches (alpha-net and beta-net) onto the caller's harness-owned
    /// switch names and leaves external-net on "Default Switch" (host-owned, never swept), and it also
    /// tags the standalone Router VM. Because the two anchor DCs live on disjoint switches and the router
    /// carries a leg on each, the planner must order the forest-trust/DNS-prep stage AFTER the router is
    /// routing (the RouterReady -> prepareForestTrustDns edge). All three VMs reference the REAL prepared
    /// base image so the plan requires guest work and can only start once the local bootstrap slot is filled.
    ///
    /// Each DC's firstDomainControllerVmId is stitched to the VM that owns its domain by matching
    /// vmTemplate.domainId (not array position); the router is resolved by topologyRole. Each DC's static
    /// IP, default gateway (the router's LAN leg on its subnet), and subnet are read back from the fixture
    /// we are about to write so live validation - especially the route-hop next-hop check - compares each
    /// guest against exactly what was deployed. "Source"/"target" follow the trust's own
    /// sourceDomainId/targetDomainId so the source DC is the anchor the runtime creates the trust from.
    /// </summary>
    public SeededForestTrustRoutedTemplate SeedForestTrustRoutedTemplate(
        ResourceTagger tagger,
        string baseDiskCatalogId,
        string switchAName,
        string switchBName)
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Templates", "forest-trust-routed-v2-template.json");
        if (!File.Exists(fixturePath))
        {
            throw new FileNotFoundException(
                $"Routed forest-trust V2 template fixture not found at '{fixturePath}'. Ensure Fixtures\\Templates\\forest-trust-routed-v2-template.json is copied to output.");
        }

        var root = JsonNode.Parse(File.ReadAllText(fixturePath))?.AsObject()
            ?? throw new InvalidOperationException("Routed forest-trust V2 template fixture did not parse as a JSON object.");

        var templateName = tagger.Name("ftrtpl");
        root["id"] = Guid.NewGuid().ToString("N");
        root["name"] = templateName;

        // Rewrite the two Internal lab-network switches onto the harness-owned (ghost) switch names; the
        // NICs bind by networkId (static IPs), so the switches stay out of the NICs and both DCs + the
        // router need guest work. external-net keeps "Default Switch" (a host-owned switch we never sweep).
        var networks = root["labNetworks"]?.AsArray()
            ?? throw new InvalidOperationException("Routed forest-trust V2 template fixture is missing its labNetworks array.");
        var alphaNet = FindNetwork(networks, "alpha-net");
        var betaNet = FindNetwork(networks, "beta-net");
        alphaNet["switchName"] = switchAName;
        betaNet["switchName"] = switchBName;

        // Map networkId -> subnet so each DC's own subnet can be read back for the route-hop assertion.
        var subnetByNetwork = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var networkNode in networks)
        {
            var network = networkNode?.AsObject();
            var networkId = network?["networkId"]?.GetValue<string>();
            var subnet = network?["subnet"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(networkId) && !string.IsNullOrWhiteSpace(subnet))
            {
                subnetByNetwork[networkId!] = subnet!;
            }
        }

        var domains = root["directoryTopology"]?["domains"]?.AsArray()
            ?? throw new InvalidOperationException("Routed forest-trust V2 template fixture is missing its directoryTopology.domains array.");
        var vmArray = root["vmTemplates"]?.AsArray()
            ?? throw new InvalidOperationException("Routed forest-trust V2 template fixture is missing its vmTemplates array.");

        // Tag + stitch each DC by domainId, reading back its NIC's static IP, default gateway (the router
        // LAN leg on this DC's subnet), and subnet so the scenario proves the route to the peer uses the
        // router as its next hop. Both DCs are FirstDomainController, so domainId is the authoritative link.
        var seeded = new List<RoutedDcSeed>();
        foreach (var domainNode in domains)
        {
            var domain = domainNode?.AsObject()
                ?? throw new InvalidOperationException("Routed forest-trust V2 template fixture has a malformed domain entry.");
            var domainId = domain["domainId"]?.GetValue<string>()
                ?? throw new InvalidOperationException("Routed forest-trust V2 template fixture domain is missing domainId.");

            var vm = vmArray
                .Select(n => n?.AsObject())
                .FirstOrDefault(n => n is not null &&
                    string.Equals(n["domainId"]?.GetValue<string>(), domainId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"Routed forest-trust V2 template fixture has no vmTemplate whose domainId is '{domainId}'.");

            var dnsName = domain["dnsName"]?.GetValue<string>()
                ?? throw new InvalidOperationException($"Routed forest-trust V2 template fixture domain '{domainId}' is missing dnsName.");
            var netBiosName = domain["netBiosName"]?.GetValue<string>()
                ?? throw new InvalidOperationException($"Routed forest-trust V2 template fixture domain '{domainId}' is missing netBiosName.");

            var vmId = Guid.NewGuid().ToString("N");
            var vmName = tagger.Name("dc-" + netBiosName.ToLowerInvariant());
            vm["vmId"] = vmId;
            vm["name"] = vmName;
            vm["vhdxId"] = baseDiskCatalogId;
            domain["firstDomainControllerVmId"] = vmId;

            var nic = vm["nics"]?.AsArray()?.FirstOrDefault()?.AsObject()
                ?? throw new InvalidOperationException($"Routed forest-trust V2 template fixture DC for domain '{domainId}' is missing its NIC.");
            var networkId = nic["networkId"]?.GetValue<string>()
                ?? throw new InvalidOperationException($"Routed forest-trust V2 template fixture DC for domain '{domainId}' has a NIC with no networkId.");
            var dcIp = nic["ipAddress"]?.GetValue<string>()
                ?? throw new InvalidOperationException($"Routed forest-trust V2 template fixture DC for domain '{domainId}' has a NIC with no ipAddress.");
            var gateway = nic["defaultGateway"]?.GetValue<string>()
                ?? throw new InvalidOperationException($"Routed forest-trust V2 template fixture DC for domain '{domainId}' has a NIC with no defaultGateway (the routed topology needs each DC to reach the peer through the router).");
            if (!subnetByNetwork.TryGetValue(networkId, out var subnet))
            {
                throw new InvalidOperationException(
                    $"Routed forest-trust V2 template fixture DC for domain '{domainId}' references network '{networkId}' which has no subnet.");
            }

            seeded.Add(new RoutedDcSeed(domainId, vmName, dnsName, netBiosName, dcIp, gateway, subnet));
        }

        // Tag the standalone Router VM (resolved by role, not position).
        var routerVm = vmArray
            .Select(n => n?.AsObject())
            .FirstOrDefault(n => n is not null &&
                string.Equals(n["topologyRole"]?.GetValue<string>(), "Router", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Routed forest-trust V2 template fixture is missing its Router vmTemplate.");
        var routerVmName = tagger.Name("rtr");
        routerVm["vmId"] = Guid.NewGuid().ToString("N");
        routerVm["name"] = routerVmName;
        routerVm["vhdxId"] = baseDiskCatalogId;

        // Resolve the two anchors by the trust's source/target domain ids so the source DC (where the
        // runtime creates the trust) is unambiguous regardless of domain declaration order.
        var trust = root["directoryTopology"]?["trusts"]?.AsArray()?.FirstOrDefault()?.AsObject()
            ?? throw new InvalidOperationException("Routed forest-trust V2 template fixture is missing its trust entry.");
        var sourceDomainId = trust["sourceDomainId"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Routed forest-trust V2 template fixture trust is missing sourceDomainId.");
        var targetDomainId = trust["targetDomainId"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Routed forest-trust V2 template fixture trust is missing targetDomainId.");

        var source = seeded.FirstOrDefault(item => string.Equals(item.DomainId, sourceDomainId, StringComparison.OrdinalIgnoreCase));
        var target = seeded.FirstOrDefault(item => string.Equals(item.DomainId, targetDomainId, StringComparison.OrdinalIgnoreCase));
        if (source is null || target is null)
        {
            throw new InvalidOperationException(
                "Routed forest-trust V2 template fixture trust references a domain that has no seeded DC VM.");
        }

        Directory.CreateDirectory(_appData.TemplatesFolder);
        var filePath = Path.Combine(_appData.TemplatesFolder, templateName + ".json");
        File.WriteAllText(filePath, root.ToJsonString(JsonOptions));

        return new SeededForestTrustRoutedTemplate(
            filePath,
            templateName,
            routerVmName,
            source.VmName,
            source.DnsName,
            source.NetBiosName,
            source.DcIp,
            source.Gateway,
            source.Subnet,
            target.VmName,
            target.DnsName,
            target.NetBiosName,
            target.DcIp,
            target.Gateway,
            target.Subnet,
            switchAName,
            switchBName,
            DcLocalBootstrapSlotKey);
    }

    private static JsonObject FindNetwork(JsonArray networks, string networkId)
        => networks
            .Select(n => n?.AsObject())
            .FirstOrDefault(n => n is not null && string.Equals(n["networkId"]?.GetValue<string>(), networkId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Routed forest-trust V2 template fixture is missing its '{networkId}' labNetworks entry.");

    private sealed record RoutedDcSeed(
        string DomainId,
        string VmName,
        string DnsName,
        string NetBiosName,
        string DcIp,
        string Gateway,
        string Subnet);
}

/// <summary>Identity of a harness-seeded template: its file, its library display name, and the VM name it deploys.</summary>
public sealed record SeededTemplate(string FilePath, string TemplateName, string VmName);

/// <summary>Identity of a harness-seeded multi-VM template: its file, its library display name, and the per-VM ground truth it deploys.</summary>
public sealed record SeededMultiVmTemplate(string FilePath, string TemplateName, IReadOnlyList<SeededVm> Vms);

/// <summary>The expected ground truth for one VM in a seeded multi-VM template: its tagged name and its own memory/cpu.</summary>
public sealed record SeededVm(string VmName, int ExpectedMemoryMb, int ExpectedCpu);

/// <summary>
/// Identity + forest ground truth of a harness-seeded single-DC template: its file, its library
/// display name, the DC VM name it deploys, the forest/root-domain DNS + NetBIOS names to validate
/// against after promotion, and the local bootstrap credential slot the deploy must fill.
/// </summary>
public sealed record SeededDcTemplate(
    string FilePath,
    string TemplateName,
    string VmName,
    string DnsName,
    string NetBiosName,
    string LocalBootstrapSlotKey);

/// <summary>
/// Identity + LAN ground truth of a harness-seeded single-router template: its file, its library
/// display name, the router VM name it deploys, the LAN gateway IP the guest must end up holding
/// (the multi-NIC static-IP the router applies), and the local bootstrap credential slot the
/// deploy must fill.
/// </summary>
public sealed record SeededRouterTemplate(
    string FilePath,
    string TemplateName,
    string VmName,
    string LanIpAddress,
    string LocalBootstrapSlotKey);

/// <summary>
/// Identity + static-IP ground truth of a harness-seeded single guest-static-IP template: its file,
/// its library display name, the client VM name it deploys, the static IP the guest must end up
/// holding (the in-guest static address applied on its adapter), and the local bootstrap credential
/// slot the deploy must fill.
/// </summary>
public sealed record SeededGuestStaticTemplate(
    string FilePath,
    string TemplateName,
    string VmName,
    string StaticIpAddress,
    string LocalBootstrapSlotKey);

/// <summary>
/// Identity + per-VM static-IP ground truth of a harness-seeded two-VM guest-static-IP template: its
/// file, its library display name, the per-VM ground truth it deploys, and the local bootstrap
/// credential slot the deploy must fill for every VM.
/// </summary>
public sealed record SeededGuestStaticMultiVmTemplate(
    string FilePath,
    string TemplateName,
    IReadOnlyList<SeededGuestStaticVm> Vms,
    string LocalBootstrapSlotKey);

/// <summary>The expected ground truth for one VM in a seeded guest-static template: its tagged name and its own static IP.</summary>
public sealed record SeededGuestStaticVm(string VmName, string StaticIpAddress);

/// <summary>
/// Identity + forest ground truth of a harness-seeded DC + domain-joined member template: its file,
/// its library display name, the DC VM name and the member VM name it deploys, the forest/root-domain
/// DNS + NetBIOS names to validate against (the DC promotes them and the member joins them), and the
/// local bootstrap credential slot the deploy must fill for both VMs.
/// </summary>
public sealed record SeededDcMemberTemplate(
    string FilePath,
    string TemplateName,
    string DcVmName,
    string MemberVmName,
    string DnsName,
    string NetBiosName,
    string LocalBootstrapSlotKey);

/// <summary>
/// Identity + per-forest ground truth of a harness-seeded two-forest + bidirectional forest-trust
/// template: its file, its library display name, the source and target DC VM names it deploys, each
/// forest/root-domain DNS + NetBIOS names to validate against (each DC promotes its own forest and the
/// runtime establishes the trust between them), and the local bootstrap credential slot the deploy must
/// fill for both DCs. "Source" and "target" follow the trust's own sourceDomainId/targetDomainId so the
/// source DC is the anchor the runtime creates the trust from; the harness proves BOTH sides in-guest.
/// </summary>
public sealed record SeededForestTrustTemplate(
    string FilePath,
    string TemplateName,
    string SourceDcVmName,
    string SourceDnsName,
    string SourceNetBiosName,
    string TargetDcVmName,
    string TargetDnsName,
    string TargetNetBiosName,
    string LocalBootstrapSlotKey);

/// <summary>
/// Identity + per-forest ground truth of a harness-seeded ROUTED cross-forest + bidirectional
/// forest-trust template (the capstone rung): its file, its library display name, the standalone
/// Router VM name, and for each side the DC VM name, forest/root-domain DNS + NetBIOS names, the DC's
/// static IP, the default gateway it uses (the router's LAN leg on that DC's subnet), and that DC's
/// subnet. The gateway + peer subnet let the scenario prove the trust traffic crossed the router
/// (route-hop next hop = the DC's gateway to reach the peer on the OTHER subnet). It also echoes the
/// two harness-owned Internal switch names so the no-orphans sweep can assert both were removed.
/// "Source"/"target" follow the trust's own sourceDomainId/targetDomainId, so the source DC is the
/// anchor the runtime creates the trust from; the harness proves BOTH sides in-guest. All three VMs
/// share the one local bootstrap credential slot the deploy must fill.
/// </summary>
public sealed record SeededForestTrustRoutedTemplate(
    string FilePath,
    string TemplateName,
    string RouterVmName,
    string SourceDcVmName,
    string SourceDnsName,
    string SourceNetBiosName,
    string SourceDcIpAddress,
    string SourceGatewayIpAddress,
    string SourceSubnet,
    string TargetDcVmName,
    string TargetDnsName,
    string TargetNetBiosName,
    string TargetDcIpAddress,
    string TargetGatewayIpAddress,
    string TargetSubnet,
    string SwitchAName,
    string SwitchBName,
    string LocalBootstrapSlotKey);
