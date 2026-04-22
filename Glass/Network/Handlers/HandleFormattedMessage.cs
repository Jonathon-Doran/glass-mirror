using Glass.Core;
using Glass.Network.Protocol;
using System;
using System.Buffers.Binary;
using System.Text;

namespace Glass.Network.Handlers;

///////////////////////////////////////////////////////////////////////////////////////////////
// HandleFormattedMessage
//
// Handles OP_FormattedMessage packets.  
///////////////////////////////////////////////////////////////////////////////////////////////
public class HandleFormattedMessage : IHandleOpcodes
{
    private ushort _opcode = 0x359f;
    private readonly string _opcodeName = "OP_FormattedMessage";

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
        uint msgLength = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(13));
        string msgText = Encoding.ASCII.GetString(data.Slice(17, (int) msgLength));

        DebugLog.Write("[" + metadata.Timestamp.ToString("HH:mm:ss.fff") + "] "
            + _opcodeName + " length=" + length);
        DebugLog.Write("Message: " + msgText);

    }

}

