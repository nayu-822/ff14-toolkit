using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace FF14Toolkit.App.Services.TemplateMatching.Resources;

public sealed class PpmP6TemplateLoader : ITemplateResourceLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Lock syncRoot = new();
    private readonly Dictionary<string, TemplateResource> cache = new(StringComparer.OrdinalIgnoreCase);

    public TemplateResource Load(TemplateResourceDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.TemplateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.MetadataPath);

        string metadataPath = Path.GetFullPath(definition.MetadataPath);

        lock (syncRoot)
        {
            if (cache.TryGetValue(metadataPath, out TemplateResource? cached))
            {
                return cached;
            }

            TemplateResource loaded = LoadCore(definition, metadataPath);
            cache[metadataPath] = loaded;
            return loaded;
        }
    }

    private static TemplateResource LoadCore(TemplateResourceDefinition definition, string metadataPath)
    {
        if (!File.Exists(metadataPath))
        {
            throw new FileNotFoundException($"Template metadata was not found: {metadataPath}", metadataPath);
        }

        using FileStream metadataStream = File.OpenRead(metadataPath);
        TemplateResourceMetadata metadata = JsonSerializer.Deserialize<TemplateResourceMetadata>(metadataStream, JsonOptions)
            ?? throw new InvalidDataException($"Template metadata is invalid: {metadataPath}");

        ValidateMetadata(definition.TemplateId, metadata, metadataPath);

        string templatePath = Path.Combine(Path.GetDirectoryName(metadataPath)!, metadata.Template);
        TemplateImage image = LoadImage(templatePath);
        if (image.Width != metadata.ReferenceTemplateWidth || image.Height != metadata.ReferenceTemplateHeight)
        {
            throw new InvalidDataException($"Template size does not match metadata: {templatePath}");
        }

        return new TemplateResource(definition, metadata, image);
    }

    private static void ValidateMetadata(string templateId, TemplateResourceMetadata metadata, string metadataPath)
    {
        if (!string.Equals(templateId, metadata.TemplateId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"TemplateId mismatch: {metadataPath}");
        }

        if (metadata.ReferenceTemplateWidth <= 0 || metadata.ReferenceTemplateHeight <= 0)
        {
            throw new InvalidDataException($"Template dimensions are invalid: {metadataPath}");
        }

        if (metadata.MinimumScore is < 0d or > 1d)
        {
            throw new InvalidDataException($"MinimumScore is invalid: {metadataPath}");
        }
    }

    private static TemplateImage LoadImage(string templatePath)
    {
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"Template image was not found: {templatePath}", templatePath);
        }

        using FileStream stream = File.OpenRead(templatePath);
        string magic = ReadToken(stream, templatePath);
        if (!string.Equals(magic, "P6", StringComparison.Ordinal)
            && !string.Equals(magic, "P3", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unsupported template format: {templatePath}");
        }

        int width = int.Parse(ReadToken(stream, templatePath), CultureInfo.InvariantCulture);
        int height = int.Parse(ReadToken(stream, templatePath), CultureInfo.InvariantCulture);
        int maxValue = int.Parse(ReadToken(stream, templatePath), CultureInfo.InvariantCulture);
        if (maxValue != 255)
        {
            throw new InvalidDataException($"Unsupported template max value: {templatePath}");
        }

        return string.Equals(magic, "P6", StringComparison.Ordinal)
            ? LoadP6Pixels(stream, templatePath, width, height)
            : LoadP3Pixels(stream, templatePath, width, height);
    }

    private static TemplateImage LoadP6Pixels(FileStream stream, string templatePath, int width, int height)
    {
        int pixelLength = checked(width * height * 3);
        byte[] pixels = new byte[pixelLength];
        int offset = 0;
        while (offset < pixelLength)
        {
            int read = stream.Read(pixels, offset, pixelLength - offset);
            if (read <= 0)
            {
                throw new InvalidDataException($"Template pixel data is incomplete: {templatePath}");
            }

            offset += read;
        }

        if (stream.ReadByte() >= 0)
        {
            throw new InvalidDataException($"Template pixel data contains trailing bytes: {templatePath}");
        }

        return new TemplateImage(width, height, pixels);
    }

    private static TemplateImage LoadP3Pixels(FileStream stream, string templatePath, int width, int height)
    {
        int pixelLength = checked(width * height * 3);
        byte[] pixels = new byte[pixelLength];
        for (int i = 0; i < pixelLength; i++)
        {
            pixels[i] = byte.Parse(ReadToken(stream, templatePath), CultureInfo.InvariantCulture);
        }

        string? extraToken = TryReadToken(stream, templatePath);
        if (extraToken is not null)
        {
            throw new InvalidDataException($"Template pixel data contains trailing tokens: {templatePath}");
        }

        return new TemplateImage(width, height, pixels);
    }

    private static string ReadToken(Stream stream, string templatePath)
    {
        SkipWhitespaceAndComments(stream);

        List<byte> bytes = [];
        while (true)
        {
            int next = stream.ReadByte();
            if (next < 0)
            {
                break;
            }

            if (char.IsWhiteSpace((char)next))
            {
                break;
            }

            if (next == '#')
            {
                throw new InvalidDataException($"Unexpected comment marker in token: {templatePath}");
            }

            bytes.Add((byte)next);
        }

        if (bytes.Count == 0)
        {
            throw new InvalidDataException($"Template header is incomplete: {templatePath}");
        }

        return Encoding.ASCII.GetString(bytes.ToArray());
    }

    private static string? TryReadToken(Stream stream, string templatePath)
    {
        SkipWhitespaceAndComments(stream);
        if (stream.Position >= stream.Length)
        {
            return null;
        }

        return ReadToken(stream, templatePath);
    }

    private static void SkipWhitespaceAndComments(Stream stream)
    {
        while (true)
        {
            int next = stream.ReadByte();
            if (next < 0)
            {
                return;
            }

            if (char.IsWhiteSpace((char)next))
            {
                continue;
            }

            if (next == '#')
            {
                SkipComment(stream);
                continue;
            }

            stream.Position--;
            return;
        }
    }

    private static void SkipComment(Stream stream)
    {
        while (true)
        {
            int next = stream.ReadByte();
            if (next < 0 || next == '\n')
            {
                return;
            }
        }
    }
}
