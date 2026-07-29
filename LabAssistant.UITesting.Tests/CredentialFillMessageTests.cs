using LabAssistant.UITesting.Infrastructure;
using LabAssistant.UITesting.Pages;
using Xunit;

namespace LabAssistant.UITesting.Tests;

/// <summary>
/// Pins the four honest, self-attributing failure messages
/// <see cref="DeployFromTemplatePage.DescribeFillFailure"/> emits when a credential-slot fill cannot
/// be made to persist. The whole point of the fill hardening is that it fails LOUD and attributes the
/// failure correctly: a harness fill-miss (focus never landed / Save never enabled) must never be
/// blamed on the product, and the ambiguous "focus landed, Save fired, nothing persisted" case must
/// stay OPEN between an empty-password type-miss and a product Save bug rather than prematurely
/// closing on either. These assertions guard against that attribution silently drifting.
/// </summary>
public sealed class CredentialFillMessageTests
{
    [Fact]
    public void FocusNeverLanded_IsAttributedToHarnessFillMiss()
    {
        var outcome = new CredentialFillOutcome(Persisted: false, Attempts: 4, FocusEverLanded: false);

        var message = DeployFromTemplatePage.DescribeFillFailure(outcome, saveEverFired: false, reachedStore: false);

        Assert.Contains("focus never landed", message);
        Assert.Contains("harness fill-miss", message);
        Assert.DoesNotContain("product Save/Upsert failure", message);
    }

    [Fact]
    public void SaveButtonNeverEnabled_IsAttributedToHarnessFillMiss()
    {
        var outcome = new CredentialFillOutcome(Persisted: false, Attempts: 4, FocusEverLanded: true);

        var message = DeployFromTemplatePage.DescribeFillFailure(outcome, saveEverFired: false, reachedStore: false);

        Assert.Contains("never became enabled", message);
        Assert.Contains("harness fill-miss", message);
    }

    [Fact]
    public void ReachedStoreButRowStuck_IsAttributedToProductReplan()
    {
        var outcome = new CredentialFillOutcome(Persisted: false, Attempts: 4, FocusEverLanded: true);

        var message = DeployFromTemplatePage.DescribeFillFailure(outcome, saveEverFired: true, reachedStore: true);

        Assert.Contains("reached the store", message);
        Assert.Contains("re-plan/resolver", message);
    }

    [Fact]
    public void FocusedAndSavedButNothingPersisted_StaysOpenBetweenEmptyPasswordAndProductBug()
    {
        var outcome = new CredentialFillOutcome(Persisted: false, Attempts: 4, FocusEverLanded: true);

        var message = DeployFromTemplatePage.DescribeFillFailure(outcome, saveEverFired: true, reachedStore: false);

        Assert.Contains("did not persist after 4 attempt(s)", message);
        Assert.Contains("password-empty suspected", message);
        // The fork is held open: BOTH candidates named, neither declared the winner.
        Assert.Contains("harness", message);
        Assert.Contains("product Save/Upsert failure", message);
    }
}
