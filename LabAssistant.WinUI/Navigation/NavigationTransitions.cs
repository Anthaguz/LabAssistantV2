using Microsoft.UI.Xaml.Media.Animation;

namespace LabAssistant.WinUI.Navigation;

/// <summary>
/// Predefined navigation transition animations.
/// </summary>
public static class NavigationTransitions
{
    /// <summary>
    /// Gets the default transition.
    /// </summary>
    public static NavigationTransitionInfo Default => new EntranceNavigationTransitionInfo();

    /// <summary>
    /// Gets a slide transition entering from the right.
    /// </summary>
    public static NavigationTransitionInfo SlideFromRight => new SlideNavigationTransitionInfo
    {
        Effect = SlideNavigationTransitionEffect.FromRight
    };

    /// <summary>
    /// Gets a slide transition entering from the left.
    /// </summary>
    public static NavigationTransitionInfo SlideFromLeft => new SlideNavigationTransitionInfo
    {
        Effect = SlideNavigationTransitionEffect.FromLeft
    };

    /// <summary>
    /// Gets a drill-in transition.
    /// </summary>
    public static NavigationTransitionInfo DrillIn => new DrillInNavigationTransitionInfo();

    /// <summary>
    /// Gets a transition that suppresses animation.
    /// </summary>
    public static NavigationTransitionInfo Suppress => new SuppressNavigationTransitionInfo();
}
