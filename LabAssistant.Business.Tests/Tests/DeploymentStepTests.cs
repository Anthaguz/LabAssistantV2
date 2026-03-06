using System.Threading.Tasks;
using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using Xunit;

namespace LabAssistant.Business.Tests;

public class DeploymentStepTests
{
    private sealed class TestStep : DeploymentStep
    {
        private readonly System.Action _onHandle;

        public TestStep(System.Action onHandle)
        {
            _onHandle = onHandle;
        }

        protected override Task HandleAsync(VmDeploymentContext context)
        {
            _onHandle();
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ExecuteAsync_SkipsNext_WhenContextNotSuccessful()
    {
        var context = new VmDeploymentContext { IsSuccess = false };
        var handled = 0;
        var first = new TestStep(() => handled++);
        var next = new TestStep(() => handled++);
        first.SetNext(next);

        await first.ExecuteAsync(context);

        Assert.Equal(0, handled);
    }

    [Fact]
    public async Task ExecuteAsync_EmitsDeterministicStepStateTransitions()
    {
        var emitted = new List<DeployStepStateUpdate>();
        var context = new VmDeploymentContext
        {
            VmId = Guid.NewGuid(),
            VmName = "vm-1",
            OperationId = "op-1",
            StepStateEmitter = update => emitted.Add(update)
        };

        var step = new TestStep(() => { });

        await step.ExecuteAsync(context);

        Assert.Equal(3, emitted.Count);
        Assert.Equal(DeployStepState.Pending, emitted[0].State);
        Assert.Equal(DeployStepState.Running, emitted[1].State);
        Assert.Equal(DeployStepState.Succeeded, emitted[2].State);
        Assert.True(emitted[0].Sequence < emitted[1].Sequence && emitted[1].Sequence < emitted[2].Sequence);
    }

    [Fact]
    public async Task ExecuteAsync_UsesSkippedTerminalOverride_WhenProvidedByStep()
    {
        var emitted = new List<DeployStepStateUpdate>();
        var context = new VmDeploymentContext
        {
            VmId = Guid.NewGuid(),
            VmName = "vm-1",
            OperationId = "op-2",
            StepStateEmitter = update => emitted.Add(update)
        };

        var step = new TestStep(() => context.SetStepTerminalOverride("TestStep", DeployStepState.Skipped, "Skipped by policy"));

        await step.ExecuteAsync(context);

        Assert.Equal(DeployStepState.Skipped, emitted[^1].State);
        Assert.Equal("Skipped by policy", emitted[^1].Message);
    }
}
