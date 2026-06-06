using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;
using System.Text.Json;

string sqPackPath = @"C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack";

using var gameData = new GameData(sqPackPath, new LuminaOptions
{
    LoadMultithreaded = false
});

Language[] languages =
[
    Language.Japanese,
    Language.English,
    Language.German,
    Language.French
];

int[] classJobIds = Enumerable.Range(1, 42).ToArray();

var results = classJobIds
    .Select(classJobId => new
    {
        ClassJobId = classJobId,
        Japanese = GetClassJobName(gameData, Language.Japanese, (uint)classJobId),
        English = GetClassJobName(gameData, Language.English, (uint)classJobId),
        German = GetClassJobName(gameData, Language.German, (uint)classJobId),
        French = GetClassJobName(gameData, Language.French, (uint)classJobId)
    })
    .ToArray();

Console.WriteLine(JsonSerializer.Serialize(results, new JsonSerializerOptions
{
    WriteIndented = true
}));

static string? GetClassJobName(GameData gameData, Language language, uint classJobId)
{
    var sheet = gameData.GetExcelSheet<ClassJob>(language, null);
    var row = sheet?.GetRowOrDefault(classJobId);
    return row?.Name.ExtractText();
}
