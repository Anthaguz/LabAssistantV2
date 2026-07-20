using System.Collections.Concurrent;
using LabAssistant.Business.Runtime;
using LabAssistant.Models.Deployment;
using Xunit;

namespace LabAssistant.Business.Tests.Tests;

public sealed class RuntimeCredentialCoordinatorTests
{
    private static ConcurrentDictionary<string, V2RuntimeCredential> CreateSlots(string slotKey, V2RuntimeCredential value) =>
        new(System.StringComparer.OrdinalIgnoreCase) { [slotKey] = value };

    private static VmDeploymentContext ContextWithPrompt(
        System.Func<GuestCredentialPromptRequest, System.Threading.CancellationToken, System.Threading.Tasks.Task<GuestCredentialPromptResponse>>? prompt) =>
        new() { VmName = "vm01", RequestGuestCredential = prompt };

    [Fact]
    public async Task RepromptAsync_NoPromptWired_ReturnsNull()
    {
        var rejected = new V2RuntimeCredential { Username = "Administrator", Password = "wrong" };
        var slots = CreateSlots("slot-local", rejected);
        var coordinator = new RuntimeCredentialCoordinator(slots);

        var result = await coordinator.RepromptAsync(
            ContextWithPrompt(null),
            "slot-local",
            new GuestCredentialPromptRequest { CredentialSlotKey = "slot-local" },
            rejected,
            System.Threading.CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task RepromptAsync_UserCancels_ReturnsNullAndLeavesSlotUnchanged()
    {
        var rejected = new V2RuntimeCredential { Username = "Administrator", Password = "wrong" };
        var slots = CreateSlots("slot-local", rejected);
        var coordinator = new RuntimeCredentialCoordinator(slots);

        var result = await coordinator.RepromptAsync(
            ContextWithPrompt((_, _) => System.Threading.Tasks.Task.FromResult(GuestCredentialPromptResponse.Cancel())),
            "slot-local",
            new GuestCredentialPromptRequest { CredentialSlotKey = "slot-local" },
            rejected,
            System.Threading.CancellationToken.None);

        Assert.Null(result);
        Assert.Same(rejected, slots["slot-local"]);
    }

    [Fact]
    public async Task RepromptAsync_UserSuppliesCredential_StoresItOnSharedSlot()
    {
        var rejected = new V2RuntimeCredential { Username = "Administrator", Password = "wrong" };
        var slots = CreateSlots("slot-local", rejected);
        var coordinator = new RuntimeCredentialCoordinator(slots);

        var result = await coordinator.RepromptAsync(
            ContextWithPrompt((_, _) => System.Threading.Tasks.Task.FromResult(new GuestCredentialPromptResponse
            {
                Cancelled = false,
                Username = "Administrator",
                Password = "correct"
            })),
            "slot-local",
            new GuestCredentialPromptRequest { CredentialSlotKey = "slot-local" },
            rejected,
            System.Threading.CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("correct", result!.Password);
        // The shared dictionary is mutated in place so later VMs resolving the same slot see the correction.
        Assert.Equal("correct", slots["slot-local"].Password);
    }

    [Fact]
    public async Task RepromptAsync_SlotAlreadyCorrectedByAnotherVm_AdoptsItWithoutPrompting()
    {
        var rejected = new V2RuntimeCredential { Username = "Administrator", Password = "wrong" };
        var corrected = new V2RuntimeCredential { Username = "Administrator", Password = "correct" };
        // A peer VM already corrected the slot: the stored value no longer matches what this VM rejected.
        var slots = CreateSlots("slot-local", corrected);
        var coordinator = new RuntimeCredentialCoordinator(slots);

        var promptShown = false;
        var result = await coordinator.RepromptAsync(
            ContextWithPrompt((_, _) =>
            {
                promptShown = true;
                return System.Threading.Tasks.Task.FromResult(GuestCredentialPromptResponse.Cancel());
            }),
            "slot-local",
            new GuestCredentialPromptRequest { CredentialSlotKey = "slot-local" },
            rejected,
            System.Threading.CancellationToken.None);

        Assert.False(promptShown);
        Assert.Same(corrected, result);
    }

    [Fact]
    public async Task RepromptAsync_ConcurrentCallersSameSlot_PromptsOnce()
    {
        var rejected = new V2RuntimeCredential { Username = "Administrator", Password = "wrong" };
        var slots = CreateSlots("slot-local", rejected);
        var coordinator = new RuntimeCredentialCoordinator(slots);

        var promptCount = 0;
        var release = new System.Threading.Tasks.TaskCompletionSource();

        System.Threading.Tasks.Task<GuestCredentialPromptResponse> Prompt(
            GuestCredentialPromptRequest _, System.Threading.CancellationToken __)
        {
            System.Threading.Interlocked.Increment(ref promptCount);
            // Hold the first prompt open until both callers are contending on the gate, proving single-flight.
            return release.Task.ContinueWith(_ => new GuestCredentialPromptResponse
            {
                Cancelled = false,
                Username = "Administrator",
                Password = "correct"
            });
        }

        var context = ContextWithPrompt(Prompt);
        var request = new GuestCredentialPromptRequest { CredentialSlotKey = "slot-local" };

        var first = coordinator.RepromptAsync(context, "slot-local", request, rejected, System.Threading.CancellationToken.None);
        var second = coordinator.RepromptAsync(context, "slot-local", request, rejected, System.Threading.CancellationToken.None);

        release.SetResult();
        var results = await System.Threading.Tasks.Task.WhenAll(first, second);

        Assert.Equal(1, promptCount);
        Assert.All(results, r => Assert.Equal("correct", r!.Password));
    }
}
