using Glass.Core;
using Glass.Core.Logging;
using Glass.Network.Protocol;
using System;
using System.Buffers.Binary;

namespace Glass.Network.Handlers;

///////////////////////////////////////////////////////////////////////////////////////////////
// HandlePlayerProfile
//
// Handles OP_PlayerProfile packets.  
///////////////////////////////////////////////////////////////////////////////////////////////
public class HandlePlayerProfile : IHandleOpcodes
{
    private ushort _opcode = 0xdb56;
    private readonly string _opcodeName = "OP_PlayerProfile";

    ///////////////////////////////////////////////////////////////////////////////////////////////
    // Opcode
    ///////////////////////////////////////////////////////////////////////////////////////////////
    public ushort Opcode
    {
        get { return _opcode; }
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////
    // OpcodeName
    ///////////////////////////////////////////////////////////////////////////////////////////////
    public string OpcodeName
    {
        get { return _opcodeName; }
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////
    // HandlePacket
    //
    // Dispatches to direction-specific handlers.
    //
    // data:       The application payload
    // length:     Length of the application payload
    // direction:  Direction byte
    // opcode:     The application-level opcode
    ///////////////////////////////////////////////////////////////////////////////////////////////
    public void HandlePacket(ReadOnlySpan<byte> data, int length,
                              byte direction, ushort opcode, PacketMetadata metadata)
    {
        if (direction == SoeConstants.DirectionServerToClient)
        {
            HandleServerToClient(data, length, metadata);
        }
    }


    ///////////////////////////////////////////////////////////////////////////////////////////////
    // HandleServerToClient
    //
    // Processes zone-to-client traffic
    //
    // data:    The application payload
    // length:  Length of the application payload
    ///////////////////////////////////////////////////////////////////////////////////////////////
    private void HandleServerToClient(ReadOnlySpan<byte> data, int length, PacketMetadata metadata)
    {
        if (length < 4)
        {
            DebugLog.Write(LogChannel.Opcodes, _opcodeName + " too short, length=" + length);
            return;
        }

        string name = "unknown";

        int nullPosition = FindNullTerminator(data.Slice(0x4fcb), length - 0x4fcb);
        if (nullPosition != -1)
        {
            name = System.Text.Encoding.ASCII.GetString(data.Slice(0x4fcb, nullPosition));
        }

        int level = BinaryPrimitives.ReadInt32BigEndian(data.Slice(0x1a));
        int playerClass = data[0x19];
        string className = GetClassName(playerClass);
        uint practicePoints = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0x3b2));
        uint mana = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0x3b6));
        int hitpoints = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0x3ba));
        int strength = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0x3be));
        int stamina = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0x3c2));
        int charisma = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0x3c6));
        int dexterity = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0x3ca));
        int intelligence = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0x3ce));
        int agility = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0x3d2));
        int wisdom = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0x3d6));

        int platinumCarried = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0x4beb));
        int goldCarried = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0x4bef));
        int silverCarried = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0x4bf3));
        int copperCarried = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(0x4bf7));


        DebugLog.Write(LogChannel.Opcodes, "[" + metadata.Timestamp.ToString("HH:mm:ss.fff") + "] "
            + _opcodeName + " length=" + length);
        DebugLog.Write(LogChannel.Opcodes, "Name: " + name + ", " + level + " " + className);
        DebugLog.Write(LogChannel.Opcodes, "HP: " + hitpoints + ", Mana: " + mana + ", " + platinumCarried + "pp/" + goldCarried + "gp/" + silverCarried + "sp/" + copperCarried + "cp");
        DebugLog.Write(LogChannel.Opcodes, "Str: " + strength + ", Cha: " + charisma + ", Dex: " + dexterity + ", Int: " + intelligence + ", Agi: " + agility + ", Wis: " + wisdom);
    }
    private int FindNullTerminator(ReadOnlySpan<byte> data, int length)
    {
        // Find the null terminator for the name string at offset 0
        int nullPos = -1;
        for (int i = 0; i < length; i++)
        {
            if (data[i] == 0)
            {
                nullPos = i;
                break;
            }
        }

        if (nullPos < 0)
        {
            DebugLog.Write(LogChannel.Opcodes, _opcodeName + ": no null terminator found");
            return -1;
        }

        return nullPos;
    }

    private static readonly Dictionary<int, string> ClassNames = new Dictionary<int, string>()
    {
        { 0, "None" },
        { 1, "Warrior" },
        { 2, "Cleric" },
        { 3, "Paladin" },
        { 4, "Ranger" },
        { 5, "ShadowKnight" },
        { 6, "Druid" },
        { 7, "Monk" },
        { 8, "Bard" },
        { 9, "Rogue" },
        {10, "Shaman" },
        {11, "Necromancer" },
        {12, "Wizard" },
        {13, "Magician" },
        {14, "Enchanter" },
        {15, "Beastlord" },
        {16, "Berserker" }
    };

    ///////////////////////////////////////////////////////////////////////////////////////////////
    // GetClassName
    //
    // Looks up a class name by its byte value. Returns a descriptive string for unknown values
    // rather than throwing — an unknown class id should log and continue, not crash.
    //
    // classId:    The class byte from OP_PlayerProfile at offset 0x1a
    ///////////////////////////////////////////////////////////////////////////////////////////////
    public static string GetClassName(int classId)
    {
        if (ClassNames.TryGetValue(classId, out string? name))
        {
            return name;
        }

        DebugLog.Write(LogChannel.Opcodes, $"[GetClassName] classId=0x{classId:X2} not in map, returning 'Unknown'");
        return $"Unknown(0x{classId:X2})";
    }
}

