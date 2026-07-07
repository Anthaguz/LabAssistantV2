using System.Text;
using LabAssistant.Models.Deployment;

namespace LabAssistant.Business.Runtime;

internal static class BaseRemoteAccessGuestScriptBuilder
{
    public static string BuildConfigureBaseRemoteAccessScript(V2BaseRemoteAccessOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine("$ErrorActionPreference = 'Stop'");
        sb.AppendLine();
        sb.AppendLine("if ($true) {");
        sb.AppendLine("    Set-ItemProperty -Path 'HKLM:\\System\\CurrentControlSet\\Control\\Terminal Server' -Name 'fDenyTSConnections' -Value 0 -ErrorAction Stop");
        sb.AppendLine("}");
        sb.AppendLine();

        if (options.DisableFirewall)
        {
            sb.AppendLine("netsh advfirewall set allprofiles state off | Out-Null");
            sb.AppendLine();
        }

        if (options.DisableRdpNla)
        {
            sb.AppendLine("$rdpSetting = Get-CimInstance -ClassName Win32_TSGeneralSetting -Namespace 'root/cimv2/terminalservices' -Filter \"TerminalName='RDP-tcp'\"");
            sb.AppendLine("if ($null -eq $rdpSetting) { throw 'RDP terminal settings could not be resolved.' }");
            sb.AppendLine("$nlaResult = Invoke-CimMethod -InputObject $rdpSetting -MethodName SetUserAuthenticationRequired -Arguments @{ UserAuthenticationRequired = 0 }");
            sb.AppendLine("if ($nlaResult.ReturnValue -ne 0) { throw \"Failed to disable RDP NLA. ReturnValue=$($nlaResult.ReturnValue)\" }");
            sb.AppendLine();
        }

        if (options.SetPrivateNetworkProfile)
        {
            sb.AppendLine("$profiles = Get-NetConnectionProfile -ErrorAction SilentlyContinue");
            sb.AppendLine("foreach ($profile in $profiles) {");
            sb.AppendLine("    Set-NetConnectionProfile -InterfaceIndex $profile.InterfaceIndex -NetworkCategory Private -ErrorAction Stop");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        sb.AppendLine("Write-Output 'Base remote access configured'");
        return sb.ToString();
    }

    /// <summary>
    /// Builds a guest probe that verifies the durable outcome of
    /// <see cref="BuildConfigureBaseRemoteAccessScript"/>: RDP is enabled
    /// (<c>fDenyTSConnections = 0</c>) and an RDP-tcp listener is present and listening on 3389.
    /// The script throws when readiness cannot be confirmed so the caller can retry the gate.
    /// </summary>
    public static string BuildProbeBaseRemoteAccessReadyScript()
    {
        var sb = new StringBuilder();
        sb.AppendLine("$ErrorActionPreference = 'Stop'");
        sb.AppendLine();
        sb.AppendLine("$deny = (Get-ItemProperty -Path 'HKLM:\\System\\CurrentControlSet\\Control\\Terminal Server' -Name 'fDenyTSConnections' -ErrorAction Stop).fDenyTSConnections");
        sb.AppendLine("if ($deny -ne 0) { throw \"RDP is not enabled. fDenyTSConnections=$deny\" }");
        sb.AppendLine();
        sb.AppendLine("$rdpSetting = Get-CimInstance -ClassName Win32_TSGeneralSetting -Namespace 'root/cimv2/terminalservices' -Filter \"TerminalName='RDP-tcp'\" -ErrorAction SilentlyContinue");
        sb.AppendLine("if ($null -eq $rdpSetting) { throw 'RDP-tcp listener is not present.' }");
        sb.AppendLine();
        sb.AppendLine("$listening = Get-NetTCPConnection -State Listen -LocalPort 3389 -ErrorAction SilentlyContinue");
        sb.AppendLine("if ($null -eq $listening) { throw 'No listener is bound to TCP 3389.' }");
        sb.AppendLine();
        sb.AppendLine("Write-Output 'Base remote access ready'");
        return sb.ToString();
    }
}
