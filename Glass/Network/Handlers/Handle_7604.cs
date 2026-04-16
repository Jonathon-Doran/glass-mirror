///////////////////////////////////////////////////////////////////////////////////////////////
// HandleZoneSpawns
//
// Handles OP_ZoneSpawns packets.  
///////////////////////////////////////////////////////////////////////////////////////////////
using Glass.Core;
using Glass.Network.Protocol;
using System.Buffers.Binary;

public class HandleClientUpdate : IHandleOpcodes
{
    private ushort _opcode = 0x7604;
    private readonly string _opcodeName = "OP_ZoneSpawns";

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
            HandleClientToServer(data, length, metadata);
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
        DebugLog.Write("[" + metadata.Timestamp.ToString("HH:mm:ss.fff") + "] " + _opcodeName + " length=" + length + " zone->client");

        uint magic = BinaryPrimitives.ReadUInt32BigEndian(data);
        ushort count = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0));

        // this stupid magic number overloads the opcode and will control when the client can ask for spawn packets
        if (magic == 0x4f348bff)
        {
            DebugLog.Write("magic=" + magic.ToString("x4"));

            float cooldown = BinaryPrimitives.ReadSingleLittleEndian(data.Slice(4));
            DebugLog.Write("cooldown=" +  cooldown.ToString());
            return;
        }

        ushort spawnId = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(2));
        int unknown2 = BinaryPrimitives.ReadInt32BigEndian(data.Slice(4));
        uint unknown3 = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(8));

        // count is either a non-zero value, or 0 for padding (internal entries)

        DebugLog.Write("spawn= " + spawnId + " (0x" + spawnId.ToString("x4") + ")");
        DebugLog.Write("count= " + count.ToString() + " (0x" + count.ToString("x2") + ")");
        DebugLog.Write("unknown2= " + unknown2.ToString() + " (0x" + unknown2.ToString("x4") + ")");
        DebugLog.Write("unknown3= " + unknown3.ToString() + " (0x" + unknown3.ToString("x4") + ")");


        int terminator = FindNullTerminator(data.Slice(12), length - 12);
        if (terminator == -1)
        {
            DebugLog.Write("No null terminator seen after name");
            return;
        }

        string name = System.Text.Encoding.ASCII.GetString(data.Slice(12, terminator));
        DebugLog.Write("name= " + name);
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////
    // HandleClientToServer
    //
    // Processes client-to-zone
    //
    // data:    The application payload
    // length:  Length of the application payload
    ///////////////////////////////////////////////////////////////////////////////////////////////
    private void HandleClientToServer(ReadOnlySpan<byte> data, int length, PacketMetadata metadata)
    {
        DebugLog.Write("[" + metadata.Timestamp.ToString("HH:mm:ss.fff") + "] " + _opcodeName + " length=" + length + " client->zone");
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
            DebugLog.Write("HandleZoneEntry.HandleServerToClient: "
                + _opcodeName + " no null terminator found, length=" + length);
            return -1;
        }

        return nullPos;
    }
}

