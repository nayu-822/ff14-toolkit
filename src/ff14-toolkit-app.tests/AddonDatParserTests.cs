using FF14Toolkit.App.Services.Addon;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace FF14Toolkit.App.Tests;

[TestClass]
public sealed class AddonDatParserTests
{
    [TestMethod]
    public void Parse_ReadsHeaderAndEntries()
    {
        byte[] fileBytes = BuildAddonDatFile();
        using MemoryStream stream = new(fileBytes);

        AddonDatParser parser = new();
        var result = parser.Parse(stream);

        Assert.AreEqual("ADDN", result.Header.Magic);
        Assert.AreEqual("Default", result.Header.DataSetName);
        Assert.AreEqual(2, result.Entries.Count);
        Assert.AreEqual(0x12345678U, result.Entries[0].AddonNameHash);
        Assert.AreEqual(50f, result.Entries[0].X);
        Assert.AreEqual(0x9ABCDEF0U, result.Entries[1].AddonNameHash);
        Assert.AreEqual(0.8f, result.Entries[1].Scale, 0.0001f);
    }

    [TestMethod]
    public void Parse_ResolvesPartyListDisplayName()
    {
        byte[] fileBytes = BuildAddonDatFile(ComputeCrc32("PartyList_a"));
        using MemoryStream stream = new(fileBytes);

        AddonDatParser parser = new();
        var result = parser.Parse(stream);

        Assert.AreEqual("PartyList", result.Entries[0].DisplayName);
    }

    [TestMethod]
    public void Parse_ResolvesHudLayoutWindowDisplayName()
    {
        byte[] fileBytes = BuildAddonDatFile(ComputeCrc32("HudLayoutWindow_a"));
        using MemoryStream stream = new(fileBytes);

        AddonDatParser parser = new();
        var result = parser.Parse(stream);

        Assert.AreEqual("HudLayoutWindow", result.Entries[0].DisplayName);
    }

    [TestMethod]
    public void Parse_LeavesNonClientStructsNameUnresolved()
    {
        byte[] fileBytes = BuildAddonDatFile(ComputeCrc32("AddonContextSub_a"));
        using MemoryStream stream = new(fileBytes);

        AddonDatParser parser = new();
        var result = parser.Parse(stream);

        Assert.AreEqual("0x467967B5", result.Entries[0].DisplayName);
    }

    [TestMethod]
    public void Parse_LeavesTargetInfoUnresolved()
    {
        byte[] fileBytes = BuildAddonDatFile(ComputeCrc32("TargetInfo_a"));
        using MemoryStream stream = new(fileBytes);

        AddonDatParser parser = new();
        var result = parser.Parse(stream);

        Assert.AreEqual("0xCB65ABD4", result.Entries[0].DisplayName);
    }

    private static byte[] BuildAddonDatFile(uint firstEntryHash = 0x12345678U)
    {
        byte[] header = new byte[AddonDatParser.DefaultHeaderSize];
        byte[] entry1 = BuildEntry(firstEntryHash, 50f, 48f, 1f, 0U, 400, 200, 4, 0, 0, 0, 0, 0);
        byte[] entry2 = BuildEntry(0x9ABCDEF0U, 42f, 24f, 0.8f, 1U, 280, 140, 2, 1, 0, 255, 1, 0);
        byte[] body = new byte[entry1.Length + entry2.Length + 0x100];

        Buffer.BlockCopy(entry1, 0, body, 0, entry1.Length);
        Buffer.BlockCopy(entry2, 0, body, entry1.Length, entry2.Length);

        uint fileSize = checked((uint)(header.Length + body.Length));
        uint storedSize = fileSize - 32U;

        WriteUInt32(header, 0x04, storedSize);
        WriteUInt32(header, 0x08, storedSize);
        header[0x10] = (byte)'A';
        header[0x11] = (byte)'D';
        header[0x12] = (byte)'D';
        header[0x13] = (byte)'N';
        WriteUInt32(header, 0x14, 1U);
        WriteAscii(header, 0x30, "Default");

        byte[] fileBytes = new byte[fileSize];
        Buffer.BlockCopy(header, 0, fileBytes, 0, header.Length);
        Buffer.BlockCopy(body, 0, fileBytes, header.Length, body.Length);
        return fileBytes;
    }

    private static byte[] BuildEntry(
        uint hash,
        float x,
        float y,
        float scale,
        uint flags,
        ushort width,
        ushort height,
        byte state1,
        byte state2,
        byte state3,
        byte alpha,
        byte state4,
        byte state5)
    {
        byte[] bytes = new byte[0x20];
        WriteUInt32(bytes, 0x00, hash);
        WriteSingle(bytes, 0x04, x);
        WriteSingle(bytes, 0x08, y);
        WriteSingle(bytes, 0x0C, scale);
        WriteUInt32(bytes, 0x10, flags);
        WriteUInt16(bytes, 0x14, width);
        WriteUInt16(bytes, 0x16, height);
        bytes[0x18] = state1;
        bytes[0x19] = state2;
        bytes[0x1A] = state3;
        bytes[0x1B] = alpha;
        bytes[0x1C] = state4;
        bytes[0x1D] = state5;
        return bytes;
    }

    private static void WriteAscii(byte[] bytes, int offset, string value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            bytes[offset + index] = (byte)value[index];
        }
    }

    private static void WriteSingle(byte[] bytes, int offset, float value)
    {
        Buffer.BlockCopy(BitConverter.GetBytes(value), 0, bytes, offset, sizeof(float));
    }

    private static void WriteUInt16(byte[] bytes, int offset, ushort value)
    {
        Buffer.BlockCopy(BitConverter.GetBytes(value), 0, bytes, offset, sizeof(ushort));
    }

    private static void WriteUInt32(byte[] bytes, int offset, uint value)
    {
        Buffer.BlockCopy(BitConverter.GetBytes(value), 0, bytes, offset, sizeof(uint));
    }

    private static uint ComputeCrc32(string text)
    {
        uint crc = 0xFFFFFFFF;

        foreach (byte value in System.Text.Encoding.ASCII.GetBytes(text))
        {
            crc ^= value;

            for (int index = 0; index < 8; index++)
            {
                crc = (crc & 1) != 0
                    ? 0xEDB88320U ^ (crc >> 1)
                    : crc >> 1;
            }
        }

        return ~crc;
    }
}
