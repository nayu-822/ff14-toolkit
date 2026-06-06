using FF14Toolkit.App.Models.Keybind;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FF14Toolkit.App.Services.Keybind;

public sealed class KeybindDatParser
{
    public const int DefaultHeaderSize = 0x11;

    private const byte XorKey = 0x73;

    public KeybindParseResult ParseFile(string filePath, int headerSize = DefaultHeaderSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        using FileStream stream = new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);

        return Parse(stream, headerSize);
    }

    public KeybindParseResult Parse(Stream stream, int headerSize = DefaultHeaderSize)
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

        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);
        long remainingLength = stream.Length - stream.Position;

        if (remainingLength < headerSize)
        {
            throw new InvalidOperationException("The file is smaller than the configured header size.");
        }

        byte[] headerBytes = reader.ReadBytes(headerSize);
        KeybindFileHeader header = ParseHeader(headerBytes, headerSize, stream.Length);
        int validBodyLength = checked((int)Math.Max(0, header.DataSize - headerSize));

        if (validBodyLength > stream.Length - stream.Position)
        {
            throw new InvalidOperationException("The file is smaller than the data size declared in the header.");
        }

        byte[] encodedBody = reader.ReadBytes(validBodyLength);
        byte[] decodedBody = DecodeBody(encodedBody);
        IReadOnlyList<KeybindSectionEntry> sections = ParseSections(decodedBody);
        IReadOnlyList<KeybindEntry> entries = ParseEntries(sections);
        byte[] paddingBytes = reader.ReadBytes((int)(stream.Length - stream.Position));

        return new KeybindParseResult
        {
            Header = header,
            DecodedBody = decodedBody,
            Sections = sections,
            Entries = entries,
            PaddingBytes = paddingBytes
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

    private static KeybindFileHeader ParseHeader(byte[] headerBytes, int headerSize, long actualFileSize)
    {
        if (headerBytes.Length < 0x0C)
        {
            throw new InvalidOperationException("The header is too short to contain the expected size fields.");
        }

        uint storedFileSize = ReadUInt32LittleEndian(headerBytes, 0x04);
        uint storedDataSize = ReadUInt32LittleEndian(headerBytes, 0x08);
        uint fileSize = checked(storedFileSize + 32U);
        uint dataSize = checked(storedDataSize + 16U);

        if (fileSize != actualFileSize)
        {
            throw new InvalidOperationException("The header file size does not match the actual file length.");
        }

        if (dataSize < headerSize || dataSize > fileSize)
        {
            throw new InvalidOperationException("The header data size is outside the valid file range.");
        }

        return new KeybindFileHeader
        {
            HeaderSize = headerSize,
            FileSize = fileSize,
            DataSize = dataSize,
            UnknownPrefixBytes = headerBytes[..0x04],
            UnknownSuffixBytes = headerBytes[0x0C..],
            RawBytes = headerBytes
        };
    }

    private static IReadOnlyList<KeybindSectionEntry> ParseSections(byte[] decodedBody)
    {
        List<KeybindSectionEntry> sections = [];
        int offset = 0;

        while (offset + 3 <= decodedBody.Length)
        {
            byte tag = decodedBody[offset];
            ushort declaredSize = (ushort)(decodedBody[offset + 1] | (decodedBody[offset + 2] << 8));

            if (declaredSize == 0)
            {
                throw new InvalidOperationException($"Encountered a zero-sized section at offset {offset}.");
            }

            int payloadOffset = offset + 3;
            int payloadEndExclusive = payloadOffset + declaredSize;

            if (payloadEndExclusive > decodedBody.Length)
            {
                throw new InvalidOperationException($"Section at offset {offset} exceeds the decoded body length.");
            }

            byte[] payloadBytes = decodedBody[payloadOffset..payloadEndExclusive];

            if (payloadBytes[^1] != 0)
            {
                throw new InvalidOperationException($"Section at offset {offset} is missing a null terminator.");
            }

            string content = DecodeNullTerminatedUtf8(payloadBytes);

            sections.Add(new KeybindSectionEntry
            {
                Index = sections.Count,
                Offset = offset,
                Tag = tag,
                DeclaredSize = declaredSize,
                PayloadLength = payloadBytes.Length,
                Content = content,
                PayloadBytes = payloadBytes
            });

            offset = payloadEndExclusive;
        }

        return sections;
    }

    private static IReadOnlyList<KeybindEntry> ParseEntries(IReadOnlyList<KeybindSectionEntry> sections)
    {
        if (sections.Count % 2 != 0)
        {
            throw new InvalidOperationException("The section count is not even, so command/binding pairs cannot be formed.");
        }

        List<KeybindEntry> entries = new(sections.Count / 2);

        for (int index = 0; index < sections.Count; index += 2)
        {
            KeybindSectionEntry commandSection = sections[index];
            KeybindSectionEntry bindingSection = sections[index + 1];
            (KeybindAssignment primary, KeybindAssignment secondary) = ParseBindingText(bindingSection.Content);

            entries.Add(new KeybindEntry
            {
                Index = entries.Count,
                CommandSectionType = commandSection.TagCharacter,
                BindingSectionType = bindingSection.TagCharacter,
                Command = commandSection.Content,
                RawBindingText = bindingSection.Content,
                Primary = primary,
                Secondary = secondary
            });
        }

        return entries;
    }

    private static (KeybindAssignment Primary, KeybindAssignment Secondary) ParseBindingText(string bindingText)
    {
        string[] pairs = bindingText.Split(',', StringSplitOptions.None);
        string primaryText = pairs.Length >= 1 ? pairs[0] : string.Empty;
        string secondaryText = pairs.Length >= 2 ? pairs[1] : string.Empty;

        return (ParseBindingPair(primaryText), ParseBindingPair(secondaryText));
    }

    private static KeybindAssignment ParseBindingPair(string pairText)
    {
        if (string.IsNullOrEmpty(pairText))
        {
            return new KeybindAssignment();
        }

        string[] parts = pairText.Split('.', StringSplitOptions.None);
        string keyCode = parts.Length >= 1 ? parts[0] : string.Empty;
        string modifierCode = parts.Length >= 2 ? parts[1] : string.Empty;

        return new KeybindAssignment
        {
            KeyCode = keyCode,
            ModifierCode = modifierCode
        };
    }

    private static string DecodeNullTerminatedUtf8(byte[] payloadBytes)
    {
        int textLength = Math.Max(0, payloadBytes.Length - 1);

        try
        {
            return Encoding.UTF8.GetString(payloadBytes, 0, textLength);
        }
        catch (DecoderFallbackException)
        {
            return Convert.ToHexString(payloadBytes);
        }
    }

    private static uint ReadUInt32LittleEndian(byte[] bytes, int offset)
    {
        return (uint)(
            bytes[offset]
            | (bytes[offset + 1] << 8)
            | (bytes[offset + 2] << 16)
            | (bytes[offset + 3] << 24));
    }
}
