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
}
