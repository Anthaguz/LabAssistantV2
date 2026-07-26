// Minimal shims for the few Microsoft.UI.Xaml types referenced by source-linked view models.
// The UI.Tests project compiles selected WinUI view-model source files directly (it does not
// reference the WinUI app assembly or the Windows App SDK), so any framework type a linked view
// model touches must be satisfied here. These stand in only for compilation of runtime-independent
// logic; they intentionally carry no behavior.
namespace Microsoft.UI.Xaml;

public enum Visibility
{
    Visible = 0,
    Collapsed = 1
}
