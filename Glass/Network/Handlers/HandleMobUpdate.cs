using System;
using System.Buffers.Binary;
using Glass.Core;
using Glass.Network.Protocol;

namespace Glass.Network.Handlers;

///////////////////////////////////////////////////////////////////////////////////////////////
// HandleMobUpdate
//
// Handles OP_NpcMove packets.  
///////////////////////////////////////////////////////////////////////////////////////////////
public class HandleMobUpdate : IHandleOpcodes
{
    private ushort _opcode = 0x73da;
    private readonly string _opcodeName = "OP_MobUpdate";

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
        ushort spawnId = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(0));
        ushort unknown02 = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(2));
        ulong packed = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(4));
        short headingRaw = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(12));

        // -----------------------------------------------------------
        // Extract 19-bit fixed-point coordinates (3 fractional bits)
        // -----------------------------------------------------------
        const ulong MASK_19BIT = 0x7FFFF;
        const float FIXED_POINT_DIVISOR = 8.0f;

        ulong rawX = packed & MASK_19BIT;
        ulong rawY = (packed >> 19) & MASK_19BIT;
        ulong rawZ = (packed >> 45) & MASK_19BIT;

        // -----------------------------------------------------------
        // Sign-extend 19-bit values to handle negative coordinates
        // -----------------------------------------------------------
        int signedX = (rawX & 0x40000) != 0 ? (int)(rawX | 0xFFF80000) : (int)rawX;
        int signedZ = (rawZ & 0x40000) != 0 ? (int)(rawZ | 0xFFF80000) : (int)rawZ;
        int signedY = (rawY & 0x40000) != 0 ? (int)(rawY | 0xFFF80000) : (int)rawY;

        float x = signedX / FIXED_POINT_DIVISOR;
        float z = signedZ / FIXED_POINT_DIVISOR;
        float y = signedY / FIXED_POINT_DIVISOR;



        DebugLog.Write("[" + metadata.Timestamp.ToString("HH:mm:ss.fff") + "] "
            + _opcodeName + " length=" + length);
        DebugLog.Write("SpawnId=" + spawnId + " (0x" + spawnId.ToString("x4") + ")");
        DebugLog.Write("ExtractPositionUpdate: rawX={0} rawY={1} rawZ={2} -> x={3:F2} y={5:F2} z={4:F2} ",
    rawX, rawZ, rawY, x, y, z);
    }

}
