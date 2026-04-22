namespace Glass.Network.Protocol;

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using Microsoft.Data.Sqlite;
using Glass.Core;
using Glass.Data;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// PacketFieldExtractor
//
// Reads packet field definitions from the database and extracts typed values
// from a raw packet payload.  Returns a dictionary keyed by field name.
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
public class PacketFieldExtractor
{
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // FieldDefinition
    //
    // Holds a single field definition loaded from the PacketField table.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private struct FieldDefinition
    {
        public string FieldName;
        public int BitOffset;
        public int BitLength;
        public string Encoding;
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Extract
    //
    // Loads field definitions for the given opcode name, patch date, server type,
    // and direction, then extracts each field from the payload.
    //
    // patchDate:   Patch date string (e.g. "2026-04-15")
    // serverType:  Server type string (e.g. "live", "test")
    // opcodeName:  The opcode name (e.g. "OP_ClientUpdate")
    // direction:   Direction (0 = client-to-server, 1 = server-to-client)
    // payload:     The raw packet payload bytes
    //
    // Returns:     Dictionary of field name to typed value, empty if no fields found
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public Dictionary<string, object> Extract(string patchDate, string serverType,
        string opcodeName, int direction, ReadOnlySpan<byte> payload)
    {
        Dictionary<string, object> results = new Dictionary<string, object>();

        DebugLog.Write("PacketFieldExtractor.Extract: opcodeName=" + opcodeName
            + " patchDate=" + patchDate + " serverType=" + serverType
            + " direction=" + direction + " payloadLength=" + payload.Length);

        List<FieldDefinition> fields = LoadFieldDefinitions(patchDate, serverType,
            opcodeName, direction);

        if (fields.Count == 0)
        {
            DebugLog.Write("PacketFieldExtractor.Extract: no fields found");
            return results;
        }

        foreach (FieldDefinition field in fields)
        {
            DebugLog.Write("PacketFieldExtractor.Extract: field=" + field.FieldName
                + " bitOffset=" + field.BitOffset + " bitLength=" + field.BitLength
                + " encoding=" + field.Encoding);

            try
            {
                object? value = DecodeField(field, payload);
                if (value == null)
                {
                    DebugLog.Write("PacketFieldExtractor.Extract: field=" + field.FieldName
                        + " returned null, skipping");
                    continue;
                }
                results[field.FieldName] = value;
                DebugLog.Write("PacketFieldExtractor.Extract: field=" + field.FieldName
                    + " value=" + value);
            }
            catch (Exception ex)
            {
                DebugLog.Write("PacketFieldExtractor.Extract: field=" + field.FieldName
                    + " FAILED: " + ex.Message);
            }
        }

        DebugLog.Write("PacketFieldExtractor.Extract: extracted " + results.Count
            + " of " + fields.Count + " fields");
        return results;
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // LoadFieldDefinitions
    //
    // Queries the PatchOpcode and PacketField tables to load all field definitions
    // for the given opcode name, patch date, server type, and direction.
    //
    // patchDate:   Patch date string (e.g. "2026-04-15")
    // serverType:  Server type string (e.g. "live", "test")
    // opcodeName:  The opcode name (e.g. "OP_ClientUpdate")
    // direction:   Direction (0 = client-to-server, 1 = server-to-client)
    //
    // Returns:     List of FieldDefinition ordered by bit offset, empty if no match found
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private List<FieldDefinition> LoadFieldDefinitions(string patchDate, string serverType,
        string opcodeName, int direction)
    {
        List<FieldDefinition> fields = new List<FieldDefinition>();

        DebugLog.Write("PacketFieldExtractor.LoadFieldDefinitions: opcodeName=" + opcodeName
            + " patchDate=" + patchDate + " serverType=" + serverType
            + " direction=" + direction);

        using SqliteConnection conn = Database.Instance.Connect();
        conn.Open();

        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT pf.field_name, pf.bit_offset, pf.bit_length, pf.encoding"
            + " FROM PacketField pf"
            + " JOIN PatchOpcode po ON pf.patch_opcode_id = po.id"
            + " WHERE po.patch_date = @patchDate"
            + " AND po.server_type = @serverType"
            + " AND po.opcode_name = @opcodeName"
            + " AND po.direction = @direction"
            + " ORDER BY pf.bit_offset";
        cmd.Parameters.AddWithValue("@patchDate", patchDate);
        cmd.Parameters.AddWithValue("@serverType", serverType);
        cmd.Parameters.AddWithValue("@opcodeName", opcodeName);
        cmd.Parameters.AddWithValue("@direction", direction);

        using SqliteDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            FieldDefinition field;
            field.FieldName = reader.GetString(0);
            field.BitOffset = reader.GetInt32(1);
            field.BitLength = reader.GetInt32(2);
            field.Encoding = reader.GetString(3);
            fields.Add(field);

            DebugLog.Write("PacketFieldExtractor.LoadFieldDefinitions: loaded field="
                + field.FieldName + " bitOffset=" + field.BitOffset
                + " bitLength=" + field.BitLength + " encoding=" + field.Encoding);
        }

        DebugLog.Write("PacketFieldExtractor.LoadFieldDefinitions: total fields=" + fields.Count);
        return fields;
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // DecodeField
    //
    // Decodes a single field from the payload based on its encoding.
    // Dispatches to the appropriate extraction method for the encoding type.
    //
    // field:    The field definition from the database
    // payload:  The raw packet payload bytes
    //
    // Returns:  The decoded value as the appropriate type, or null if encoding is unknown
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private object? DecodeField(FieldDefinition field, ReadOnlySpan<byte> payload)
    {
        int byteOffset = field.BitOffset / 8;

        DebugLog.Write("PacketFieldExtractor.DecodeField: field=" + field.FieldName
            + " byteOffset=" + byteOffset + " encoding=" + field.Encoding);

        switch (field.Encoding)
        {
            case "uint8":
                return payload[byteOffset];

            case "uint16_le":
                return BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(byteOffset));

            case "uint16_be":
                return BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(byteOffset));

            case "int16_le":
                return BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(byteOffset));

            case "int32_le":
                return BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(byteOffset));

            case "int32_be":
                return BinaryPrimitives.ReadInt32BigEndian(payload.Slice(byteOffset));

            case "uint32_le":
                return BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(byteOffset));

            case "float_le":
                return BinaryPrimitives.ReadSingleLittleEndian(payload.Slice(byteOffset));

            case "float_be":
                return BinaryPrimitives.ReadSingleBigEndian(payload.Slice(byteOffset));

            case "uint_le_masked":
                return ExtractMaskedUintLE(payload, byteOffset, field.BitLength);

            case "sign_extend_fixed_div8":
                return ExtractSignExtendedDiv8(payload, field.BitOffset, field.BitLength);

            case "string_null_terminated":
                return ExtractNullTerminatedString(payload, byteOffset);

            case "string_length_prefixed":
                return ExtractLengthPrefixedString(payload, byteOffset);

            default:
                DebugLog.Write("PacketFieldExtractor.DecodeField: unknown encoding="
                    + field.Encoding + " for field=" + field.FieldName);
                return null;
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // ExtractMaskedUintLE
    //
    // Reads a uint16 little-endian value and masks to the specified number of bits.
    // Used for fields like heading where only the low N bits are meaningful.
    //
    // payload:    The raw packet payload bytes
    // byteOffset: Byte offset into the payload
    // bitLength:  Number of low bits to keep
    //
    // Returns:    The masked unsigned integer value
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private uint ExtractMaskedUintLE(ReadOnlySpan<byte> payload, int byteOffset, int bitLength)
    {
        uint raw = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(byteOffset));
        uint mask = (1u << bitLength) - 1;
        uint result = raw & mask;

        DebugLog.Write("PacketFieldExtractor.ExtractMaskedUintLE: byteOffset=" + byteOffset
            + " raw=0x" + raw.ToString("x4")
            + " mask=0x" + mask.ToString("x4")
            + " result=" + result);

        return result;
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // ExtractSignExtendedDiv8
    //
    // Extracts a bitfield from a uint64 little-endian read, sign-extends it,
    // and divides by 8.0.  Used for MobUpdate/NpcMoveUpdate coordinates where
    // positions are packed as 19-bit sign-extended fixed-point values.
    //
    // payload:    The raw packet payload bytes
    // bitOffset:  Bit offset from the start of the payload
    // bitLength:  Number of bits in the field
    //
    // Returns:    The sign-extended, scaled double value
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private double ExtractSignExtendedDiv8(ReadOnlySpan<byte> payload, int bitOffset, int bitLength)
    {
        int byteOffset = bitOffset / 8;
        int bitShift = bitOffset % 8;

        ulong raw = BinaryPrimitives.ReadUInt64LittleEndian(payload.Slice(byteOffset));
        ulong mask = (1ul << bitLength) - 1;
        ulong extracted = (raw >> bitShift) & mask;

        ulong signBit = 1ul << (bitLength - 1);
        int signedValue;
        if ((extracted & signBit) != 0)
        {
            signedValue = (int)(extracted | (~mask & 0xFFFFFFFF));
        }
        else
        {
            signedValue = (int)extracted;
        }

        double result = signedValue / 8.0;

        DebugLog.Write("PacketFieldExtractor.ExtractSignExtendedDiv8: bitOffset=" + bitOffset
            + " bitShift=" + bitShift
            + " extracted=0x" + extracted.ToString("x")
            + " signedValue=" + signedValue
            + " result=" + result);

        return result;
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // ExtractNullTerminatedString
    //
    // Reads an ASCII string starting at the given byte offset, terminated by a
    // null byte.  If no null terminator is found, reads to the end of the payload.
    //
    // payload:    The raw packet payload bytes
    // byteOffset: Byte offset into the payload where the string starts
    //
    // Returns:    The extracted string
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private string ExtractNullTerminatedString(ReadOnlySpan<byte> payload, int byteOffset)
    {
        ReadOnlySpan<byte> slice = payload.Slice(byteOffset);
        int nullIndex = slice.IndexOf((byte)0x00);

        string result;
        if (nullIndex < 0)
        {
            result = Encoding.ASCII.GetString(slice);
            DebugLog.Write("PacketFieldExtractor.ExtractNullTerminatedString: byteOffset="
                + byteOffset + " no null terminator, used remaining "
                + slice.Length + " bytes, value=\"" + result + "\"");
        }
        else
        {
            result = Encoding.ASCII.GetString(slice.Slice(0, nullIndex));
            DebugLog.Write("PacketFieldExtractor.ExtractNullTerminatedString: byteOffset="
                + byteOffset + " nullIndex=" + nullIndex
                + " value=\"" + result + "\"");
        }

        return result;
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // ExtractLengthPrefixedString
    //
    // Reads a uint32 little-endian length value at the given byte offset, then
    // reads that many bytes as an ASCII string immediately following.
    //
    // payload:    The raw packet payload bytes
    // byteOffset: Byte offset into the payload where the length field starts
    //
    // Returns:    The extracted string
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private string ExtractLengthPrefixedString(ReadOnlySpan<byte> payload, int byteOffset)
    {
        uint length = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(byteOffset));

        DebugLog.Write("PacketFieldExtractor.ExtractLengthPrefixedString: byteOffset="
            + byteOffset + " length=" + length);

        if (length == 0)
        {
            DebugLog.Write("PacketFieldExtractor.ExtractLengthPrefixedString: zero length string");
            return "";
        }

        int stringOffset = byteOffset + 4;
        int available = payload.Length - stringOffset;
        if (length > available)
        {
            DebugLog.Write("PacketFieldExtractor.ExtractLengthPrefixedString: length=" + length
                + " exceeds available=" + available + ", clamping");
            length = (uint)available;
        }

        string result = Encoding.ASCII.GetString(payload.Slice(stringOffset, (int)length));

        DebugLog.Write("PacketFieldExtractor.ExtractLengthPrefixedString: value=\"" + result + "\"");

        return result;
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // GetCandidateOpcodes
    //
    // Queries the PatchOpcode table for all opcodes that could match a packet
    // of the given direction and payload length.  An opcode is a candidate if
    // its direction matches and its known byte_length is less than or equal to
    // the payload length.  Variable-length opcodes (null byte_length) are
    // included if the payload is at least large enough to contain the highest
    // defined field.
    //
    // patchDate:     Patch date string (e.g. "2026-04-15")
    // serverType:    Server type string (e.g. "live", "test")
    // direction:     Direction (0 = client-to-server, 1 = server-to-client)
    // payloadLength: Length of the observed payload in bytes
    //
    // Returns:       List of opcode names that could match
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public List<string> GetCandidateOpcodes(string patchDate, string serverType,
        int direction, int payloadLength)
    {
        List<string> candidates = new List<string>();

        DebugLog.Write("PacketFieldExtractor.GetCandidateOpcodes: patchDate=" + patchDate
            + " serverType=" + serverType + " direction=" + direction
            + " payloadLength=" + payloadLength);

        using SqliteConnection conn = Database.Instance.Connect();
        conn.Open();

        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT po.opcode_name, po.byte_length,"
            + " MAX(pf.bit_offset + pf.bit_length) AS max_bit"
            + " FROM PatchOpcode po"
            + " LEFT JOIN PacketField pf ON pf.patch_opcode_id = po.id"
            + " WHERE po.patch_date = @patchDate"
            + " AND po.server_type = @serverType"
            + " AND po.direction = @direction"
            + " GROUP BY po.id"
            + " ORDER BY po.opcode_name";
        cmd.Parameters.AddWithValue("@patchDate", patchDate);
        cmd.Parameters.AddWithValue("@serverType", serverType);
        cmd.Parameters.AddWithValue("@direction", direction);

        using SqliteDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            string opcodeName = reader.GetString(0);
            bool hasFixedLength = !reader.IsDBNull(1);

            if (hasFixedLength)
            {
                int byteLength = reader.GetInt32(1);
                if (payloadLength >= byteLength)
                {
                    candidates.Add(opcodeName);
                    DebugLog.Write("PacketFieldExtractor.GetCandidateOpcodes: candidate="
                        + opcodeName + " fixedLength=" + byteLength + " MATCH");
                }
                else
                {
                    DebugLog.Write("PacketFieldExtractor.GetCandidateOpcodes: candidate="
                        + opcodeName + " fixedLength=" + byteLength + " TOO LARGE");
                }
            }
            else
            {
                int minBytes = 0;
                if (!reader.IsDBNull(2))
                {
                    int maxBit = reader.GetInt32(2);
                    minBytes = (maxBit + 7) / 8;
                }

                if (payloadLength >= minBytes)
                {
                    candidates.Add(opcodeName);
                    DebugLog.Write("PacketFieldExtractor.GetCandidateOpcodes: candidate="
                        + opcodeName + " variable minBytes=" + minBytes + " MATCH");
                }
                else
                {
                    DebugLog.Write("PacketFieldExtractor.GetCandidateOpcodes: candidate="
                        + opcodeName + " variable minBytes=" + minBytes + " TOO LARGE");
                }
            }
        }

        DebugLog.Write("PacketFieldExtractor.GetCandidateOpcodes: total candidates="
            + candidates.Count);
        return candidates;
    }
}