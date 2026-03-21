using Glass.Core;
using Glass.Data.Models;
using Microsoft.Data.Sqlite;

namespace Glass.Data.Repositories;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// MonitorRepository
//
// Provides access to monitor records in the database.
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
public class MonitorRepository
{
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // GetFirstMonitor
    //
    // Returns the width and height of the first monitor ordered by id,
    // or null if none exist.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public (int Width, int Height)? GetFirstMonitor()
    {
        DebugLog.Write(DebugLog.Log_Database, "MonitorRepository.GetFirstMonitor: loading.");

        using var conn = Database.Instance.Connect();
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT width, height
            FROM Monitors
            ORDER BY id
            LIMIT 1";

        using var reader = cmd.ExecuteReader();

        if (!reader.Read())
        {
            DebugLog.Write(DebugLog.Log_Database, "MonitorRepository.GetFirstMonitor: no monitors found.");
            return null;
        }

        int width = reader.GetInt32(0);
        int height = reader.GetInt32(1);

        DebugLog.Write(DebugLog.Log_Database, $"MonitorRepository.GetFirstMonitor: found {width}x{height}.");
        return (width, height);
    }
}