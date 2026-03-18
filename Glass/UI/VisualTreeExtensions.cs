using System.Windows;
using System.Windows.Media;

namespace Glass;

public static class VisualTreeExtensions
{
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // FindAncestorOrSelf<T>
    //
    // Walks up the visual tree and returns the first ancestor (or self) of type T, or null if none found.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public static T? FindAncestorOrSelf<T>(this DependencyObject element) where T : DependencyObject
    {
        while (element != null)
        {
            if (element is T match)
            {
                return match;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return null;
    }
}