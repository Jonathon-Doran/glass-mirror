using System;
using System.Buffers.Binary;
using Glass.Core;
using Glass.Network.Protocol;

namespace Glass.Network.Handlers;

///////////////////////////////////////////////////////////////////////////////////////////////
// HandleZoneEntry
//
// Handles OP_ZoneEntry packets.  Server-to-client packets contain NPC/mob
// spawn data with a null-terminated name at offset 0.  Client-to-server
// packets contain the player's own zone entry with a different layout.
///////////////////////////////////////////////////////////////////////////////////////////////
public class HandleZoneEntry : IHandleOpcodes
{
    private ushort _opcode = 0xf19a;
    private readonly string _opcodeName = "OP_ZoneEntry";

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
        else if (direction == SoeConstants.DirectionClientToServer)
        {
            HandleClientToServer(data, length);
        }
        else
        {
            DebugLog.Write("HandleZoneEntry: unknown direction=" + direction);
        }
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////
    // HandleServerToClient
    //
    // Processes zone-to-client OP_ZoneEntry.
    //
    // data:    The application payload
    // length:  Length of the application payload
    ///////////////////////////////////////////////////////////////////////////////////////////////
    private void HandleServerToClient(ReadOnlySpan<byte> data, int length, PacketMetadata metadata)
    {
        if (length < 4)
        {
            DebugLog.Write("HandleZoneEntry.HandleServerToClient: "
                + _opcodeName + " too short, length=" + length);
            return;
        }

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
            DebugLog.Write("HandleZoneEntry.HandleServerToClient: "
                + _opcodeName + " no null terminator found, length=" + length);
            return;
        }

        string name = System.Text.Encoding.ASCII.GetString(data.Slice(0, nullPos));

        uint spawnId = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(nullPos+1));
        uint level = data[nullPos + 5];

        DebugLog.Write("[" + metadata.Timestamp.ToString("HH:mm:ss.fff") + "] " + _opcodeName + " length=" + length);
        DebugLog.Write("name=\"" + name + "\"  id=(0x" + spawnId.ToString("x4")+")");
        DebugLog.Write("SpawnId=" + spawnId + " (0x" + spawnId.ToString("x4") + ")");
        DebugLog.Write("Level=" + level + " (0x" + level.ToString("x4") + ")");
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////
    // HandleClientToServer
    //
    // Processes client-to-zone OP_ZoneEntry. 
    //
    // data:    The application payload
    // length:  Length of the application payload
    ///////////////////////////////////////////////////////////////////////////////////////////////
    private void HandleClientToServer(ReadOnlySpan<byte> data, int length)
    {
        DebugLog.Write("HandleZoneEntry.HandleClientToServer: "
            + _opcodeName + " length=" + length);
    }
}