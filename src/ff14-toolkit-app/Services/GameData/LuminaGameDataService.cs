using FF14Toolkit.App.Models.GameData;
using FF14Toolkit.App.Models.Configuration;
using FF14Toolkit.App.Services.Localization;
using AppLuminaOptions = FF14Toolkit.App.Models.Configuration.LuminaOptions;
using Lumina;
using Lumina.Data;
using Lumina.Data.Files;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ActionSheet = Lumina.Excel.Sheets.Action;
using CraftActionSheet = Lumina.Excel.Sheets.CraftAction;
using MapSheet = Lumina.Excel.Sheets.Map;
using TerritoryTypeSheet = Lumina.Excel.Sheets.TerritoryType;

namespace FF14Toolkit.App.Services.GameData;

public sealed class LuminaGameDataService : IGameDataService, IDisposable
{
    private static readonly Language DefaultExcelLanguage = Language.Japanese;
    private static readonly string[] KnownSqPackPaths =
    [
        @"C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack",
        @"C:\Program Files\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack",
        @"C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY XIV Online\game\sqpack",
        @"C:\Program Files\Steam\steamapps\common\FINAL FANTASY XIV Online\game\sqpack"
    ];

    private readonly SemaphoreSlim initializationLock = new(1, 1);
    private readonly AppLuminaOptions options;
    private readonly CacheOptions cacheOptions;
    private readonly ILocalizationService localizationService;
    private Lumina.GameData? gameData;
    private string? errorMessage;
    private string? resolvedSqPackPath;
    private bool disposed;

    public LuminaGameDataService(
        IOptions<AppLuminaOptions> options,
        IOptions<CacheOptions> cacheOptions,
        ILocalizationService localizationService)
    {
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.cacheOptions = cacheOptions?.Value ?? throw new ArgumentNullException(nameof(cacheOptions));
        this.localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SqPackPath);

    public bool IsAvailable => gameData is not null;

    public string? SqPackPath => resolvedSqPackPath ??= ResolveSqPackPath();

    public string? ErrorMessage => errorMessage;

    public Language ExcelLanguage => ConvertExcelLanguage(options.ExcelLanguage);

    public string? ResolveHotbarCommandName(byte slotTypeId, uint commandId)
    {
        if (gameData is null || commandId == 0)
        {
            return null;
        }

        Language language = ResolveContentLanguage();

        return slotTypeId switch
        {
            1 => GetRowName(gameData.GetExcelSheet<ActionSheet>(language, null), commandId, row => row.Name.ExtractText()),
            2 => GetItemName(commandId),
            9 => GetRowName(gameData.GetExcelSheet<CraftActionSheet>(language, null), commandId, row => row.Name.ExtractText()),
            10 => GetRowName(gameData.GetExcelSheet<GeneralAction>(language, null), commandId, row => row.Name.ExtractText()),
            _ => null
        };
    }

    public BitmapSource? ResolveHotbarCommandIcon(byte slotTypeId, uint commandId)
    {
        if (gameData is null || commandId == 0)
        {
            return null;
        }

        Language language = ResolveContentLanguage();
        string? iconPath = slotTypeId switch
        {
            1 => GetRowIconPath(gameData.GetExcelSheet<ActionSheet>(language, null), commandId),
            2 => GetItemIconPath(commandId, language),
            9 => GetRowIconPath(gameData.GetExcelSheet<CraftActionSheet>(language, null), commandId),
            10 => GetRowIconPath(gameData.GetExcelSheet<GeneralAction>(language, null), commandId),
            _ => null
        };

        return string.IsNullOrWhiteSpace(iconPath)
            ? null
            : ResolveIcon(iconPath);
    }

    public string? ResolveClassJobName(int classJobId)
    {
        if (gameData is null || classJobId <= 0)
        {
            return null;
        }

        Language language = ResolveContentLanguage();
        return GetRowName(gameData.GetExcelSheet<ClassJob>(language, null), (uint)classJobId, row => row.Name.ExtractText());
    }

    public BitmapSource? ResolveIcon(string iconPath)
    {
        if (gameData is null || string.IsNullOrWhiteSpace(iconPath))
        {
            return null;
        }

        TexFile? texFile = gameData.GetFile<TexFile>(iconPath);
        if (texFile?.ImageData is not { Length: > 0 } imageData)
        {
            return null;
        }

        int width = texFile.Header.Width;
        int height = texFile.Header.Height;
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        BitmapSource bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            imageData,
            width * 4);

        bitmap.Freeze();
        return bitmap;
    }

    public string FormatMapCoordinates(uint? mapId, uint? territoryTypeId, string? mapName, double posX, double posY)
    {
        if (gameData is null)
        {
            return FormatRawCoordinates(posX, posY);
        }

        object? mapRow = ResolveMapRow(mapId, territoryTypeId, mapName, ExcelLanguage);
        if (mapRow is null)
        {
            return FormatRawCoordinates(posX, posY);
        }

        double? sizeFactor = ReadNumericProperty(mapRow, "SizeFactor");
        double? offsetX = ReadNumericProperty(mapRow, "OffsetX");
        double? offsetY = ReadNumericProperty(mapRow, "OffsetY");
        if (sizeFactor is null or <= 0 || offsetX is null || offsetY is null)
        {
            return FormatRawCoordinates(posX, posY);
        }

        double mapX = ConvertToMapCoordinate(posX, offsetX.Value, sizeFactor.Value);
        double mapY = ConvertToMapCoordinate(posY, offsetY.Value, sizeFactor.Value);
        return FormatDisplayCoordinates(mapX, mapY);
    }

    public string DescribeMapCoordinateResolution(uint? mapId, uint? territoryTypeId, string? mapName, double posX, double posY)
    {
        if (gameData is null)
        {
            return "Lumina unavailable";
        }

        string territoryMapDescription = DescribeResolvedMapRow(
            "territoryType.Map",
            ResolveMapRowFromTerritoryType(territoryTypeId, ExcelLanguage),
            posX,
            posY);
        string directMapDescription = DescribeResolvedMapRow(
            "mapId",
            ResolveMapRowFromMapId(mapId, ExcelLanguage),
            posX,
            posY);
        string nameMapDescription = DescribeResolvedMapRow(
            "mapName",
            ResolveMapRowByMapName(mapName, ExcelLanguage),
            posX,
            posY);
        string finalMapDescription = DescribeResolvedMapRow(
            "final",
            ResolveMapRow(mapId, territoryTypeId, mapName, ExcelLanguage),
            posX,
            posY);

        return $"input(mapId={mapId?.ToString() ?? "-"}, territoryTypeId={territoryTypeId?.ToString() ?? "-"}, mapName={mapName ?? "-"}) | {territoryMapDescription} | {directMapDescription} | {nameMapDescription} | {finalMapDescription}";
    }

    public ResolvedMapInfo? ResolveMapInfo(uint? mapId, uint? territoryTypeId, string? mapName)
    {
        if (gameData is null)
        {
            return null;
        }

        Language language = ResolveContentLanguage();
        TerritoryTypeSheet? territoryType = ResolveTerritoryTypeRow(territoryTypeId, language);
        object? mapRow = ResolveMapRow(mapId, territoryTypeId, mapName, language);
        if (territoryType is null && mapRow is null)
        {
            return null;
        }

        string regionName = FirstNonEmptyString(
            territoryType is null ? null : ReadReferencedName(territoryType, "PlaceNameRegion"),
            territoryType is null ? null : ReadReferencedName(territoryType, "RegionPlaceName"),
            "-");
        string resolvedMapName = FirstNonEmptyString(
            mapRow is null ? null : ReadReferencedName(mapRow, "PlaceName"),
            territoryType is null ? null : ReadReferencedName(territoryType, "PlaceName"),
            "-");

        return new ResolvedMapInfo
        {
            MapId = mapId,
            TerritoryTypeId = territoryTypeId,
            RegionName = regionName,
            MapName = resolvedMapName,
            MapImage = mapRow is null ? null : ResolveMapImage(mapRow)
        };
    }

    public async Task<GameDataStatus> CheckAvailabilityAsync()
    {
        if (!IsConfigured)
        {
            errorMessage = null;
            return CreateStatus(GameDataAvailabilityState.Unconfigured);
        }

        if (!Directory.Exists(SqPackPath))
        {
            errorMessage = null;
            return CreateStatus(GameDataAvailabilityState.PathNotFound);
        }

        if (gameData is not null)
        {
            return CreateStatus(GameDataAvailabilityState.Ready);
        }

        await initializationLock.WaitAsync().ConfigureAwait(false);

        try
        {
            if (gameData is not null)
            {
                return CreateStatus(GameDataAvailabilityState.Ready);
            }

            try
            {
                gameData = await Task.Run(InitializeGameData).ConfigureAwait(false);
                errorMessage = null;
                return CreateStatus(GameDataAvailabilityState.Ready);
            }
            catch (Exception ex)
            {
                gameData?.Dispose();
                gameData = null;
                errorMessage = ex.Message;
                return CreateStatus(GameDataAvailabilityState.InitializationFailed);
            }
        }
        finally
        {
            initializationLock.Release();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        initializationLock.Dispose();
        gameData?.Dispose();
        disposed = true;
    }

    private Lumina.GameData InitializeGameData()
    {
        string path = SqPackPath
            ?? throw new InvalidOperationException("SqPackPath is not configured.");

        return new Lumina.GameData(path, new Lumina.LuminaOptions
        {
            LoadMultithreaded = false
        });
    }

    private string? ResolveSqPackPath()
    {
        string? configuredPath = NormalizePath(options.SqPackPath);
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath;
        }

        foreach (string candidate in KnownSqPackPaths)
        {
            string normalizedCandidate = NormalizePath(candidate)
                ?? candidate;

            if (Directory.Exists(normalizedCandidate))
            {
                return normalizedCandidate;
            }
        }

        return configuredPath;
    }

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string expandedPath = Environment.ExpandEnvironmentVariables(path.Trim());
        return Path.GetFullPath(expandedPath);
    }

    private GameDataStatus CreateStatus(GameDataAvailabilityState state)
    {
        return new GameDataStatus
        {
            State = state,
            IsConfigured = IsConfigured,
            IsAvailable = IsAvailable,
            SqPackPath = SqPackPath,
            ErrorMessage = errorMessage
        };
    }

    private string? GetItemName(uint commandId)
    {
        if (gameData is null)
        {
            return null;
        }

        uint normalizedCommandId = commandId >= 2_000_000
            ? commandId - 2_000_000
            : commandId >= 1_000_000
                ? commandId - 1_000_000
                : commandId;

        return GetRowName(gameData.GetExcelSheet<Item>(ResolveContentLanguage(), null), normalizedCommandId, row => row.Name.ExtractText());
    }

    private string? GetItemIconPath(uint commandId, Language language)
    {
        if (gameData is null)
        {
            return null;
        }

        uint normalizedCommandId = commandId >= 2_000_000
            ? commandId - 2_000_000
            : commandId >= 1_000_000
                ? commandId - 1_000_000
                : commandId;

        return GetRowIconPath(gameData.GetExcelSheet<Item>(language, null), normalizedCommandId);
    }

    private static string? GetRowName<TRow>(ExcelSheet<TRow>? sheet, uint rowId, Func<TRow, string?> selector)
        where TRow : struct, IExcelRow<TRow>
    {
        if (sheet is null)
        {
            return null;
        }

        TRow? row = sheet.GetRowOrDefault(rowId);
        return row is null ? null : selector(row.Value);
    }

    private static string? GetRowIconPath<TRow>(ExcelSheet<TRow>? sheet, uint rowId)
        where TRow : struct, IExcelRow<TRow>
    {
        if (sheet is null)
        {
            return null;
        }

        TRow? row = sheet.GetRowOrDefault(rowId);
        if (row is null)
        {
            return null;
        }

        uint? iconId = ReadIconId(row.Value);
        return iconId is > 0
            ? BuildIconPath(iconId.Value)
            : null;
    }

    private static Language ConvertExcelLanguage(LuminaExcelLanguage excelLanguage)
    {
        return excelLanguage switch
        {
            LuminaExcelLanguage.Japanese => Language.Japanese,
            LuminaExcelLanguage.English => Language.English,
            LuminaExcelLanguage.German => Language.German,
            LuminaExcelLanguage.French => Language.French,
            _ => DefaultExcelLanguage
        };
    }

    private object? ResolveMapRow(uint? mapId, uint? territoryTypeId, string? mapName, Language language)
    {
        return ResolveMapRowFromTerritoryType(territoryTypeId, language)
            ?? ResolveMapRowFromMapId(mapId, language)
            ?? ResolveMapRowByMapName(mapName, language);
    }

    private object? ResolveMapRowFromTerritoryType(uint? territoryTypeId, Language language)
    {
        ExcelSheet<MapSheet>? mapSheet = gameData?.GetExcelSheet<MapSheet>(language, null);
        if (mapSheet is null)
        {
            return null;
        }

        if (territoryTypeId is not uint resolvedTerritoryTypeId)
        {
            return null;
        }

        ExcelSheet<TerritoryTypeSheet>? territoryTypeSheet = gameData?.GetExcelSheet<TerritoryTypeSheet>(language, null);
        TerritoryTypeSheet? territoryType = territoryTypeSheet?.GetRowOrDefault(resolvedTerritoryTypeId);
        if (territoryType is null)
        {
            return null;
        }

        object territoryRow = territoryType;
        object? mapReference = territoryRow.GetType().GetProperty("Map")?.GetValue(territoryRow);
        if (mapReference is null)
        {
            return null;
        }

        object? mapValue = mapReference.GetType().GetProperty("Value")?.GetValue(mapReference);
        if (mapValue is not null)
        {
            return mapValue;
        }

        object? mapRowId = mapReference.GetType().GetProperty("RowId")?.GetValue(mapReference);
        if (TryConvertToUInt(mapRowId, out uint territoryMapRowId))
        {
            MapSheet? territoryMap = mapSheet.GetRowOrDefault(territoryMapRowId);
            if (territoryMap is not null)
            {
                return territoryMap;
            }
        }

        return null;
    }

    private object? ResolveMapRowFromMapId(uint? mapId, Language language)
    {
        if (gameData is null || mapId is not uint resolvedMapId)
        {
            return null;
        }

        ExcelSheet<MapSheet>? mapSheet = gameData.GetExcelSheet<MapSheet>(language, null);
        return mapSheet?.GetRowOrDefault(resolvedMapId);
    }

    private TerritoryTypeSheet? ResolveTerritoryTypeRow(uint? territoryTypeId, Language language)
    {
        if (gameData is null || territoryTypeId is not uint resolvedTerritoryTypeId)
        {
            return null;
        }

        ExcelSheet<TerritoryTypeSheet>? territoryTypeSheet = gameData.GetExcelSheet<TerritoryTypeSheet>(language, null);
        return territoryTypeSheet?.GetRowOrDefault(resolvedTerritoryTypeId);
    }

    private object? ResolveMapRowByMapName(string? mapName, Language language)
    {
        if (gameData is null || string.IsNullOrWhiteSpace(mapName))
        {
            return null;
        }

        string normalizedTargetName = NormalizeComparisonText(mapName);
        ExcelSheet<TerritoryTypeSheet>? territoryTypeSheet = gameData.GetExcelSheet<TerritoryTypeSheet>(language, null);
        if (territoryTypeSheet is null)
        {
            return null;
        }

        foreach (TerritoryTypeSheet territoryType in territoryTypeSheet)
        {
            string? placeName = ReadReferencedName(territoryType, "PlaceName");
            if (!string.Equals(NormalizeComparisonText(placeName), normalizedTargetName, StringComparison.Ordinal))
            {
                continue;
            }

            object? mapReference = territoryType.GetType().GetProperty("Map")?.GetValue(territoryType);
            if (mapReference is null)
            {
                continue;
            }

            object? mapValue = mapReference.GetType().GetProperty("Value")?.GetValue(mapReference);
            if (mapValue is not null)
            {
                return mapValue;
            }

            object? mapRowId = mapReference.GetType().GetProperty("RowId")?.GetValue(mapReference);
            if (!TryConvertToUInt(mapRowId, out uint fallbackMapRowId))
            {
                continue;
            }

            ExcelSheet<MapSheet>? mapSheet = gameData.GetExcelSheet<MapSheet>(language, null);
            MapSheet? fallbackMap = mapSheet?.GetRowOrDefault(fallbackMapRowId);
            if (fallbackMap is not null)
            {
                return fallbackMap;
            }
        }

        return null;
    }

    private static bool TryConvertToUInt(object? value, out uint result)
    {
        switch (value)
        {
            case byte byteValue:
                result = byteValue;
                return true;
            case ushort ushortValue:
                result = ushortValue;
                return true;
            case uint uintValue:
                result = uintValue;
                return true;
            case int intValue when intValue >= 0:
                result = (uint)intValue;
                return true;
            case long longValue when longValue >= 0 && longValue <= uint.MaxValue:
                result = (uint)longValue;
                return true;
            default:
                result = 0;
                return false;
        }
    }

    private static double? ReadNumericProperty(object instance, string propertyName)
    {
        object? value = instance.GetType().GetProperty(propertyName)?.GetValue(instance);
        return value switch
        {
            byte byteValue => byteValue,
            short shortValue => shortValue,
            ushort ushortValue => ushortValue,
            int intValue => intValue,
            uint uintValue => uintValue,
            long longValue => longValue,
            float floatValue => floatValue,
            double doubleValue => doubleValue,
            _ => null
        };
    }

    private static string? ReadReferencedName(object instance, string propertyName)
    {
        object? reference = instance.GetType().GetProperty(propertyName)?.GetValue(instance);
        if (reference is null)
        {
            return null;
        }

        object? referencedValue = reference.GetType().GetProperty("Value")?.GetValue(reference);
        if (referencedValue is null)
        {
            return null;
        }

        object? nameValue = referencedValue.GetType().GetProperty("Name")?.GetValue(referencedValue);
        return ConvertTextValue(nameValue);
    }

    private static string? ConvertTextValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is string text)
        {
            return text;
        }

        System.Reflection.MethodInfo? extractTextMethod = value.GetType().GetMethod("ExtractText", Type.EmptyTypes);
        if (extractTextMethod?.Invoke(value, null) is string extractedText)
        {
            return extractedText;
        }

        return value.ToString();
    }

    private static string NormalizeComparisonText(string? text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : text.Trim();
    }

    private static double ConvertToMapCoordinate(double position, double offset, double sizeFactor)
    {
        double scale = sizeFactor / 100d;
        return ((((position + offset) * scale) + 1024d) / 2048d) * 41d / scale + 1d;
    }

    private static double ConvertToMapCoordinateWithSubtractedOffset(double position, double offset, double sizeFactor)
    {
        double scale = sizeFactor / 100d;
        return ((41d / scale) * ((position - offset) / 2048d)) + 1d;
    }

    private string DescribeResolvedMapRow(string source, object? mapRow, double posX, double posY)
    {
        if (mapRow is null)
        {
            return $"{source}=null";
        }

        uint? rowId = ReadRowId(mapRow);
        double? sizeFactor = ReadNumericProperty(mapRow, "SizeFactor");
        double? offsetX = ReadNumericProperty(mapRow, "OffsetX");
        double? offsetY = ReadNumericProperty(mapRow, "OffsetY");
        string? id = ReadReferencedName(mapRow, "Id");
        string? placeName = ReadReferencedName(mapRow, "PlaceName");

        if (sizeFactor is null or <= 0 || offsetX is null || offsetY is null)
        {
            return $"{source}=row:{rowId?.ToString() ?? "-"} id:{id ?? "-"} place:{placeName ?? "-"} invalid-coordinates";
        }

        double standardX = ConvertToMapCoordinate(posX, offsetX.Value, sizeFactor.Value);
        double standardY = ConvertToMapCoordinate(posY, offsetY.Value, sizeFactor.Value);
        double subtractedX = ConvertToMapCoordinateWithSubtractedOffset(posX, offsetX.Value, sizeFactor.Value);
        double subtractedY = ConvertToMapCoordinateWithSubtractedOffset(posY, offsetY.Value, sizeFactor.Value);
        double swappedX = ConvertToMapCoordinate(posX, offsetY.Value, sizeFactor.Value);
        double swappedY = ConvertToMapCoordinate(posY, offsetX.Value, sizeFactor.Value);

        return $"{source}=row:{rowId?.ToString() ?? "-"} id:{id ?? "-"} place:{placeName ?? "-"} size:{sizeFactor:0.###} offsetX:{offsetX:0.###} offsetY:{offsetY:0.###} standard:X:{standardX:0.000}/Y:{standardY:0.000} subtract:X:{subtractedX:0.000}/Y:{subtractedY:0.000} swapped:X:{swappedX:0.000}/Y:{swappedY:0.000}";
    }

    private static uint? ReadRowId(object instance)
    {
        object? rowId = instance.GetType().GetProperty("RowId")?.GetValue(instance);
        return TryConvertToUInt(rowId, out uint converted) ? converted : null;
    }

    private BitmapSource? ResolveMapImage(object mapRow)
    {
        string? mapKey = ReadStringValue(mapRow, "Id");
        uint? rowId = ReadRowId(mapRow);
        string cacheFilePath = GetMapCacheFilePath(rowId, mapKey);
        BitmapSource? cachedBitmap = TryLoadBitmapFromCache(cacheFilePath);
        if (cachedBitmap is not null)
        {
            return cachedBitmap;
        }

        foreach (string candidatePath in BuildMapTexturePathCandidates(mapKey))
        {
            BitmapSource? bitmap = CreateBitmapSource(gameData?.GetFile<TexFile>(candidatePath));
            if (bitmap is null)
            {
                continue;
            }

            SaveBitmapToCache(bitmap, cacheFilePath);
            return bitmap;
        }

        return null;
    }

    private string GetMapCacheFilePath(uint? rowId, string? mapKey)
    {
        string mapsPath = NormalizePath(cacheOptions.MapsPath)
            ?? Path.Combine(NormalizePath(cacheOptions.ImagesPath) ?? Path.GetTempPath(), "Maps");
        Directory.CreateDirectory(mapsPath);

        string fileName = rowId is uint resolvedRowId
            ? $"{resolvedRowId}.png"
            : $"{SanitizeFileName(mapKey)}.png";
        return Path.Combine(mapsPath, fileName);
    }

    private static BitmapSource? TryLoadBitmapFromCache(string cacheFilePath)
    {
        if (!File.Exists(cacheFilePath))
        {
            return null;
        }

        try
        {
            using FileStream stream = File.OpenRead(cacheFilePath);
            PngBitmapDecoder decoder = new(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            BitmapSource bitmap = decoder.Frames[0];
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static void SaveBitmapToCache(BitmapSource bitmap, string cacheFilePath)
    {
        try
        {
            string? directoryPath = Path.GetDirectoryName(cacheFilePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            PngBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using FileStream stream = File.Create(cacheFilePath);
            encoder.Save(stream);
        }
        catch
        {
        }
    }

    private static BitmapSource? CreateBitmapSource(TexFile? texFile)
    {
        if (texFile?.ImageData is not { Length: > 0 } imageData)
        {
            return null;
        }

        int width = texFile.Header.Width;
        int height = texFile.Header.Height;
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        BitmapSource bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            imageData,
            width * 4);

        bitmap.Freeze();
        return bitmap;
    }

    private static IEnumerable<string> BuildMapTexturePathCandidates(string? mapKey)
    {
        if (string.IsNullOrWhiteSpace(mapKey))
        {
            yield break;
        }

        string normalizedMapKey = mapKey.Replace('\\', '/').Trim('/');
        string[] segments = normalizedMapKey.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            yield break;
        }

        string leafSegment = segments[^1];
        string parentSegment = segments.Length > 1 ? segments[^2] : leafSegment;
        string compactSegment = string.Concat(segments);

        foreach (string fileName in new[]
        {
            leafSegment,
            $"{leafSegment}_m",
            parentSegment,
            $"{parentSegment}_m",
            $"{parentSegment}{leafSegment}",
            $"{parentSegment}{leafSegment}_m",
            compactSegment,
            $"{compactSegment}_m"
        }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            yield return $"ui/map/{normalizedMapKey}/{fileName}.tex";
        }
    }

    private static string SanitizeFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown-map";
        }

        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        return new string(value.Select(character => invalidCharacters.Contains(character) ? '_' : character).ToArray());
    }

    private static string? ReadStringValue(object instance, string propertyName)
    {
        object? value = instance.GetType().GetProperty(propertyName)?.GetValue(instance);
        return ConvertTextValue(value);
    }

    private static uint? ReadIconId(object instance)
    {
        object? iconValue = instance.GetType().GetProperty("Icon")?.GetValue(instance);
        if (TryConvertToUInt(iconValue, out uint iconId))
        {
            return iconId;
        }

        object? rowId = iconValue?.GetType().GetProperty("RowId")?.GetValue(iconValue);
        return TryConvertToUInt(rowId, out uint referencedIconId)
            ? referencedIconId
            : null;
    }

    private static string FirstNonEmptyString(params string?[] values)
    {
        return values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static string BuildIconPath(uint iconId)
    {
        uint folder = iconId / 1000 * 1000;
        return $"ui/icon/{folder:D6}/{iconId:D6}.tex";
    }
    private Language ResolveContentLanguage()
    {
        return localizationService.CurrentCultureName switch
        {
            "en-US" => Language.English,
            "de-DE" => Language.German,
            "fr-FR" => Language.French,
            _ => Language.Japanese
        };
    }

    private static string FormatRawCoordinates(double posX, double posY)
    {
        return FormatDisplayCoordinates(posX, posY);
    }

    private static string FormatDisplayCoordinates(double posX, double posY)
    {
        return $"X:{TruncateToTenths(posX):0.0} Y:{TruncateToTenths(posY):0.0}";
    }

    private static double TruncateToTenths(double value)
    {
        return Math.Truncate(value * 10d) / 10d;
    }
}
