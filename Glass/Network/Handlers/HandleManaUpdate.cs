using Glass.Core;
using Glass.Core.Logging;
using Glass.Network.Protocol;
using System;
using System.Buffers.Binary;

namespace Glass.Network.Handlers;

///////////////////////////////////////////////////////////////////////////////////////////////
// HandleManaUpdate
//
// Handles OP_PlayerProfile packets.  
///////////////////////////////////////////////////////////////////////////////////////////////
public class HandleManaUpdate : IHandleOpcodes
{
    private ushort _opcode = 0x37ad;
    private readonly string _opcodeName = "OP_ManaUpdate";

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
    // metadata:  Packet metadata (timestamp, source/dest)
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
    // metadata:  Packet metadata (timestamp, source/dest)
    ///////////////////////////////////////////////////////////////////////////////////////////////
    private void HandleServerToClient(ReadOnlySpan<byte> data, int length, PacketMetadata metadata)
    {
        if (length < 10)
        {
            DebugLog.Write(LogChannel.Opcodes, "ManaUpdate packet less than 10 bytes.  This is unusual");
            return;
        }

        int playerId = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(0));
        int currentMana = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(2));
        int maxMana = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(6));



        DebugLog.Write(LogChannel.Opcodes, "[" + metadata.Timestamp.ToString("HH:mm:ss.fff") + "] "
            + _opcodeName + " length=" + length);
        DebugLog.Write(LogChannel.Opcodes, "Mana at " + currentMana + " / " + maxMana);
    }
}


