using Glass.Core;
using Glass.Data.Models;
using Microsoft.Data.Sqlite;

namespace Glass.Data.Repositories;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// KeyBindingRepository
//
// Provides access to key bindings in the database.
// Bindings are queried and saved on demand — no preloaded cache.
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
public class KeyBindingRepository
{
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // GetBindingsForPage
    //
    // Returns all key bindings for the given page ID, ordered by key.
    //
    // keyPageId:  The page to load bindings for
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public List<KeyBinding> GetBindingsForPage(int keyPageId)
    {
        DebugLog.Write(DebugLog.Log_Database, $"KeyBindingRepository.GetBindingsForPage: keyPageId={keyPageId}.");

        using var conn = Database.Instance.Connect();
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, key, command_type, relay_group_id, round_robin, action
            FROM KeyBindings
            WHERE key_page_id = @pageId
            ORDER BY key";
        cmd.Parameters.AddWithValue("@pageId", keyPageId);

        var bindings = new List<KeyBinding>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            bindings.Add(new KeyBinding
            {
                Id = reader.GetInt32(0),
                KeyPageId = keyPageId,
                Key = reader.GetString(1),
                CommandType = reader.GetString(2),
                RelayGroupId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                RoundRobin = reader.GetInt32(4) != 0,
                Action = reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
            });
        }

        DebugLog.Write(DebugLog.Log_Database, $"KeyBindingRepository.GetBindingsForPage: found {bindings.Count} bindings.");
        return bindings;
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Save
    //
    // Inserts or updates a key binding. If the binding has an ID of 0, inserts a new record
    // and updates the binding's ID. Otherwise updates the existing record.
    //
    // binding:  The binding to persist
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void Save(KeyBinding binding)
    {
        DebugLog.Write(DebugLog.Log_Database, $"KeyBindingRepository.Save: keyPageId={binding.KeyPageId} key='{binding.Key}' commandType='{binding.CommandType}' action='{binding.Action}'.");

        using var conn = Database.Instance.Connect();
        conn.Open();

        if (binding.Id == 0)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO KeyBindings (key_page_id, key, command_type, relay_group_id, round_robin, action)
                VALUES (@pageId, @key, @commandType, @relayGroupId, @roundRobin, @action);
                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@pageId", binding.KeyPageId);
            cmd.Parameters.AddWithValue("@key", binding.Key);
            cmd.Parameters.AddWithValue("@commandType", binding.CommandType);
            cmd.Parameters.AddWithValue("@relayGroupId", binding.RelayGroupId.HasValue ? binding.RelayGroupId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@roundRobin", binding.RoundRobin ? 1 : 0);
            cmd.Parameters.AddWithValue("@action", binding.Action);
            binding.Id = Convert.ToInt32(cmd.ExecuteScalar());
            DebugLog.Write(DebugLog.Log_Database, $"KeyBindingRepository.Save: inserted. id={binding.Id}.");
        }
        else
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE KeyBindings
                SET key = @key, command_type = @commandType, relay_group_id = @relayGroupId,
                    round_robin = @roundRobin, action = @action
                WHERE id = @id";
            cmd.Parameters.AddWithValue("@key", binding.Key);
            cmd.Parameters.AddWithValue("@commandType", binding.CommandType);
            cmd.Parameters.AddWithValue("@relayGroupId", binding.RelayGroupId.HasValue ? binding.RelayGroupId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@roundRobin", binding.RoundRobin ? 1 : 0);
            cmd.Parameters.AddWithValue("@action", binding.Action);
            cmd.Parameters.AddWithValue("@id", binding.Id);
            cmd.ExecuteNonQuery();
            DebugLog.Write(DebugLog.Log_Database, $"KeyBindingRepository.Save: updated. id={binding.Id}.");
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Delete
    //
    // Deletes the key binding with the given ID.
    //
    // id:  The binding ID to delete
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void Delete(int id)
    {
        DebugLog.Write(DebugLog.Log_Database, $"KeyBindingRepository.Delete: id={id}.");

        using var conn = Database.Instance.Connect();
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM KeyBindings WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();

        DebugLog.Write(DebugLog.Log_Database, $"KeyBindingRepository.Delete: deleted. id={id}.");
    }
}