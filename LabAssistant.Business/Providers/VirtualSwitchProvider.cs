using LabAssistant.Services.HyperV;
using LabAssistant.Services.PowerShell;
using LabAssistant.Services.Logging;

namespace LabAssistant.Business.Deployment
{
    public class VirtualSwitchProvider
    {
        private readonly Func<IPersistentPowerShellSession> _sessionFactory;
        private readonly Func<IPersistentPowerShellSession, IHyperVService> _hyperVFactory;

        public VirtualSwitchProvider(
            Func<IPersistentPowerShellSession> sessionFactory,
            Func<IPersistentPowerShellSession, IHyperVService> hyperVFactory)
        {
            _sessionFactory = sessionFactory;
            _hyperVFactory = hyperVFactory;
        }

        public async Task<List<string>> GetVirtualSwitchesAsync()
        {
            try
            {
                DebugLogger.Log("Fetching virtual switches...");
                using var session = _sessionFactory();
                var hyperv = _hyperVFactory(session);
                var switches = await hyperv.GetVirtualSwitchNamesAsync();
                DebugLogger.Log($"Found {switches.Count} virtual switch(es): {string.Join(", ", switches)}");
                return switches;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Failed to retrieve virtual switches: {ex.Message}");
                return new List<string>();
            }
        }
    }
}
