using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace WhereWindsMeetMidiPlayer.Helpers;

/// <summary>
/// Walks visual and logical trees. <see cref="VisualTreeHelper.GetParent"/> throws on
/// <see cref="System.Windows.Documents.Run"/> and other non-visual content elements.
/// </summary>
internal static class DependencyTreeHelper
{
    public static DependencyObject? GetParent(DependencyObject? node)
    {
        if (node is null)
            return null;

        if (node is Visual or Visual3D)
            return VisualTreeHelper.GetParent(node);

        if (node is FrameworkContentElement contentElement)
            return contentElement.Parent;

        return null;
    }

    public static T? FindAncestor<T>(DependencyObject? node) where T : DependencyObject
    {
        while (node is not null)
        {
            if (node is T match)
                return match;

            node = GetParent(node);
        }

        return null;
    }
}
