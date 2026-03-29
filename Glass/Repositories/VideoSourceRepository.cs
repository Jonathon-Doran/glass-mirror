using Glass.Core;
using Glass.Data.Models;
using Microsoft.Data.Sqlite;

namespace Glass.Data.Repositories;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// VideoSourceRepository
//
// Handles persistence of VideoSource records.
// VideoSources are profile-independent — they form a global catalog of
// named regions within a captured EQ client window.
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
public class VideoSourceRepository
{
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // GetAll
    //
    // Returns all VideoSource records from the database, ordered by name.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public List<VideoSource> GetAll()
    {
        DebugLog.Write(DebugLog.Log_Database, "VideoSourceRepository.GetAll: loading all video sources.");

        List<VideoSource> sources = new List<VideoSource>();

        using SqliteConnection conn = Database.Instance.Connect();
        conn.Open();

        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, x, y, width, height FROM VideoSources ORDER BY name";

        using SqliteDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            VideoSource source = new VideoSource
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                X = reader.GetInt32(2),
                Y = reader.GetInt32(3),
                Width = reader.GetInt32(4),
                Height = reader.GetInt32(5)
            };
            sources.Add(source);
            DebugLog.Write(DebugLog.Log_Database, $"VideoSourceRepository.GetAll: loaded source id={source.Id} name='{source.Name}' x={source.X} y={source.Y} w={source.Width} h={source.Height}.");
        }

        DebugLog.Write(DebugLog.Log_Database, $"VideoSourceRepository.GetAll: {sources.Count} sources loaded.");
        return sources;
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // GetById
    //
    // Returns the VideoSource with the given ID, or null if not found.
    //
    // id:  The ID of the source to load.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public VideoSource? GetById(int id)
    {
        DebugLog.Write(DebugLog.Log_Database, $"VideoSourceRepository.GetById: id={id}.");

        using SqliteConnection conn = Database.Instance.Connect();
        conn.Open();

        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, x, y, width, height FROM VideoSources WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);

        using SqliteDataReader reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            DebugLog.Write(DebugLog.Log_Database, $"VideoSourceRepository.GetById: id={id} not found.");
            return null;
        }

        VideoSource source = new VideoSource
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            X = reader.GetInt32(2),
            Y = reader.GetInt32(3),
            Width = reader.GetInt32(4),
            Height = reader.GetInt32(5)
        };

        DebugLog.Write(DebugLog.Log_Database, $"VideoSourceRepository.GetById: loaded name='{source.Name}' x={source.X} y={source.Y} w={source.Width} h={source.Height}.");
        return source;
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Save
    //
    // Inserts or updates a VideoSource. If the source has Id == 0 it is inserted
    // and the new ID is assigned back to the model. Otherwise the existing row
    // is updated.
    //
    // source:  The source to save.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void Save(VideoSource source)
    {
        DebugLog.Write(DebugLog.Log_Database, $"VideoSourceRepository.Save: id={source.Id} name='{source.Name}' x={source.X} y={source.Y} w={source.Width} h={source.Height}.");

        using SqliteConnection conn = Database.Instance.Connect();
        conn.Open();

        using SqliteTransaction tx = conn.BeginTransaction();
        try
        {
            using SqliteCommand cmd = conn.CreateCommand();
            cmd.Transaction = tx;

            if (source.Id == 0)
            {
                DebugLog.Write(DebugLog.Log_Database, "VideoSourceRepository.Save: inserting new source.");
                cmd.CommandText = @"
                    INSERT INTO VideoSources (name, x, y, width, height)
                    VALUES (@name, @x, @y, @width, @height);
                    SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("@name", source.Name);
                cmd.Parameters.AddWithValue("@x", source.X);
                cmd.Parameters.AddWithValue("@y", source.Y);
                cmd.Parameters.AddWithValue("@width", source.Width);
                cmd.Parameters.AddWithValue("@height", source.Height);
                source.Id = Convert.ToInt32(cmd.ExecuteScalar());
                DebugLog.Write(DebugLog.Log_Database, $"VideoSourceRepository.Save: inserted, new id={source.Id}.");
            }
            else
            {
                DebugLog.Write(DebugLog.Log_Database, $"VideoSourceRepository.Save: updating id={source.Id}.");
                cmd.CommandText = @"
                    UPDATE VideoSources
                    SET name = @name, x = @x, y = @y, width = @width, height = @height
                    WHERE id = @id";
                cmd.Parameters.AddWithValue("@id", source.Id);
                cmd.Parameters.AddWithValue("@name", source.Name);
                cmd.Parameters.AddWithValue("@x", source.X);
                cmd.Parameters.AddWithValue("@y", source.Y);
                cmd.Parameters.AddWithValue("@width", source.Width);
                cmd.Parameters.AddWithValue("@height", source.Height);
                cmd.ExecuteNonQuery();
                DebugLog.Write(DebugLog.Log_Database, $"VideoSourceRepository.Save: updated id={source.Id}.");
            }

            tx.Commit();
            DebugLog.Write(DebugLog.Log_Database, $"VideoSourceRepository.Save: committed id={source.Id}.");
        }
        catch (Exception ex)
        {
            DebugLog.Write(DebugLog.Log_Database, $"VideoSourceRepository.Save: exception: {ex.Message}, rolling back.");
            tx.Rollback();
            throw;
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Delete
    //
    // Deletes the VideoSource with the given ID. All VideoDestinations referencing
    // this source will be cascade-deleted by the database.
    //
    // id:  The ID of the source to delete.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void Delete(int id)
    {
        DebugLog.Write(DebugLog.Log_Database, $"VideoSourceRepository.Delete: id={id}.");

        using SqliteConnection conn = Database.Instance.Connect();
        conn.Open();

        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM VideoSources WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        int rows = cmd.ExecuteNonQuery();

        DebugLog.Write(DebugLog.Log_Database, $"VideoSourceRepository.Delete: {rows} row(s) deleted for id={id}.");
    }
}