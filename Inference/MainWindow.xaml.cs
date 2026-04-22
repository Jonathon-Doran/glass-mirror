using Glass.Core;
using Inference.Core;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Inference.Dialogs;

namespace Inference;

///////////////////////////////////////////////////////////////////////////////////////////////
// MainWindow
//
// Main window for the Inference tool.
///////////////////////////////////////////////////////////////////////////////////////////////
public partial class MainWindow : Window
{
    private bool _hasPatchLevel = false;
    private bool _hasUnsavedChanges = false;
    private readonly Stack<object> _undoStack = new Stack<object>();

    ///////////////////////////////////////////////////////////////////////////////////////////
    // MainWindow
    //
    // Constructs the main window and initializes the XAML-defined components.
    ///////////////////////////////////////////////////////////////////////////////////////////
    public MainWindow()
    {
        InitializeComponent();

        InferenceDebugLog.Initialize(WriteToDebugLog);
        InferenceLog.Initialize(WriteToInferenceLog);

        InferenceDebugLog.Write("Inference application started");
        InferenceLog.Write("Inference log initialized");
        UpdateControlStates();
    }

    ///////////////////////////////////////////////////////////////////////////////////////////
    // Window_Closing
    //
    // Handles the window closing event. Shuts down logging.
    //
    // sender:  The window being closed.
    // e:       Event arguments.
    ///////////////////////////////////////////////////////////////////////////////////////////
    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        InferenceDebugLog.Write("Inference application closing");
        InferenceLog.Shutdown();
        InferenceDebugLog.Shutdown();
    }

    ///////////////////////////////////////////////////////////////////////////////////////////
    // UpdateControlStates
    //
    // Evaluates the current application state and enables or disables controls
    // according to context rules. Called whenever state changes that could affect
    // control availability.
    //
    // Rules:
    //   Launch Profile:  enabled when a patch level is loaded
    //   Save:            enabled when a patch level is loaded and unsaved changes exist
    //   Undo:            enabled when the undo stack is not empty
    //   Analyze:         enabled when a patch level is loaded and an opcode row is selected
    //   Accept:          enabled when a candidate row is selected
    ///////////////////////////////////////////////////////////////////////////////////////////
    private void UpdateControlStates()
    {
        bool hasPatchLevel = _hasPatchLevel;
        bool hasOpcodeSelected = OpcodeGrid.SelectedItem != null;
        bool hasCandidateSelected = CandidateGrid.SelectedItem != null;
        bool hasUndoHistory = _undoStack.Count > 0;

        MenuLaunchProfile.IsEnabled = hasPatchLevel;
        MenuSave.IsEnabled = hasPatchLevel && _hasUnsavedChanges;
        MenuUndo.IsEnabled = hasUndoHistory;
        ButtonAnalyze.IsEnabled = hasPatchLevel && hasOpcodeSelected;
        ToggleAccept.IsEnabled = hasCandidateSelected;
    }

    ///////////////////////////////////////////////////////////////////////////////////////////
    // OpcodeGrid_SelectionChanged
    //
    // Handles selection changes in the Opcodes data grid.
    // Updates control states to reflect whether an opcode is selected.
    //
    // sender:  The data grid that raised the event.
    // e:       Event arguments.
    ///////////////////////////////////////////////////////////////////////////////////////////
    private void OpcodeGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        InferenceDebugLog.Write("OpcodeGrid_SelectionChanged");
        UpdateControlStates();
    }

    ///////////////////////////////////////////////////////////////////////////////////////////
    // CandidateGrid_SelectionChanged
    //
    // Handles selection changes in the Candidate data grid.
    // Updates control states to reflect whether a candidate is selected.
    //
    // sender:  The data grid that raised the event.
    // e:       Event arguments.
    ///////////////////////////////////////////////////////////////////////////////////////////
    private void CandidateGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        InferenceDebugLog.Write("CandidateGrid_SelectionChanged");
        UpdateControlStates();
    }

    ///////////////////////////////////////////////////////////////////////////////////////////
    // MenuItem_NewPatchLevel_Click
    //
    // Handles the File > New Patch Level menu item click.
    // Opens the New Patch Level dialog. If the user confirms, creates a new patch
    // level entry and sets it as the current working patch level.
    //
    // sender:  The menu item that raised the event.
    // e:       Event arguments.
    ///////////////////////////////////////////////////////////////////////////////////////////
    private void MenuItem_NewPatchLevel_Click(object sender, RoutedEventArgs e)
    {
        InferenceDebugLog.Write("MenuItem_NewPatchLevel_Click");

        NewPatchLevelDialog dialog = new NewPatchLevelDialog();
        dialog.Owner = this;

        if (dialog.ShowDialog() == true)
        {
            InferenceDebugLog.Write("New patch level created: ServerType="
                + dialog.ServerType + " PatchDate=" + dialog.PatchDate.ToString("yyyy-MM-dd"));

            _hasPatchLevel = true;
            StatusPatchLevel.Text = dialog.ServerType + " " + dialog.PatchDate.ToString("yyyy-MM-dd");
            UpdateControlStates();
        }
    }

    ///////////////////////////////////////////////////////////////////////////////////////////
    // MenuItem_LaunchProfile_Click
    //
    // Handles the File > Launch Profile menu item click.
    // Launches an EQ client profile through Inner Space.
    //
    // sender:  The menu item that raised the event.
    // e:       Event arguments.
    ///////////////////////////////////////////////////////////////////////////////////////////
    private void MenuItem_LaunchProfile_Click(object sender, RoutedEventArgs e)
    {
        InferenceDebugLog.Write("MenuItem_LaunchProfile_Click");
    }

    ///////////////////////////////////////////////////////////////////////////////////////////
    // MenuItem_Save_Click
    //
    // Handles the File > Save menu item click.
    // Persists the current patch level state to the database.
    //
    // sender:  The menu item that raised the event.
    // e:       Event arguments.
    ///////////////////////////////////////////////////////////////////////////////////////////
    private void MenuItem_Save_Click(object sender, RoutedEventArgs e)
    {
        InferenceDebugLog.Write("MenuItem_Save_Click");
    }

    ///////////////////////////////////////////////////////////////////////////////////////////
    // MenuItem_Exit_Click
    //
    // Handles the File > Exit menu item click.
    // Closes the application.
    //
    // sender:  The menu item that raised the event.
    // e:       Event arguments.
    ///////////////////////////////////////////////////////////////////////////////////////////
    private void MenuItem_Exit_Click(object sender, RoutedEventArgs e)
    {
        InferenceDebugLog.Write("MenuItem_Exit_Click");
        Close();
    }

    ///////////////////////////////////////////////////////////////////////////////////////////
    // MenuItem_Undo_Click
    //
    // Handles the Edit > Undo menu item click.
    // Reverses the most recent edit operation.
    //
    // sender:  The menu item that raised the event.
    // e:       Event arguments.
    ///////////////////////////////////////////////////////////////////////////////////////////
    private void MenuItem_Undo_Click(object sender, RoutedEventArgs e)
    {
        InferenceDebugLog.Write("MenuItem_Undo_Click");
    }

    ///////////////////////////////////////////////////////////////////////////////////////////
    // Button_Analyze_Click
    //
    // Handles the Analyze button click on the Opcodes tab.
    // Triggers analysis on the currently selected opcode row.
    //
    // sender:  The button that raised the event.
    // e:       Event arguments.
    ///////////////////////////////////////////////////////////////////////////////////////////
    private void Button_Analyze_Click(object sender, RoutedEventArgs e)
    {
        if (OpcodeGrid.SelectedItem == null)
        {
            InferenceDebugLog.Write("Button_Analyze_Click: no opcode selected");
            return;
        }
        InferenceDebugLog.Write("Button_Analyze_Click: analyzing selected opcode");
    }

    ///////////////////////////////////////////////////////////////////////////////////////////
    // ToggleButton_AcceptCandidate_Click
    //
    // Handles the Accept toggle button click on the Analysis tab.
    // Toggles acceptance of the selected candidate identification. When toggled on,
    // the candidate's logical name is applied to the opcode. When toggled off,
    // the identification is reverted.
    //
    // sender:  The toggle button that raised the event.
    // e:       Event arguments.
    ///////////////////////////////////////////////////////////////////////////////////////////
    private void ToggleButton_AcceptCandidate_Click(object sender, RoutedEventArgs e)
    {
        if (CandidateGrid.SelectedItem == null)
        {
            InferenceDebugLog.Write("ToggleButton_AcceptCandidate_Click: no candidate selected");
            return;
        }
        System.Windows.Controls.Primitives.ToggleButton toggle = (System.Windows.Controls.Primitives.ToggleButton)sender;
        bool isAccepted = toggle.IsChecked == true;
        InferenceDebugLog.Write("ToggleButton_AcceptCandidate_Click: accepted=" + isAccepted);
    }

    ///////////////////////////////////////////////////////////////////////////////////////////
    // WriteToDebugLog
    //
    // Callback for DebugLog. Appends a message to the Debug Log list box.
    // Dispatches to the UI thread if called from a background thread.
    //
    // message:  The message to display.
    ///////////////////////////////////////////////////////////////////////////////////////////
    private void WriteToDebugLog(string message)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => WriteToDebugLog(message));
            return;
        }
        DebugLogList.Items.Add(message);
        DebugLogList.ScrollIntoView(DebugLogList.Items[DebugLogList.Items.Count - 1]);
    }

    ///////////////////////////////////////////////////////////////////////////////////////////
    // WriteToInferenceLog
    //
    // Callback for InferenceLog. Appends a message to the Inference Log list box.
    // Dispatches to the UI thread if called from a background thread.
    //
    // message:  The message to display.
    ///////////////////////////////////////////////////////////////////////////////////////////
    private void WriteToInferenceLog(string message)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => WriteToInferenceLog(message));
            return;
        }
        InferenceLogList.Items.Add(message);
        InferenceLogList.ScrollIntoView(InferenceLogList.Items[InferenceLogList.Items.Count - 1]);
    }
}