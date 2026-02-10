using System;
using System.Threading.Tasks;
using LabAssistant.Models.PowerShell;
using LabAssistant.Services.PowerShell;
using Xunit;

namespace LabAssistant.Services.Tests;

public class SessionResolverTests
{
    private sealed class FakeSession : IPersistentPowerShellSession
    {
        public void Dispose()
        {
        }

        public Task<(string Output, string Error)> ExecuteAsync(string command)
        {
            return Task.FromResult((string.Empty, string.Empty));
        }
    }

    [Fact]
    public void RegisterAndResolve_ReturnsSameSession()
    {
        var resolver = new SessionResolver();
        var handle = new PowerShellHandle();
        var session = new FakeSession();

        resolver.RegisterSession(handle, session);
        var resolved = resolver.Resolve(handle);

        Assert.Same(session, resolved);
    }

    [Fact]
    public void Resolve_ThrowsForUnknownHandle()
    {
        var resolver = new SessionResolver();
        var handle = new PowerShellHandle();

        Assert.Throws<KeyNotFoundException>(() => resolver.Resolve(handle));
    }

    [Fact]
    public void RemoveSession_RemovesRegisteredSession()
    {
        var resolver = new SessionResolver();
        var handle = new PowerShellHandle();
        var session = new FakeSession();

        resolver.RegisterSession(handle, session);
        resolver.RemoveSession(handle);

        Assert.Throws<KeyNotFoundException>(() => resolver.Resolve(handle));
    }
}
