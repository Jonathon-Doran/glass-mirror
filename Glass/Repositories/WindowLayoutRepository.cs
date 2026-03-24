using Glass.Core;
using Glass.Data.Models;
using Microsoft.Data.Sqlite;
using System.Windows;

namespace Glass.Data.Repositories;

// Handles persistence of window layouts and character placements.
public class WindowLayoutRepository
{
    // Returns the next available layout name for a profile, e.g. "Layout3".
    public string GetNextLayoutName(int profileId)
    {
        using var conn = Database.Instance.Connect();
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM WindowLayouts WHERE profile_id = @id";
        cmd.Parameters.AddWithValue("@id", profileId);
        int count = Convert.ToInt32(cmd.ExecuteScalar());

        return $"Layout{count + 1}";
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Save
    //
    // Saves a window layout and its character placements. Overwrites an existing
    // layout with the same name for this profile if one exists.
    // Returns the layout ID.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public int Save(int profileId, string layoutName, List<SlotAssignment> slots, List<MonitorConfig> monitors)
    {
        DebugLog.Write(DebugLog.Log_Database, $"WindowLayoutRepository.Save: profileID={profileId} name='{layoutName}' slots={slots.Count} monitors={monitors.Count}");

        using var conn = Database.Instance.Connect();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            // Check for existing layout with same name.
            using var checkCmd = conn.CreateCommand();
            checkCmd.Transaction = tx;
            checkCmd.CommandText = "SELECT id FROM WindowLayouts WHERE profile_id = @setId AND name = @name";
            checkCmd.Parameters.AddWithValue("@setId", profileId);
            checkCmd.Parameters.AddWithValue("@name", layoutName);
            var existingId = checkCmd.ExecuteScalar();

            int layoutId;
            if (existingId != null)
            {
                layoutId = Convert.ToInt32(existingId);
                DebugLog.Write(DebugLog.Log_Database, $"WindowLayoutRepository.Save: overwriting layoutId={layoutId}");

                using var deleteCmd = conn.CreateCommand();
                deleteCmd.Transaction = tx;
                deleteCmd.CommandText = "DELETE FROM CharacterPlacements WHERE window_layout_id = @id";
                deleteCmd.Parameters.AddWithValue("@id", layoutId);
                deleteCmd.ExecuteNonQuery();
            }
            else
            {
                DebugLog.Write(DebugLog.Log_Database, $"WindowLayoutRepository.Save: inserting new layout profileId={profileId} layoutName='{layoutName}'");

                using var insertCmd = conn.CreateCommand();
                insertCmd.Transaction = tx;
                insertCmd.CommandText = "INSERT INTO WindowLayouts (name, profile_id, machine_id) VALUES (@name, @setId, 0)";
                insertCmd.Parameters.AddWithValue("@name", layoutName);
                insertCmd.Parameters.AddWithValue("@setId", profileId);
                insertCmd.ExecuteNonQuery();

                using var idCmd = conn.CreateCommand();
                idCmd.Transaction = tx;
                idCmd.CommandText = "SELECT last_insert_rowid()";
                layoutId = Convert.ToInt32(idCmd.ExecuteScalar());
            }

            // Build a flat list of slot rectangles across all non-stacked monitors,
            // in slot order (left-to-right, top-to-bottom per monitor).
            var allRects = monitors
                .Where(m => m.MonitorNumber != 1)
                .SelectMany(m => m.SlotRectangles)
                .ToList();

            DebugLog.Write(DebugLog.Log_Database, $"WindowLayoutRepository.Save: {allRects.Count} rectangles across monitors.");

            for (int i = 0; i < slots.Count && i < allRects.Count; i++)
            {
                var slot = slots[i];
                var rect = allRects[i];

                DebugLog.Write(DebugLog.Log_Database, $"WindowLayoutRepository.Save: slot={slot.SlotNumber} characterId={slot.CharacterId} x={rect.X} y={rect.Y} w={rect.Width} h={rect.Height}");
                using var insertPlacement = conn.CreateCommand();
                insertPlacement.Transaction = tx;
                insertPlacement.CommandText = @"
                    INSERT INTO CharacterPlacements (window_layout_id, character_id, x, y, width, height)
                    VALUES (@layoutId, @charId, @x, @y, @width, @height)";
                insertPlacement.Parameters.AddWithValue("@layoutId", layoutId);
                insertPlacement.Parameters.AddWithValue("@charId", slot.CharacterId);
                insertPlacement.Parameters.AddWithValue("@x", (int)rect.X);
                insertPlacement.Parameters.AddWithValue("@y", (int)rect.Y);
                insertPlacement.Parameters.AddWithValue("@width", (int)rect.Width);
                insertPlacement.Parameters.AddWithValue("@height", (int)rect.Height);
                insertPlacement.ExecuteNonQuery();
            }

            // Upsert monitor dimensions for each monitor in the layout.
            foreach (var monitor in monitors)
            {
                DebugLog.Write(DebugLog.Log_Database, $"WindowLayoutRepository.Save: upserting monitor '{monitor.DeviceName}' {monitor.MonitorWidth}x{monitor.MonitorHeight}.");

                using var monitorCmd = conn.CreateCommand();
                monitorCmd.Transaction = tx;
                monitorCmd.CommandText = @"
                        INSERT INTO Monitors (machine_id, display_name, width, height, orientation)
                        VALUES (0, @displayName, @width, @height, 0)
                        ON CONFLICT(machine_id, display_name) DO UPDATE SET
                            width  = excluded.width,
                            height = excluded.height";
                monitorCmd.Parameters.AddWithValue("@displayName", monitor.DeviceName);
                monitorCmd.Parameters.AddWithValue("@width", monitor.MonitorWidth);
                monitorCmd.Parameters.AddWithValue("@height", monitor.MonitorHeight);
                monitorCmd.ExecuteNonQuery();
            }

            tx.Commit();
            DebugLog.Write(DebugLog.Log_Database, $"WindowLayoutRepository.Save: committed layoutId={layoutId}");
            return layoutId;
        }
        catch (Exception ex)
        {
            DebugLog.Write(DebugLog.Log_Database, $"WindowLayoutRepository.Save: exception: {ex.Message}, rolling back.");
            tx.Rollback();
            throw;
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // GetLayout
    //
    // Returns the slot placements for the first layout associated with the given profile name.
    // Returns an empty list if no layout exists.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public List<SlotPlacement> GetLayout(string profileName)
    {
        DebugLog.Write(DebugLog.Log_Database, $"WindowLayoutRepository.GetLayout: profileName='{profileName}'");

        using var conn = Database.Instance.Connect();
        conn.Open();

        using var layoutCmd = conn.CreateCommand();
        layoutCmd.CommandText = @"
            SELECT wl.id 
            FROM WindowLayouts wl
            JOIN Profiles ps ON ps.id = wl.profile_id
            WHERE ps.name = @name
            ORDER BY wl.id
            LIMIT 1";
        layoutCmd.Parameters.AddWithValue("@name", profileName);
        var layoutId = layoutCmd.ExecuteScalar();

        if (layoutId == null)
        {
            DebugLog.Write(DebugLog.Log_Database, $"WindowLayoutRepository.GetLayout: no layout found for profile '{profileName}'.");
            return new List<SlotPlacement>();
        }

        int id = Convert.ToInt32(layoutId);
        DebugLog.Write(DebugLog.Log_Database, $"WindowLayoutRepository.GetLayout: layoutId={id}");

        using var placementCmd = conn.CreateCommand();
        placementCmd.CommandText = @"
            SELECT profSlot.slot_number, cp.x, cp.y, cp.width, cp.height
            FROM CharacterPlacements cp
            JOIN ProfileSlots profSlot ON profSlot.character_id = cp.character_id
                AND profSlot.profile_id = (
                    SELECT profile_id FROM WindowLayouts WHERE id = @layoutId)
            WHERE cp.window_layout_id = @layoutId
            ORDER BY profSlot.slot_number";
        placementCmd.Parameters.AddWithValue("@layoutId", id);

        var placements = new List<SlotPlacement>();
        using var reader = placementCmd.ExecuteReader();
        while (reader.Read())
        {
            placements.Add(new SlotPlacement
            {
                SlotNumber = reader.GetInt32(0),
                X = reader.GetInt32(1),
                Y = reader.GetInt32(2),
                Width = reader.GetInt32(3),
                Height = reader.GetInt32(4)
            });
            DebugLog.Write(DebugLog.Log_Database, $"WindowLayoutRepository.GetLayout: slot={placements[^1].SlotNumber} x={placements[^1].X} y={placements[^1].Y} w={placements[^1].Width} h={placements[^1].Height}");
        }

        DebugLog.Write(DebugLog.Log_Database, $"WindowLayoutRepository.GetLayout: returned {placements.Count} placements.");
        return placements;
    }
}
