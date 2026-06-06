using FF14Toolkit.App.Models.Configuration;
using AppLuminaOptions = FF14Toolkit.App.Models.Configuration.LuminaOptions;
using Lumina;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ActionSheet = Lumina.Excel.Sheets.Action;
using CraftActionSheet = Lumina.Excel.Sheets.CraftAction;

namespace FF14Toolkit.App.Services.GameData;

public sealed class LuminaGameDataService : IGameDataService, IDisposable
{
    private static readonly Language DefaultExcelLanguage = Language.Japanese;

    private readonly SemaphoreSlim initializationLock = new(1, 1);
    private readonly AppLuminaOptions options;
    private Lumina.GameData? gameData;
    private string? errorMessage;
    private bool disposed;

    public LuminaGameDataService(IOptions<AppLuminaOptions> options)
    {
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SqPackPath);

    public bool IsAvailable => gameData is not null;

    public string? SqPackPath => string.IsNullOrWhiteSpace(options.SqPackPath)
        ? null
        : options.SqPackPath;

    public string? ErrorMessage => errorMessage;

    public Language ExcelLanguage => ConvertExcelLanguage(options.ExcelLanguage);

    public string? ResolveHotbarCommandName(byte slotTypeId, uint commandId)
    {
        if (gameData is null || commandId == 0)
        {
            return null;
        }

        return slotTypeId switch
        {
            1 => GetRowName(gameData.GetExcelSheet<ActionSheet>(ExcelLanguage, null), commandId, row => row.Name.ExtractText()),
            2 => GetItemName(commandId),
            9 => GetRowName(gameData.GetExcelSheet<CraftActionSheet>(ExcelLanguage, null), commandId, row => row.Name.ExtractText()),
            10 => GetRowName(gameData.GetExcelSheet<GeneralAction>(ExcelLanguage, null), commandId, row => row.Name.ExtractText()),
            _ => null
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

        return GetRowName(gameData.GetExcelSheet<Item>(ExcelLanguage, null), normalizedCommandId, row => row.Name.ExtractText());
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
}
