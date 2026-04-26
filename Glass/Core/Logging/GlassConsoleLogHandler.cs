using System;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Glass.Core.Logging;

///////////////////////////////////////////////////////////////////////////////////////////////
// GlassConsoleLogHandler
//
// Writes log messages to the Glass console TextBox in the main window.
// Marshals all writes onto the UI thread via Dispatcher.Invoke.
// The message arrives already timestamped by DebugLog.
///////////////////////////////////////////////////////////////////////////////////////////////
public class GlassConsoleLogHandler : IHandleLogMessages
{
    private TextBox? _consoleOutput;
    private ScrollViewer? _consoleScroller;
    private readonly Dispatcher _dispatcher;

    ///////////////////////////////////////////////////////////////////////////////////////////////
    // GlassConsoleLogHandler (constructor)
    //
    // consoleOutput:    The TextBox that displays log messages
    // consoleScroller:  The ScrollViewer wrapping the TextBox
    ///////////////////////////////////////////////////////////////////////////////////////////////
    public GlassConsoleLogHandler(TextBox consoleOutput, ScrollViewer consoleScroller)
    {
        _consoleOutput = consoleOutput;
        _consoleScroller = consoleScroller;
        _dispatcher = consoleOutput.Dispatcher;
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////
    // Write
    //
    // Appends a message to the console TextBox and scrolls to the bottom.
    // Marshals onto the UI thread via Dispatcher.BeginInvoke to avoid
    // blocking the calling thread.
    //
    // message:  The message text to write (already timestamped by DebugLog)
    ///////////////////////////////////////////////////////////////////////////////////////////////
    public void Write(string message)
    {
        if (_consoleOutput == null)
        {
            return;
        }

        _dispatcher.BeginInvoke(() =>
        {
            if (_consoleOutput != null)
            {
                _consoleOutput.AppendText(message + Environment.NewLine);
                _consoleScroller?.ScrollToBottom();
            }
        });
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////
    // Shutdown
    //
    // Disconnects from the UI controls.  No further writes will be attempted.
    ///////////////////////////////////////////////////////////////////////////////////////////////
    public void Shutdown()
    {
        _consoleOutput = null;
        _consoleScroller = null;
    }
}
