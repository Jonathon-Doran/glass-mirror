using Glass.Core;
using Glass.Core.Logging;
using System.IO;

namespace Glass.ClientUI;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// IniFileReader
//
// Reads an INI file into a dictionary of section name -> list of content lines.
// Section names include the brackets, e.g. "[KeyMaps]".
// If the file does not exist, returns an empty dictionary.
// Lines before the first section header are discarded.
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
public static class IniFileReader
{
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Read
    //
    // Reads the INI file at the given path and returns its contents as an ordered dictionary
    // of section name -> lines. Section names include brackets. Returns empty if file not found.
    //
    // path:  Full path to the INI file to read
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public static Dictionary<string, List<string>> Read(string path)
    {
        DebugLog.Write(LogChannel.Database, $"IniFileReader.Read: path='{path}'.");

        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(path))
        {
            DebugLog.Write(LogChannel.Database, $"IniFileReader.Read: file not found, returning empty.");
            return result;
        }

        string[] lines = File.ReadAllLines(path);
        string? currentSection = null;

        foreach (var line in lines)
        {
            string trimmed = line.Trim();

            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                currentSection = trimmed;

                if (!result.ContainsKey(currentSection))
                {
                    result[currentSection] = new List<string>();
                }

                DebugLog.Write(LogChannel.Database, $"IniFileReader.Read: section '{currentSection}'.");
            }
            else if (currentSection != null)
            {
                result[currentSection].Add(line);
            }
        }

        DebugLog.Write(LogChannel.Database, $"IniFileReader.Read: read {result.Count} sections.");
        return result;
    }
}