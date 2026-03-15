namespace LabAssistant.WinUI.ViewModels.Assets;

internal sealed class AssetsOverviewWorkspaceViewModel
{
    public string BaseDisksSummaryText { get; private set; } = "Open Base Disks to inspect imported VHDX inventory.";

    public string SwitchesSummaryText { get; private set; } = "Open Switches to inspect host virtual switch inventory.";

    public void RefreshSummary(bool isBaseDisksLoading, bool isSwitchesLoading, int assetsBaseDiskCount, int assetsSwitchCount)
    {
        BaseDisksSummaryText = isBaseDisksLoading
            ? "Base disk inventory is loading."
            : assetsBaseDiskCount > 0
                ? $"{assetsBaseDiskCount} base disks currently loaded."
                : "Open Base Disks to inspect imported VHDX inventory.";

        SwitchesSummaryText = isSwitchesLoading
            ? "Switch inventory is loading."
            : assetsSwitchCount > 0
                ? $"{assetsSwitchCount} virtual switches currently loaded."
                : "Open Switches to inspect host virtual switch inventory.";
    }
}
