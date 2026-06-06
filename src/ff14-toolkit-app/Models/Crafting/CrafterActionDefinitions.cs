using System;
using System.Collections.Generic;
using System.Linq;

namespace FF14Toolkit.App.Models.Crafting;

public enum CrafterActionId
{
    BasicSynthesis = 1,
    BasicTouch = 2,
    MastersMend = 3,
    StandardTouch = 4,
    Observe = 5,
    PreciseTouch = 6,
    CarefulSynthesis = 7,
    PrudentTouch = 8,
    TrainedEye = 9,
    PreparatoryTouch = 10,
    IntensiveSynthesis = 11,
    DelicateSynthesis = 12,
    ByregotsBlessing = 13,
    HastyTouch = 14,
    RapidSynthesis = 15,
    TricksOfTheTrade = 16,
    MuscleMemory = 17,
    Reflect = 18,
    CarefulObservation = 19,
    Groundwork = 20,
    AdvancedTouch = 21,
    HeartAndSoul = 22,
    PrudentSynthesis = 23,
    TrainedFinesse = 24,
    RefinedTouch = 25,
    QuickInnovation = 26,
    ImmaculateMend = 27,
    TrainedPerfection = 28
}

public sealed class CrafterActionDefinition
{
    public required CrafterActionId ActionId { get; init; }

    public required string NameJa { get; init; }

    public required string NameEn { get; init; }

    public required string NameDe { get; init; }

    public required string NameFr { get; init; }

    public required IReadOnlyList<CrafterActionVariant> Variants { get; init; }

    public bool TryGetVariant(int classJobId, out CrafterActionVariant? variant)
    {
        variant = Variants.FirstOrDefault(candidate => candidate.ClassJobId == classJobId);
        return variant is not null;
    }
}

public sealed class CrafterActionVariant
{
    public required int ClassJobId { get; init; }

    public required string ClassJobAbbreviation { get; init; }

    public required uint LuminaActionId { get; init; }

    public required string IconPath { get; init; }
}

public static class CrafterActionDefinitions
{
    private static readonly IReadOnlyList<CrafterClassJobInfo> CrafterJobs =
    [
        new(8, "CRP"),
        new(9, "BSM"),
        new(10, "ARM"),
        new(11, "GSM"),
        new(12, "LTW"),
        new(13, "WVR"),
        new(14, "ALC"),
        new(15, "CUL")
    ];

    public static IReadOnlyList<CrafterActionDefinition> All { get; } =
    [
        Create(CrafterActionId.BasicSynthesis, "作業", "Basic Synthesis", "Bearbeiten", "Travail de base", [100001u, 100015u, 100030u, 100075u, 100045u, 100060u, 100090u, 100105u], [1501u, 1551u, 1601u, 1651u, 1701u, 1751u, 1801u, 1851u]),
        Create(CrafterActionId.BasicTouch, "加工", "Basic Touch", "Veredelung", "Ouvrage de base", [100002u, 100016u, 100031u, 100076u, 100046u, 100061u, 100091u, 100106u], [1502u, 1552u, 1602u, 1652u, 1702u, 1752u, 1802u, 1852u]),
        Create(CrafterActionId.MastersMend, "マスターズメンド", "Master's Mend", "Wiederherstellung", "Réparation de maître", [100003u, 100017u, 100032u, 100077u, 100047u, 100062u, 100092u, 100107u], [1952u, 1952u, 1952u, 1952u, 1952u, 1952u, 1952u, 1952u]),
        Create(CrafterActionId.StandardTouch, "中級加工", "Standard Touch", "Solide Veredelung", "Ouvrage standard", [100004u, 100018u, 100034u, 100078u, 100048u, 100064u, 100093u, 100109u], [1516u, 1566u, 1616u, 1665u, 1716u, 1765u, 1816u, 1865u]),
        Create(CrafterActionId.Observe, "経過観察", "Observe", "Beobachten", "Observation", [100010u, 100023u, 100040u, 100082u, 100053u, 100070u, 100099u, 100113u], [1954u, 1954u, 1954u, 1954u, 1954u, 1954u, 1954u, 1954u]),
        Create(CrafterActionId.PreciseTouch, "集中加工", "Precise Touch", "Präzise Veredelung", "Ouvrage précis", [100128u, 100129u, 100130u, 100131u, 100132u, 100133u, 100134u, 100135u], [1524u, 1574u, 1625u, 1676u, 1724u, 1774u, 1825u, 1875u]),
        Create(CrafterActionId.CarefulSynthesis, "模範作業", "Careful Synthesis", "Sorgfältige Bearbeitung", "Travail prudent", [100203u, 100204u, 100205u, 100206u, 100207u, 100208u, 100209u, 100210u], [1986u, 1986u, 1986u, 1986u, 1986u, 1986u, 1986u, 1986u]),
        Create(CrafterActionId.PrudentTouch, "倹約加工", "Prudent Touch", "Nachhaltige Veredelung", "Ouvrage parcimonieux", [100227u, 100228u, 100229u, 100230u, 100231u, 100232u, 100233u, 100234u], [1535u, 1584u, 1635u, 1686u, 1734u, 1784u, 1835u, 1886u]),
        Create(CrafterActionId.TrainedEye, "匠の早業", "Trained Eye", "Flinke Hand", "Main preste", [100283u, 100284u, 100285u, 100286u, 100287u, 100288u, 100289u, 100290u], [1981u, 1981u, 1981u, 1981u, 1981u, 1981u, 1981u, 1981u]),
        Create(CrafterActionId.PreparatoryTouch, "下地加工", "Preparatory Touch", "Basisveredelung", "Ouvrage préparatoire", [100299u, 100300u, 100301u, 100302u, 100303u, 100304u, 100305u, 100306u], [1507u, 1557u, 1607u, 1657u, 1707u, 1757u, 1807u, 1857u]),
        Create(CrafterActionId.IntensiveSynthesis, "集中作業", "Intensive Synthesis", "Fokussierte Bearbeitung", "Travail vigilant", [100315u, 100316u, 100317u, 100318u, 100319u, 100320u, 100321u, 100322u], [1514u, 1564u, 1614u, 1663u, 1714u, 1763u, 1814u, 1863u]),
        Create(CrafterActionId.DelicateSynthesis, "精密作業", "Delicate Synthesis", "Akribische Bearbeitung", "Travail minutieux", [100323u, 100324u, 100325u, 100326u, 100327u, 100328u, 100329u, 100330u], [1503u, 1553u, 1603u, 1653u, 1703u, 1753u, 1803u, 1853u]),
        Create(CrafterActionId.ByregotsBlessing, "ビエルゴの祝福", "Byregot's Blessing", "Byregots Benediktion", "Bénédiction de Byregot", [100339u, 100340u, 100341u, 100342u, 100343u, 100344u, 100345u, 100346u], [1975u, 1975u, 1975u, 1975u, 1975u, 1975u, 1975u, 1975u]),
        Create(CrafterActionId.HastyTouch, "ヘイスティタッチ", "Hasty Touch", "Hastige Veredelung", "Ouvrage hâtif", [100355u, 100356u, 100357u, 100358u, 100359u, 100360u, 100361u, 100362u], [1989u, 1989u, 1989u, 1989u, 1989u, 1989u, 1989u, 1989u]),
        Create(CrafterActionId.RapidSynthesis, "突貫作業", "Rapid Synthesis", "Schnelle Bearbeitung", "Travail rapide", [100363u, 100364u, 100365u, 100366u, 100367u, 100368u, 100369u, 100370u], [1988u, 1988u, 1988u, 1988u, 1988u, 1988u, 1988u, 1988u]),
        Create(CrafterActionId.TricksOfTheTrade, "秘訣", "Tricks of the Trade", "Kunstgriff", "Ficelles du métier", [100371u, 100372u, 100373u, 100374u, 100375u, 100376u, 100377u, 100378u], [1990u, 1990u, 1990u, 1990u, 1990u, 1990u, 1990u, 1990u]),
        Create(CrafterActionId.MuscleMemory, "確信", "Muscle Memory", "Motorisches Gedächtnis", "Mémoire musculaire", [100379u, 100380u, 100381u, 100382u, 100383u, 100384u, 100385u, 100386u], [1994u, 1994u, 1994u, 1994u, 1994u, 1994u, 1994u, 1994u]),
        Create(CrafterActionId.Reflect, "真価", "Reflect", "Einkehr", "Véritable valeur", [100387u, 100388u, 100389u, 100390u, 100391u, 100392u, 100393u, 100394u], [1982u, 1982u, 1982u, 1982u, 1982u, 1982u, 1982u, 1982u]),
        Create(CrafterActionId.CarefulObservation, "設計変更", "Careful Observation", "Planänderung", "Changement de patron", [100395u, 100396u, 100397u, 100398u, 100399u, 100400u, 100401u, 100402u], [1984u, 1984u, 1984u, 1984u, 1984u, 1984u, 1984u, 1984u]),
        Create(CrafterActionId.Groundwork, "下地作業", "Groundwork", "Vorarbeit", "Travail préparatoire", [100403u, 100404u, 100405u, 100406u, 100407u, 100408u, 100409u, 100410u], [1518u, 1568u, 1618u, 1667u, 1718u, 1767u, 1818u, 1867u]),
        Create(CrafterActionId.AdvancedTouch, "上級加工", "Advanced Touch", "Höhere Veredelung", "Ouvrage avancé", [100411u, 100412u, 100413u, 100414u, 100415u, 100416u, 100417u, 100418u], [1519u, 1569u, 1620u, 1669u, 1719u, 1769u, 1820u, 1869u]),
        Create(CrafterActionId.HeartAndSoul, "一心不乱", "Heart and Soul", "Mit Leib und Seele", "Attention totale", [100419u, 100420u, 100421u, 100422u, 100423u, 100424u, 100425u, 100426u], [1996u, 1996u, 1996u, 1996u, 1996u, 1996u, 1996u, 1996u]),
        Create(CrafterActionId.PrudentSynthesis, "倹約作業", "Prudent Synthesis", "Rationelle Bearbeitung", "Travail économe", [100427u, 100428u, 100429u, 100430u, 100431u, 100432u, 100433u, 100434u], [1520u, 1570u, 1621u, 1670u, 1720u, 1770u, 1821u, 1870u]),
        Create(CrafterActionId.TrainedFinesse, "匠の神業", "Trained Finesse", "Götter Werk", "Main divine", [100435u, 100436u, 100437u, 100438u, 100439u, 100440u, 100441u, 100442u], [1997u, 1997u, 1997u, 1997u, 1997u, 1997u, 1997u, 1997u]),
        Create(CrafterActionId.RefinedTouch, "洗練加工", "Refined Touch", "Raffinierte Veredelung", "Ouvrage raffiné", [100443u, 100444u, 100445u, 100446u, 100447u, 100448u, 100449u, 100450u], [1522u, 1572u, 1623u, 1674u, 1722u, 1772u, 1823u, 1873u]),
        Create(CrafterActionId.QuickInnovation, "クイックイノベーション", "Quick Innovation", "Spontane Innovation", "Innovation instantanée", [100459u, 100460u, 100461u, 100462u, 100463u, 100464u, 100465u, 100466u], [1999u, 1999u, 1999u, 1999u, 1999u, 1999u, 1999u, 1999u]),
        Create(CrafterActionId.ImmaculateMend, "パーフェクトメンド", "Immaculate Mend", "Winkelzug", "Réparation totale", [100467u, 100468u, 100469u, 100470u, 100471u, 100472u, 100473u, 100474u], [1950u, 1950u, 1950u, 1950u, 1950u, 1950u, 1950u, 1950u]),
        Create(CrafterActionId.TrainedPerfection, "匠の絶技", "Trained Perfection", "Meisters Beitrag", "Main suprême", [100475u, 100476u, 100477u, 100478u, 100479u, 100480u, 100481u, 100482u], [1926u, 1926u, 1926u, 1926u, 1926u, 1926u, 1926u, 1926u])
    ];

    private static readonly IReadOnlyDictionary<CrafterActionId, CrafterActionDefinition> DefinitionsByActionId =
        All.ToDictionary(definition => definition.ActionId);

    private static readonly IReadOnlyDictionary<uint, CrafterActionDefinition> DefinitionsByLuminaActionId =
        All.SelectMany(definition => definition.Variants.Select(variant => new KeyValuePair<uint, CrafterActionDefinition>(variant.LuminaActionId, definition)))
            .ToDictionary(pair => pair.Key, pair => pair.Value);

    public static bool TryGetDefinition(CrafterActionId actionId, out CrafterActionDefinition? definition)
    {
        return DefinitionsByActionId.TryGetValue(actionId, out definition);
    }

    public static bool TryGetDefinitionByLuminaActionId(uint luminaActionId, out CrafterActionDefinition? definition)
    {
        return DefinitionsByLuminaActionId.TryGetValue(luminaActionId, out definition);
    }

    private static CrafterActionDefinition Create(
        CrafterActionId actionId,
        string nameJa,
        string nameEn,
        string nameDe,
        string nameFr,
        IReadOnlyList<uint> luminaActionIds,
        IReadOnlyList<uint> iconIds)
    {
        if (luminaActionIds.Count != CrafterJobs.Count)
        {
            throw new ArgumentException("Lumina action ID count does not match crafter job count.", nameof(luminaActionIds));
        }

        if (iconIds.Count != CrafterJobs.Count)
        {
            throw new ArgumentException("Icon ID count does not match crafter job count.", nameof(iconIds));
        }

        CrafterActionVariant[] variants = new CrafterActionVariant[CrafterJobs.Count];

        for (int index = 0; index < CrafterJobs.Count; index++)
        {
            CrafterClassJobInfo crafterJob = CrafterJobs[index];
            variants[index] = new CrafterActionVariant
            {
                ClassJobId = crafterJob.ClassJobId,
                ClassJobAbbreviation = crafterJob.ClassJobAbbreviation,
                LuminaActionId = luminaActionIds[index],
                IconPath = BuildIconPath(iconIds[index])
            };
        }

        return new CrafterActionDefinition
        {
            ActionId = actionId,
            NameJa = nameJa,
            NameEn = nameEn,
            NameDe = nameDe,
            NameFr = nameFr,
            Variants = variants
        };
    }

    private static string BuildIconPath(uint iconId)
    {
        uint folder = iconId / 1000 * 1000;
        return $"ui/icon/{folder:D6}/{iconId:D6}.tex";
    }

    private sealed record CrafterClassJobInfo(int ClassJobId, string ClassJobAbbreviation);
}
