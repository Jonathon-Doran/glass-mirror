using Glass.Core;
using System.Windows;
using System.Windows.Controls;

namespace Glass.Controls;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// KeyDisplayControl
//
// A custom control that renders a single key cell in the keyboard layout.
// Visual appearance is driven by VisualStateManager states defined in Generic.xaml.
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
[TemplatePart(Name = PartBorder, Type = typeof(System.Windows.Controls.Border))]
public class KeyDisplayControl : Control
{
    private const string PartBorder = "PART_Border";

    private System.Windows.Controls.Border? _border;

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Static constructor
    //
    // Overrides the default style key so WPF looks for this control's template
    // in Generic.xaml.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    static KeyDisplayControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(KeyDisplayControl),
            new FrameworkPropertyMetadata(typeof(KeyDisplayControl)));
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // OnApplyTemplate
    //
    // Finds named template parts after the template is applied.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public override void OnApplyTemplate()
    {
        DebugLog.Write($"KeyDisplayControl.OnApplyTemplate: keyName='{KeyName}'.");

        base.OnApplyTemplate();

        _border = GetTemplateChild(PartBorder) as System.Windows.Controls.Border;

        DebugLog.Write($"KeyDisplayControl.OnApplyTemplate: border={_border != null}.");
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // KeyName dependency property
    //
    // The physical key identifier, e.g. "G1", "X-14".
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public static readonly DependencyProperty KeyNameProperty =
        DependencyProperty.Register(
            nameof(KeyName),
            typeof(string),
            typeof(KeyDisplayControl),
            new FrameworkPropertyMetadata(string.Empty));

    public string KeyName
    {
        get => (string)GetValue(KeyNameProperty);
        set => SetValue(KeyNameProperty, value);
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Label dependency property
    //
    // The command label assigned to this key. Empty if unbound.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(
            nameof(Label),
            typeof(string),
            typeof(KeyDisplayControl),
            new FrameworkPropertyMetadata(string.Empty));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }
}
