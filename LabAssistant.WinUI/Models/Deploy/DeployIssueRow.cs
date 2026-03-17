namespace LabAssistant.WinUI.Models.Deploy;

internal sealed record DeployIssueRow(
    string Scope,
    string Severity,
    string Message)
{
    public override string ToString() => $"{Severity} [{Scope}] {Message}";
}
