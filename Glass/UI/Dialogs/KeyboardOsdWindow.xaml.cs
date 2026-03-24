using Glass.Controls;
using Glass.Core;
using Glass.Data.Models;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace Glass;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// KeyboardOsdWindow
//
// A standalone always-on-top borderless window showing the current page
// bindings for one keyboard device type.
// Created by KeyboardManager on profile load, shown/hidden on trigger.
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
public partial class KeyboardOsdWindow : Window
{
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // KeyboardOsdWindow
    //
    // keyboardType:  The keyboard type — determines the grid layout
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public KeyboardOsdWindow(KeyboardType keyboardType)
    {
        InitializeComponent();
        KeyLayoutControl.Device = keyboardType;
        DebugLog.Write($"KeyboardOsdWindow: created for {keyboardType}.");

        KeyDown += (s, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                Hide();
            }
        };
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // SetPage
    //
    // Updates the OSD to display the given page name and key bindings.
    //
    // pageName:  The name of the active page
    // keys:      The key display data for the page
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void SetPage(string pageName, Dictionary<string, KeyDisplay> keys)
    {
        DebugLog.Write($"KeyboardOsdWindow.SetPage: page='{pageName}'.");

        Dispatcher.Invoke(() =>
        {
            KeyLayoutControl.PageName = pageName;
            KeyLayoutControl.Keys = keys;
        });
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Window_MouseLeftButtonDown
    //
    // Allows the window to be dragged by clicking anywhere on it.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }
}