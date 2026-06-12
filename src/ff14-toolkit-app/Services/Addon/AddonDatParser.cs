using FF14Toolkit.App.Models.Addon;
using System.IO;
using System.Text;

namespace FF14Toolkit.App.Services.Addon;

public sealed class AddonDatParser
{
    public const int DefaultHeaderSize = 0x70;

    private const int EntryOffset = 0x70;
    private const int EntrySize = 0x20;
    private const int ZeroRunThreshold = 0x100;

    public AddonParseResult ParseFile(string filePath, int headerSize = DefaultHeaderSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        using FileStream stream = new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);

        return Parse(stream, headerSize);
    }

    public AddonParseResult Parse(Stream stream, int headerSize = DefaultHeaderSize)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (headerSize < DefaultHeaderSize)
        {
            throw new InvalidOperationException("The ADDON.DAT header size is smaller than the minimum supported header.");
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
        AddonFileHeader header = ParseHeader(headerBytes, headerSize, stream.Length);
        int validBodyLength = checked((int)Math.Max(0, header.DataSize - headerSize));

        if (validBodyLength > stream.Length - stream.Position)
        {
            throw new InvalidOperationException("The file is smaller than the data size declared in the header.");
        }

        byte[] bodyBytes = reader.ReadBytes(validBodyLength);
        IReadOnlyList<AddonLayoutEntry> entries = ParseEntries(bodyBytes);

        return new AddonParseResult
        {
            Header = header,
            Entries = entries
        };
    }

    private static AddonFileHeader ParseHeader(byte[] headerBytes, int headerSize, long actualFileSize)
    {
        if (headerBytes.Length < DefaultHeaderSize)
        {
            throw new InvalidOperationException("The header is too short to contain the expected ADDON.DAT metadata.");
        }

        uint storedFileSize = ReadUInt32LittleEndian(headerBytes, 0x04);
        uint storedDataSize = ReadUInt32LittleEndian(headerBytes, 0x08);
        uint fileSize = checked(storedFileSize + 32U);
        uint dataSize = checked(storedDataSize + 32U);

        if (fileSize != actualFileSize)
        {
            throw new InvalidOperationException("The header file size does not match the actual file length.");
        }

        string magic = Encoding.ASCII.GetString(headerBytes, 0x10, 4);
        if (!string.Equals(magic, "ADDN", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The file does not contain the expected ADDN header.");
        }

        return new AddonFileHeader
        {
            HeaderSize = headerSize,
            FileSize = fileSize,
            DataSize = dataSize,
            Magic = magic,
            Version = ReadUInt32LittleEndian(headerBytes, 0x14),
            DataSetName = ReadNullTerminatedAscii(headerBytes, 0x30, 16),
            RawBytes = headerBytes
        };
    }

    private static IReadOnlyList<AddonLayoutEntry> ParseEntries(byte[] bodyBytes)
    {
        List<AddonLayoutEntry> entries = [];
        int zeroRunLength = 0;

        for (int offset = 0; offset + EntrySize <= bodyBytes.Length; offset += EntrySize)
        {
            if (IsAllZero(bodyBytes, offset, EntrySize))
            {
                zeroRunLength += EntrySize;
                if (zeroRunLength >= ZeroRunThreshold && entries.Count > 0)
                {
                    break;
                }

                continue;
            }

            zeroRunLength = 0;
            entries.Add(ParseEntry(bodyBytes, offset));
        }

        return entries;
    }

    private static AddonLayoutEntry ParseEntry(byte[] bodyBytes, int offset)
    {
        uint hash = ReadUInt32LittleEndian(bodyBytes, offset);

        return new AddonLayoutEntry
        {
            AddonNameHash = hash,
            X = ReadSingleLittleEndian(bodyBytes, offset + 0x04),
            Y = ReadSingleLittleEndian(bodyBytes, offset + 0x08),
            Scale = ReadSingleLittleEndian(bodyBytes, offset + 0x0C),
            ElementFlags = ReadUInt32LittleEndian(bodyBytes, offset + 0x10),
            Width = ReadUInt16LittleEndian(bodyBytes, offset + 0x14),
            Height = ReadUInt16LittleEndian(bodyBytes, offset + 0x16),
            StateByte1 = bodyBytes[offset + 0x18],
            StateByte2 = bodyBytes[offset + 0x19],
            StateByte3 = bodyBytes[offset + 0x1A],
            Alpha = bodyBytes[offset + 0x1B],
            StateByte4 = bodyBytes[offset + 0x1C],
            StateByte5 = bodyBytes[offset + 0x1D],
            DisplayName = AddonNameHashResolver.Resolve(hash)
        };
    }

    private static bool IsAllZero(byte[] buffer, int offset, int length)
    {
        for (int index = 0; index < length; index++)
        {
            if (buffer[offset + index] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static string ReadNullTerminatedAscii(byte[] bytes, int offset, int maxLength)
    {
        int textLength = 0;
        while (textLength < maxLength && bytes[offset + textLength] != 0)
        {
            textLength++;
        }

        return textLength == 0
            ? string.Empty
            : Encoding.ASCII.GetString(bytes, offset, textLength);
    }

    private static float ReadSingleLittleEndian(byte[] bytes, int offset)
    {
        return BitConverter.ToSingle(bytes, offset);
    }

    private static ushort ReadUInt16LittleEndian(byte[] bytes, int offset)
    {
        return BitConverter.ToUInt16(bytes, offset);
    }

    private static uint ReadUInt32LittleEndian(byte[] bytes, int offset)
    {
        return BitConverter.ToUInt32(bytes, offset);
    }
}
