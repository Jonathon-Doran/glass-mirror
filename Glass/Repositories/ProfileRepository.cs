using Glass.Core;
using Glass.Data.Models;
using Microsoft.Data.Sqlite;

namespace Glass.Data.Repositories;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// ProfileRepository
//
// Loads and caches all data for a single named character set.
// All public methods query the in-memory cache — no database access after construction.
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
public class ProfileRepository
{
    private Profile _profile;
    private List<SlotAssignment> _slots;

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // ProfileRepository
    //
    // Loads an existing character set from the database if it exists, otherwise creates a new
    // empty one.  Populate with SetSlots() and call Save() to persist.
    //
    // profileName:  The name of the profile to load or create
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public ProfileRepository(string profileName)
    {
        DebugLog.Write(DebugLog.Log_Database, $"ProfileRepository: opening profile '{profileName}'");

        using var conn = Database.Instance.Connect();
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name FROM Profiles WHERE name = @name";
        cmd.Parameters.AddWithValue("@name", profileName);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            _profile = new Profile
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1)
            };

            reader.Close();
            _slots = LoadSlots(_profile.Id);
            DebugLog.Write(DebugLog.Log_Database, $"ProfileRepository: loaded. id={_profile.Id} slots={_slots.Count}");
        }
        else
        {
            _profile = new Profile 
            { 
                Name = profileName 
            };
            _slots = new List<SlotAssignment>();
            DebugLog.Write(DebugLog.Log_Database, $"ProfileRepository: '{profileName}' not found, created empty.");
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // GetId
    //
    // Returns the ID of the loaded character set, or 0 if not found.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public int GetId()
    {
        return _profile?.Id ?? 0;
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // SetSlots
    //
    // Replaces the slot assignment list held by this repository.
    //
    // slots:  The new list of slot assignments
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void SetSlots(List<SlotAssignment> slots)
    {
        DebugLog.Write(DebugLog.Log_Database, $"ProfileRepository.SetSlots: {slots.Count} slots.");
        _slots = slots;
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // GetSlots
    //
    // Returns a read-only view of the cached slot assignments.  Profile launching is a use-case.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public IReadOnlyList<SlotAssignment> GetSlots() => _slots.AsReadOnly();

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // GetSlotForCharacter
    //
    // Returns the slot assignment for the given character name, or null if not found.
    //
    // characterName:  The character to query
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public SlotAssignment? GetSlotForCharacter(int characterId)
    {
        return _slots.FirstOrDefault(s => s.CharacterId == characterId);
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Save
    //
    // Persists the profile name, slot assignments, and start page currently held by this
    // repository to the database. Returns the profile ID on success, or -1 if the profile already
    // exists and overwrite is false.
    //
    // overwrite:  If true, replaces an existing profile with the same name.
    //             If false and the profile exists, returns -1 and makes no changes.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public int Save(bool overwrite = false)
    {
        string profileName = _profile.Name;
        DebugLog.Write(DebugLog.Log_Database, $"ProfileRepository.Save: name='{profileName}' slots={_slots.Count} overwrite={overwrite}");

        if (string.IsNullOrWhiteSpace(profileName))
        {
            DebugLog.Write(DebugLog.Log_Database, "ProfileRepository.Save: profile name is empty, aborting.");
            throw new InvalidOperationException("Profile name must be set before calling Save.");
        }

        using var conn = Database.Instance.Connect();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            using var checkCmd = conn.CreateCommand();
            checkCmd.Transaction = tx;
            checkCmd.CommandText = "SELECT id FROM Profiles WHERE name = @name";
            checkCmd.Parameters.AddWithValue("@name", profileName);
            var existingId = checkCmd.ExecuteScalar();

            int profileId;
            if (existingId != null)
            {
                if (!overwrite)
                {
                    DebugLog.Write(DebugLog.Log_Database, "ProfileRepository.Save: profile exists and overwrite=false, returning -1.");
                    tx.Rollback();
                    return -1;
                }

                profileId = Convert.ToInt32(existingId);
                DebugLog.Write(DebugLog.Log_Database, $"ProfileRepository.Save: overwriting profileId={profileId}, deleting existing slots.");

                using var deleteCmd = conn.CreateCommand();
                deleteCmd.Transaction = tx;
                deleteCmd.CommandText = "DELETE FROM ProfileSlots WHERE profile_id = @id";
                deleteCmd.Parameters.AddWithValue("@id", profileId);
                deleteCmd.ExecuteNonQuery();
            }
            else
            {
                using var insertCmd = conn.CreateCommand();
                insertCmd.Transaction = tx;
                insertCmd.CommandText = "INSERT INTO Profiles (name) VALUES (@name); SELECT last_insert_rowid();";
                insertCmd.Parameters.AddWithValue("@name", profileName);
                profileId = Convert.ToInt32(insertCmd.ExecuteScalar());
                DebugLog.Write(DebugLog.Log_Database, $"ProfileRepository.Save: inserted new profile, profileId={profileId}.");
            }

            foreach (var slot in _slots)
            {
                DebugLog.Write(DebugLog.Log_Database, $"ProfileRepository.Save: slot {slot.SlotNumber} = characterId={slot.CharacterId}.");

                using var insertSlot = conn.CreateCommand();
                insertSlot.Transaction = tx;
                insertSlot.CommandText = "INSERT INTO ProfileSlots (profile_id, slot_number, character_id) VALUES (@setId, @slotNumber, @charId)";
                insertSlot.Parameters.AddWithValue("@setId", profileId);
                insertSlot.Parameters.AddWithValue("@slotNumber", slot.SlotNumber);
                insertSlot.Parameters.AddWithValue("@charId", slot.CharacterId);
                insertSlot.ExecuteNonQuery();
            }

            tx.Commit();
            _profile.Id = profileId;
            DebugLog.Write(DebugLog.Log_Database, $"ProfileRepository.Save: committed. profileId={profileId}");
            return profileId;
        }
        catch (Exception ex)
        {
            DebugLog.Write(DebugLog.Log_Database, $"ProfileRepository.Save: exception: {ex.Message}, rolling back.");
            tx.Rollback();
            throw;
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // LoadCharacterSet
    //
    // Loads the character set record by name from the database.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private Profile LoadProfile(string profileName)
    {
        using var conn = Database.Instance.Connect();
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name FROM Profiles WHERE name = @name";
        cmd.Parameters.AddWithValue("@name", profileName);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Profile '{profileName}' not found.");
        }

        return new Profile
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1)
        };
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // LoadSlots
    //
    // Loads all slot assignments for the given profile ID from the database.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private List<SlotAssignment> LoadSlots(int profileID)
    {
        using var conn = Database.Instance.Connect();
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        SELECT slot_number, character_id
        FROM ProfileSlots
        WHERE profile_id = @id
        ORDER BY slot_number";
        cmd.Parameters.AddWithValue("@id", profileID);

        var slots = new List<SlotAssignment>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            slots.Add(new SlotAssignment
            {
                SlotNumber = reader.GetInt32(0),
                CharacterId = reader.GetInt32(1)
            });
        }

        return slots;
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // GetAllNames
    //
    // Returns the names of all character sets in the database, ordered alphabetically.
    // Use this to populate profile list UI before constructing a full repository.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public static List<string> GetAllNames()
    {
        DebugLog.Write(DebugLog.Log_Database, "ProfileRepository.GetAllNames: loading.");

        using var conn = Database.Instance.Connect();
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM Profiles ORDER BY name";

        var names = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        DebugLog.Write(DebugLog.Log_Database, $"ProfileRepository.GetAllNames: found {names.Count} profiles.");
        return names;
    }
}