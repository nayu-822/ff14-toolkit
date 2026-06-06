using FF14Toolkit.App.Models.Hotbar;
using System;
using System.Collections.Generic;
using System.IO;

namespace FF14Toolkit.App.Services.Hotbar;

public sealed class HotbarDatParser
{
    public const int DefaultHeaderSize = 16;

    private const byte XorKey = 0x31;
    private const int RecordSize = 8;

    public HotbarParseResult ParseFile(string filePath, int headerSize = DefaultHeaderSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        using FileStream stream = new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);

        return Parse(stream, headerSize);
    }

    public HotbarParseResult Parse(Stream stream, int headerSize = DefaultHeaderSize)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (headerSize < 0)
        {
            throw new InvalidOperationException("Header size must be 0 or greater.");
        }

        if (!stream.CanRead)
        {
            throw new InvalidOperationException("The stream must be readable.");
        }

        using BinaryReader reader = new(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        long remainingLength = stream.Length - stream.Position;

        if (remainingLength < headerSize)
        {
            throw new InvalidOperationException("The file is smaller than the configured header size.");
        }

        byte[] headerBytes = reader.ReadBytes(headerSize);
        byte[] encodedBody = reader.ReadBytes((int)(stream.Length - stream.Position));
        byte[] decodedBody = DecodeBody(encodedBody);

        if (decodedBody.Length % RecordSize != 0)
        {
            throw new InvalidOperationException("The decoded body length is not aligned to 8-byte records.");
        }

        IReadOnlyList<HotbarSlotEntry> entries = ParseEntries(decodedBody);

        return new HotbarParseResult
        {
            Header = new HotbarFileHeader
            {
                HeaderSize = headerSize,
                RawBytes = headerBytes
            },
            Entries = entries
        };
    }

    private static byte[] DecodeBody(byte[] encodedBody)
    {
        byte[] decodedBody = new byte[encodedBody.Length];

        for (int index = 0; index < encodedBody.Length; index++)
        {
            decodedBody[index] = (byte)(encodedBody[index] ^ XorKey);
        }

        return decodedBody;
    }

    private static IReadOnlyList<HotbarSlotEntry> ParseEntries(byte[] decodedBody)
    {
        List<HotbarSlotEntry> entries = new(decodedBody.Length / RecordSize);

        for (int offset = 0; offset < decodedBody.Length; offset += RecordSize)
        {
            uint commandId =
                (uint)(decodedBody[offset]
                | (decodedBody[offset + 1] << 8)
                | (decodedBody[offset + 2] << 16)
                | (decodedBody[offset + 3] << 24));

            entries.Add(new HotbarSlotEntry
            {
                CommandId = commandId,
                GroupId = decodedBody[offset + 4],
                HotbarId = decodedBody[offset + 5],
                SlotId = decodedBody[offset + 6],
                SlotTypeId = decodedBody[offset + 7]
            });
        }

        return entries;
    }
}
