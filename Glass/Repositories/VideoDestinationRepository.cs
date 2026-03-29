using Glass.Core;
using Glass.Data.Models;
using Microsoft.Data.Sqlite;

namespace Glass.Data.Repositories;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// VideoDestinationRepository
//
// Handles persistence of VideoDestination records.
// VideoDestinations are per-profile and define slot-relative render coordinates
// for each named VideoSource region. All slots in a profile share the same offsets.
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
public class VideoDestinationRepository
{
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // GetForProfile
    //
    // Returns all VideoDestination records for the given profile ID.
    //
    // profileId:  The profile to load destinations for.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public List<VideoDestination> GetForProfile(int profileId)
    {
        DebugLog.Write(DebugLog.Log_Database, $"VideoDestinationRepository.GetForProfile: profileId={profileId}.");

        List<VideoDestination> destinations = new List<VideoDestination>();

        using SqliteConnection conn = Database.Instance.Connect();
        conn.Open();

        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, profile_id, source_id, x, y, width, height
            FROM VideoDestinations
            WHERE profile_id = @profileId";
        cmd.Parameters.AddWithValue("@profileId", profileId);

        using SqliteDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            VideoDestination dest = new VideoDestination
            {
                Id = reader.GetInt32(0),
                ProfileId = reader.GetInt32(1),
                SourceId = reader.GetInt32(2),
                X = reader.GetInt32(3),
                Y = reader.GetInt32(4),
                Width = reader.GetInt32(5),
                Height = reader.GetInt32(6)
            };
            destinations.Add(dest);
            DebugLog.Write(DebugLog.Log_Database, $"VideoDestinationRepository.GetForProfile: loaded id={dest.Id} sourceId={dest.SourceId} x={dest.X} y={dest.Y} w={dest.Width} h={dest.Height}.");
        }

        DebugLog.Write(DebugLog.Log_Database, $"VideoDestinationRepository.GetForProfile: {destinations.Count} destinations loaded.");
        return destinations;
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Save
    //
    // Inserts or updates a VideoDestination. If the destination has Id == 0 it is
    // inserted and the new ID is assigned back to the model. Otherwise the existing
    // row is updated.
    //
    // destination:  The destination to save.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void Save(VideoDestination destination)
    {
        DebugLog.Write(DebugLog.Log_Database, $"VideoDestinationRepository.Save: id={destination.Id} profileId={destination.ProfileId} sourceId={destination.SourceId} x={destination.X} y={destination.Y} w={destination.Width} h={destination.Height}.");

        using SqliteConnection conn = Database.Instance.Connect();
        conn.Open();

        using SqliteTransaction tx = conn.BeginTransaction();
        try
        {
            using SqliteCommand cmd = conn.CreateCommand();
            cmd.Transaction = tx;

            if (destination.Id == 0)
            {
                DebugLog.Write(DebugLog.Log_Database, "VideoDestinationRepository.Save: inserting new destination.");
                cmd.CommandText = @"
                    INSERT INTO VideoDestinations (profile_id, source_id, x, y, width, height)
                    VALUES (@profileId, @sourceId, @x, @y, @width, @height);
                    SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("@profileId", destination.ProfileId);
                cmd.Parameters.AddWithValue("@sourceId", destination.SourceId);
                cmd.Parameters.AddWithValue("@x", destination.X);
                cmd.Parameters.AddWithValue("@y", destination.Y);
                cmd.Parameters.AddWithValue("@width", destination.Width);
                cmd.Parameters.AddWithValue("@height", destination.Height);
                destination.Id = Convert.ToInt32(cmd.ExecuteScalar());
                DebugLog.Write(DebugLog.Log_Database, $"VideoDestinationRepository.Save: inserted, new id={destination.Id}.");
            }
            else
            {
                DebugLog.Write(DebugLog.Log_Database, $"VideoDestinationRepository.Save: updating id={destination.Id}.");
                cmd.CommandText = @"
                    UPDATE VideoDestinations
                    SET profile_id = @profileId, source_id = @sourceId,
                        x = @x, y = @y, width = @width, height = @height
                    WHERE id = @id";
                cmd.Parameters.AddWithValue("@id", destination.Id);
                cmd.Parameters.AddWithValue("@profileId", destination.ProfileId);
                cmd.Parameters.AddWithValue("@sourceId", destination.SourceId);
                cmd.Parameters.AddWithValue("@x", destination.X);
                cmd.Parameters.AddWithValue("@y", destination.Y);
                cmd.Parameters.AddWithValue("@width", destination.Width);
                cmd.Parameters.AddWithValue("@height", destination.Height);
                cmd.ExecuteNonQuery();
                DebugLog.Write(DebugLog.Log_Database, $"VideoDestinationRepository.Save: updated id={destination.Id}.");
            }

            tx.Commit();
            DebugLog.Write(DebugLog.Log_Database, $"VideoDestinationRepository.Save: committed id={destination.Id}.");
        }
        catch (Exception ex)
        {
            DebugLog.Write(DebugLog.Log_Database, $"VideoDestinationRepository.Save: exception: {ex.Message}, rolling back.");
            tx.Rollback();
            throw;
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Delete
    //
    // Deletes the VideoDestination with the given ID.
    //
    // id:  The ID of the destination to delete.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void Delete(int id)
    {
        DebugLog.Write(DebugLog.Log_Database, $"VideoDestinationRepository.Delete: id={id}.");

        using SqliteConnection conn = Database.Instance.Connect();
        conn.Open();

        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM VideoDestinations WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        int rows = cmd.ExecuteNonQuery();

        DebugLog.Write(DebugLog.Log_Database, $"VideoDestinationRepository.Delete: {rows} row(s) deleted for id={id}.");
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DeleteAllForProfile
    //
    // Deletes all VideoDestinations for the given profile. Useful when rebuilding
    // the full destination set for a profile in one operation.
    //
    // profileId:  The profile whose destinations should be deleted.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void DeleteAllForProfile(int profileId)
    {
        DebugLog.Write(DebugLog.Log_Database, $"VideoDestinationRepository.DeleteAllForProfile: profileId={profileId}.");

        using SqliteConnection conn = Database.Instance.Connect();
        conn.Open();

        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM VideoDestinations WHERE profile_id = @profileId";
        cmd.Parameters.AddWithValue("@profileId", profileId);
        int rows = cmd.ExecuteNonQuery();

        DebugLog.Write(DebugLog.Log_Database, $"VideoDestinationRepository.DeleteAllForProfile: {rows} row(s) deleted for profileId={profileId}.");
    }
}