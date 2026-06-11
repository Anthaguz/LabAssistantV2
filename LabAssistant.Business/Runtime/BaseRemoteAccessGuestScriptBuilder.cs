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
}
