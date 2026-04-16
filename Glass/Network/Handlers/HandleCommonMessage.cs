using Glass.Core;
using Glass.Network.Protocol;
using System;
using System.Buffers.Binary;
using System.Text;

namespace Glass.Network.Handlers;

///////////////////////////////////////////////////////////////////////////////////////////////
// HandleCommonMessage
//
// Handles OP_CommonMessage packets.  
///////////////////////////////////////////////////////////////////////////////////////////////
public class HandleCommonMessage : IHandleOpcodes
{
    private ushort _opcode = 0xdd47;
    private readonly string _opcodeName = "OP_CommonMessage";

    private static readonly byte ChannelShout = 0x03;
    private static readonly byte ChannelOoc = 0x05;
    private static readonly byte ChannelSay = 0x08;
    private static readonly byte ChannelCustomBase = 0x10;

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
    // metadata:  Packet metadata (timestamp, source/dest)
    ///////////////////////////////////////////////////////////////////////////////////////////////
    private void HandleServerToClient(ReadOnlySpan<byte> data, int length, PacketMetadata metadata)
    {
        DebugLog.Write(_opcodeName);
        DebugLog.Write("Server to Client");
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////
    // HandleClientToServer
    //
    // Processes client-to-zone OP_CommonMessage.
    //
    // Fixed structure:
    //   Offset 0x00 (0):   Sender name, null-terminated, 21-byte fixed field
    //   Offset 0x15 (21):  Channel ID byte
    //   Offset 0x16 (22):  12 bytes unknown (zeros observed)
    //   Offset 0x22 (34):  Message text, null-terminated
    //
    // data:      The application payload
    // length:    Length of the application payload
    // metadata:  Packet metadata (timestamp, source/dest)
    ///////////////////////////////////////////////////////////////////////////////////////////////
    private void HandleClientToServer(ReadOnlySpan<byte> data, int length, PacketMetadata metadata)
    {
        DebugLog.Write("[" + metadata.Timestamp.ToString("HH:mm:ss.fff") + "] "
            + _opcodeName + " length=" + length);

        if (length < 35)
        {
            DebugLog.Write(_opcodeName + " too short, length=" + length + ", minimum is 35.");
            return;
        }

        // Sender name: null-terminated within a 21-byte fixed field (offsets 0x00-0x14)
        int nullIndex = data.Slice(0, 21).IndexOf((byte)0x00);
        if (nullIndex < 0)
        {
            DebugLog.Write(_opcodeName + " no null terminator in sender name field.");
            return;
        }
        string senderName = Encoding.ASCII.GetString(data.Slice(0, nullIndex));
        DebugLog.Write(_opcodeName + " Sender=\"" + senderName + "\"");

        // Channel ID at offset 0x15
        byte channelId = data[21];
        string channelName = GetChannelName(channelId);
        DebugLog.Write(_opcodeName + " Channel=" + channelId
            + " (0x" + channelId.ToString("x2") + ") " + channelName);

        // Message text: null-terminated starting at offset 0x22
        ReadOnlySpan<byte> messageSpan = data.Slice(34);
        int messageNull = messageSpan.IndexOf((byte)0x00);
        if (messageNull < 0)
        {
            DebugLog.Write(_opcodeName + " no null terminator in message text.");
            return;
        }
        string messageText = Encoding.ASCII.GetString(messageSpan.Slice(0, messageNull));
        DebugLog.Write(_opcodeName + " Message=\"" + messageText + "\"");
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////
    // GetChannelName
    //
    // Returns a human-readable name for the channel ID byte.
    // Custom channels appear to start at base 0x10, so channel ID 0x14 = custom channel 4.
    // This base offset is speculative and needs confirmation from other custom channel captures.
    //
    // channelId:  The channel ID byte from offset 0x15
    ///////////////////////////////////////////////////////////////////////////////////////////////
    private string GetChannelName(byte channelId)
    {
        if (channelId == ChannelShout)
        {
            return "/shout";
        }
        if (channelId == ChannelOoc)
        {
            return "/ooc";
        }
        if (channelId == ChannelSay)
        {
            return "/say";
        }
        if (channelId >= ChannelCustomBase)
        {
            int customNumber = channelId - ChannelCustomBase;
            return "/" + customNumber;
        }
        return "unknown(" + channelId.ToString("x2") + ")";
    }
}