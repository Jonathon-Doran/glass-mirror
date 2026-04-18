using System;
using System.Buffers.Binary;
using System.Security.RightsManagement;
using Glass.Core;
using Glass.Network.Protocol;

namespace Glass.Network.Handlers;

///////////////////////////////////////////////////////////////////////////////////////////////
// HandleClientUpdate
//
// Handles OP_ClientUpdate packets.  
///////////////////////////////////////////////////////////////////////////////////////////////
public class HandleClientUpdate : IHandleOpcodes
{
    private ushort _opcode = 0x0aa7;
    private readonly string _opcodeName = "OP_ClientUpdate";

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
        DebugLog.Write(_opcodeName);
        DebugLog.Write("Server to Client");
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
        DebugLog.Write("[" + metadata.Timestamp.ToString("HH:mm:ss.fff") + "] " + _opcodeName + " length=" + length);

        ushort sequence = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(0));
        uint playerId = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(2));
        // skip 2 bytes
        float deltaY = BinaryPrimitives.ReadSingleBigEndian(data.Slice(6));
        float xPos = BinaryPrimitives.ReadSingleLittleEndian(data.Slice(10));
        int deltaHeading = BinaryPrimitives.ReadInt32BigEndian(data.Slice(14));
        deltaHeading >>= 22;
        float yPos = BinaryPrimitives.ReadSingleLittleEndian(data.Slice(18));
        float zPos = BinaryPrimitives.ReadSingleLittleEndian(data.Slice(34));

        UInt32 lastword = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(38));
        uint heading = (uint)data[39] | ((uint)data[40] << 8) | ((uint)data[41] << 16);

        DebugLog.Write("Player " + playerId + " (0x" + playerId.ToString("x4") + ")");
        DebugLog.Write("[" + metadata.Timestamp.ToString("HH:mm:ss.fff") + " Position:  (" + xPos.ToString("F2") + "," + yPos.ToString("F2") + "," + zPos.ToString("F2") + ")");
        DebugLog.Write("Heading? is " + heading.ToString() + " 0x(" + heading.ToString("x8") + ") = " + heading/8148.0*360.0 + " degrees");
    }
}
