using System;
using System.IO;
using System.Reflection;

namespace Glass.Core.Logging;

///////////////////////////////////////////////////////////////////////////////////////////////
// GlassDebugLogHandler
//
// Writes log messages to the glass.log file in the Logs folder under the
// solution directory.  Manages its own StreamWriter and lock.  The file is
// created or overwritten on construction and flushed on every write.
///////////////////////////////////////////////////////////////////////////////////////////////
public class GlassDebugLogHandler : IHandleLogMessages
{
    private StreamWriter? _writer;
    private readonly object _fileLock = new object();

    ///////////////////////////////////////////////////////////////////////////////////////////////
    // GlassDebugLogHandler (constructor)
    //
    // Opens the glass.log file for writing.  Creates the Logs directory if
    // it does not exist.  Overwrites any existing file.
    //
    // solutionDirectory:  The path to the solution directory
    ///////////////////////////////////////////////////////////////////////////////////////////////
    public GlassDebugLogHandler()
    {
        string solutionDirectory = FindSolutionDirectory();
        string logsDirectory = Path.Combine(solutionDirectory, "Logs");
        Directory.CreateDirectory(logsDirectory);

        string logPath = Path.Combine(logsDirectory, "glass.log");

        _writer = new StreamWriter(logPath, append: false);
        _writer.AutoFlush = true;
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////
    // FindSolutionDirectory
    //
    // Walks up from the executable's directory until a .sln file is found.
    // Throws InvalidOperationException if the solution directory cannot be
    // located.
    ///////////////////////////////////////////////////////////////////////////////////////////////
    private string FindSolutionDirectory()
    {
        string? directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        while (directory != null)
        {
            string[] slnFiles = Directory.GetFiles(directory, "*.sln");

            if (slnFiles.Length > 0)
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new InvalidOperationException(
            "GlassDebugLogHandler: could not locate solution directory from "
            + Assembly.GetExecutingAssembly().Location);
    }


    ///////////////////////////////////////////////////////////////////////////////////////////////
    // Write
    //
    // Writes a message to the glass.log file.  Thread-safe via internal lock.
    //
    // message:  The message text to write (already timestamped by DebugLog)
    ///////////////////////////////////////////////////////////////////////////////////////////////
    public void Write(string message)
    {
        lock (_fileLock)
        {
            if (_writer != null)
            {
                _writer.WriteLine(message);
            }
        }
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////
    // Shutdown
    //
    // Flushes and closes the glass.log file.  No further writes will succeed
    // after this call.
    ///////////////////////////////////////////////////////////////////////////////////////////////
    public void Shutdown()
    {
        lock (_fileLock)
        {
            if (_writer != null)
            {
                _writer.Flush();
                _writer.Close();
                _writer = null;
            }
        }
    }
}