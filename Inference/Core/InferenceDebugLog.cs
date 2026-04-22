using System;
using System.IO;
using System.Reflection;

namespace Inference.Core;

///////////////////////////////////////////////////////////////////////////////////////////////
// InferenceDebugLog
//
// Static logging class for general debug messages in the Inference tool.
// Writes to both a UI callback and a log file (inference_debug.log).
// This is separate from Glass.Core.DebugLog to avoid filename and state conflicts.
///////////////////////////////////////////////////////////////////////////////////////////////
public static class InferenceDebugLog
{
    private static volatile Action<string>? _log;
    private static StreamWriter? _fileWriter;
    private static readonly object _fileLock = new object();

    ///////////////////////////////////////////////////////////////////////////////////////////
    // Initialize
    //
    // Wires up the debug log output action and opens the log file.
    // The log file is written alongside the executable and cleared on each startup.
    //
    // log:  Action that writes a message to the debug log UI.
    ///////////////////////////////////////////////////////////////////////////////////////////
    public static void Initialize(Action<string> log)
    {
        _log = log;

        string exeDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
        string logPath = Path.Combine(exeDirectory, "inference_debug.log");

        lock (_fileLock)
        {
            _fileWriter = new StreamWriter(logPath, append: false);
            _fileWriter.AutoFlush = true;
        }

        Write("InferenceDebugLog: log file opened at " + logPath);
    }

    ///////////////////////////////////////////////////////////////////////////////////////////
    // Shutdown
    //
    // Shuts down the debug log, flushing and closing the log file.
    ///////////////////////////////////////////////////////////////////////////////////////////
    public static void Shutdown()
    {
        Write("InferenceDebugLog: shutting down, closing log file.");

        _log = null;

        lock (_fileLock)
        {
            if (_fileWriter != null)
            {
                _fileWriter.Flush();
                _fileWriter.Close();
                _fileWriter = null;
            }
        }
    }

    ///////////////////////////////////////////////////////////////////////////////////////////
    // Write
    //
    // Writes a timestamped message to the debug log.
    // Writes to the log file always. Writes to the UI callback if one is registered.
    //
    // message:  The message to write.
    ///////////////////////////////////////////////////////////////////////////////////////////
    public static void Write(string message)
    {
        string timestamped = DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message;

        _log?.Invoke(timestamped);

        lock (_fileLock)
        {
            if (_fileWriter != null)
            {
                _fileWriter.WriteLine(timestamped);
            }
        }
    }
}
