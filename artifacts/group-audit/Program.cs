using FF14Toolkit.App.Models.Configuration;
using FF14Toolkit.App.Services.GameData;
using FF14Toolkit.App.Services.Hotbar;
using Microsoft.Extensions.Options;

string hotbarPath = @"C:\Users\nakat\OneDrive\ドキュメント\My Games\FINAL FANTASY XIV - A Realm Reborn\FFXIV_CHR00400000020C3080\HOTBAR.DAT";
string sqPackPath = @"C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack";

var parser = new HotbarDatParser();
var result = parser.ParseFile(hotbarPath);

var luminaOptions = Options.Create(new LuminaOptions
{
    SqPackPath = sqPackPath,
    ExcelLanguage = LuminaExcelLanguage.Japanese
});

using var gameDataService = new LuminaGameDataService(luminaOptions);
GameDataStatus status = await gameDataService.CheckAvailabilityAsync();

Console.WriteLine($"LuminaState={status.State}");
Console.WriteLine($"LuminaError={status.ErrorMessage}");

byte[] groups = result.Entries
    .Where(entry => !entry.IsEmpty)
    .Select(entry => entry.GroupId)
    .Distinct()
    .OrderBy(id => id)
    .ToArray();

Console.WriteLine($"Groups={string.Join(",", groups)}");

foreach (byte groupId in groups)
{
    Console.WriteLine($"=== Group {groupId} ===");

    foreach (var hotbar in result.Entries
        .Where(entry => entry.GroupId == groupId && !entry.IsEmpty && entry.HotbarId <= 3)
        .OrderBy(entry => entry.HotbarId)
        .ThenBy(entry => entry.SlotId))
    {
        string? name = gameDataService.ResolveHotbarCommandName(hotbar.SlotTypeId, hotbar.CommandId);
        Console.WriteLine($"{hotbar.DisplayLocation}|Type={hotbar.SlotTypeId}|CommandId={hotbar.CommandId}|Name={name}");
    }
}
