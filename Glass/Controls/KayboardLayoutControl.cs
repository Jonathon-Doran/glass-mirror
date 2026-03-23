using Glass.Core;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Glass.Controls;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// KeyboardLayoutControl
//
// A custom control that renders a physical keyboard layout for a given device type.
// Displays key labels or key names depending on ShowLabel.
// Fires events when keys are pressed, released, or toggled.
// The control has no knowledge of bindings, pages, or profiles.
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

[TemplatePart(Name = PartG13Grid, Type = typeof(Grid))]
[TemplatePart(Name = PartG15Grid, Type = typeof(Grid))]
[TemplatePart(Name = PartX36Grid, Type = typeof(Grid))]
public class KeyboardLayoutControl : Control
{
    private const string PartG13Grid = "PART_G13Grid";
    private const string PartG15Grid = "PART_G15Grid";
    private const string PartX36Grid = "PART_X36Grid";

    private Grid? _g13Grid;
    private Grid? _g15Grid;
    private Grid? _x36Grid;

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Static constructor
    //
    // Overrides the default style key so WPF looks for this control's template
    // in Generic.xaml.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    static KeyboardLayoutControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(KeyboardLayoutControl),
            new FrameworkPropertyMetadata(typeof(KeyboardLayoutControl)));
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // OnApplyTemplate
    //
    // Called by WPF after the control template is applied.
    // Finds named template parts and wires up initial state.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public override void OnApplyTemplate()
    {
        DebugLog.Write($"KeyboardLayoutControl.OnApplyTemplate: type='{KeyboardType}'.");

        base.OnApplyTemplate();

        _g13Grid = GetTemplateChild(PartG13Grid) as Grid;
        _g15Grid = GetTemplateChild(PartG15Grid) as Grid;
        _x36Grid = GetTemplateChild(PartX36Grid) as Grid;

        DebugLog.Write($"KeyboardLayoutControl.OnApplyTemplate: g13Grid={_g13Grid != null} g15Grid={_g15Grid != null} x36Grid={_x36Grid != null}.");

        RebuildGrid();
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // KeyboardType dependency property
    //
    // The device type to render. Determines the grid layout.
    // Valid values: "G13", "G15", "Dominator X36"
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public static readonly DependencyProperty KeyboardTypeProperty =
        DependencyProperty.Register(
            nameof(KeyboardType),
            typeof(string),
            typeof(KeyboardLayoutControl),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.AffectsMeasure |
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnKeyboardTypeChanged));

    public string KeyboardType
    {
        get => (string)GetValue(KeyboardTypeProperty);
        set => SetValue(KeyboardTypeProperty, value);
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // OnKeyboardTypeChanged
    //
    // Called when the KeyboardType property changes.
    // Triggers a rebuild of the key grid.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private static void OnKeyboardTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (KeyboardLayoutControl)d;
        DebugLog.Write($"KeyboardLayoutControl.OnKeyboardTypeChanged: type='{e.NewValue}'.");
        control.RebuildGrid();
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Keys dependency property
    //
    // The collection of key display states to render.
    // The caller populates this and updates it when state changes.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public static readonly DependencyProperty KeysProperty =
        DependencyProperty.Register(
            nameof(Keys),
            typeof(IEnumerable<KeyDisplay>),
            typeof(KeyboardLayoutControl),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnKeysChanged));

    public IEnumerable<KeyDisplay> Keys
    {
        get => (IEnumerable<KeyDisplay>)GetValue(KeysProperty);
        set => SetValue(KeysProperty, value);
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // PageName dependency property
    //
    // The name of the currently active page, displayed in the header.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public static readonly DependencyProperty PageNameProperty =
        DependencyProperty.Register(
            nameof(PageName),
            typeof(string),
            typeof(KeyboardLayoutControl),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public string PageName
    {
        get => (string)GetValue(PageNameProperty);
        set => SetValue(PageNameProperty, value);
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // OnKeysChanged
    //
    // Called when the Keys collection is replaced.
    // Triggers a refresh of all key cell appearances.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private static void OnKeysChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (KeyboardLayoutControl)d;
        DebugLog.Write($"KeyboardLayoutControl.OnKeysChanged.");
        control.RefreshKeys();
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // ShowLabel dependency property
    //
    // When true, key cells display the Label from KeyDisplay.
    // When false, key cells display the KeyName.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public static readonly DependencyProperty ShowLabelProperty =
        DependencyProperty.Register(
            nameof(ShowLabel),
            typeof(bool),
            typeof(KeyboardLayoutControl),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnShowLabelChanged));

    public bool ShowLabel
    {
        get => (bool)GetValue(ShowLabelProperty);
        set => SetValue(ShowLabelProperty, value);
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // OnShowLabelChanged
    //
    // Called when ShowLabel changes. Refreshes all key cell text.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private static void OnShowLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (KeyboardLayoutControl)d;
        DebugLog.Write($"KeyboardLayoutControl.OnShowLabelChanged: showLabel='{e.NewValue}'.");
        control.RefreshKeys();
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // SelectionEnabled dependency property
    //
    // When false, key activation events are not fired.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public static readonly DependencyProperty SelectionEnabledProperty =
        DependencyProperty.Register(
            nameof(SelectionEnabled),
            typeof(bool),
            typeof(KeyboardLayoutControl),
            new FrameworkPropertyMetadata(true));

    public bool SelectionEnabled
    {
        get => (bool)GetValue(SelectionEnabledProperty);
        set => SetValue(SelectionEnabledProperty, value);
    }
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // KeyPressed routed event
    //
    // Fired when a key cell receives a mouse down event.
    // Carries the key name and IsPressed=true.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public static readonly RoutedEvent KeyPressedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(KeyPressed),
            RoutingStrategy.Bubble,
            typeof(LayoutEventHandler),
            typeof(KeyboardLayoutControl));

    public event LayoutEventHandler KeyPressed
    {
        add => AddHandler(KeyPressedEvent, value);
        remove => RemoveHandler(KeyPressedEvent, value);
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // KeyReleased routed event
    //
    // Fired when the mouse button is released after a key press.
    // Fires regardless of cursor position at release time.
    // Carries the key name and IsPressed=false.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public static readonly RoutedEvent KeyReleasedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(KeyReleased),
            RoutingStrategy.Bubble,
            typeof(LayoutEventHandler),
            typeof(KeyboardLayoutControl));

    public event LayoutEventHandler KeyReleased
    {
        add => AddHandler(KeyReleasedEvent, value);
        remove => RemoveHandler(KeyReleasedEvent, value);
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // RebuildGrid
    //
    // Shows the correct keyboard grid based on the current KeyboardType.
    // Safe to call before OnApplyTemplate — null checks guard against missing parts.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void RebuildGrid()
    {
        DebugLog.Write($"KeyboardLayoutControl.RebuildGrid: type='{KeyboardType}'.");

        if ((_g13Grid == null) && (_g15Grid == null) && (_x36Grid == null))
        {
            DebugLog.Write("KeyboardLayoutControl.RebuildGrid: template not yet applied, deferring.");
            return;
        }

        if (_g13Grid != null)
        {
            _g13Grid.Visibility = (KeyboardType == "G13") ? Visibility.Visible : Visibility.Collapsed;
        }

        if (_g15Grid != null)
        {
            _g15Grid.Visibility = (KeyboardType == "G15") ? Visibility.Visible : Visibility.Collapsed;
        }

        if (_x36Grid != null)
        {
            _x36Grid.Visibility = (KeyboardType == "Dominator X36") ? Visibility.Visible : Visibility.Collapsed;
        }

        DebugLog.Write($"KeyboardLayoutControl.RebuildGrid: complete.");

        RefreshKeys();
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // RefreshKeys
    //
    // Refreshes key cell appearances when Keys or ShowLabel changes.
    // Implementation will be added when the template is defined.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void RefreshKeys()
    {
        DebugLog.Write($"KeyboardLayoutControl.RefreshKeys.");
    }
}
