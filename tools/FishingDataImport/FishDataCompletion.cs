using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using DuckbillFisher.Core;
using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;

// Development-only import. All inputs are pinned local files; no network or game process access.
internal static class FishDataCompletion
{
    private static readonly JsonSerializerOptions Json = new()
    { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true,
        WriteIndented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static void Run(string[] args)
    {
        Require(args.Length == 6, "Usage: --complete <game> <baseline> <manifest> <source-directory> <output> <report-directory>");
        using var manifestDoc = JsonDocument.Parse(File.ReadAllBytes(args[2]));
        var manifest = manifestDoc.RootElement;
        var build = manifest.GetProperty("gameBuild").GetString()!;
        Require(File.ReadAllText(Path.Combine(args[0], "ffxivgame.ver")).Trim() == build, "KR build mismatch");
        var baselineText = File.ReadAllText(args[1]).Replace("\r\n", "\n");
        Require(Hash(Encoding.UTF8.GetBytes(baselineText)) == manifest.GetProperty("baselineSha256").GetString(), "Baseline changed");
        var sourceFiles = new Dictionary<string, JsonDocument>();
        try
        {
            foreach (var file in manifest.GetProperty("files").EnumerateObject())
            {
                var bytes = File.ReadAllBytes(Path.Combine(args[3], file.Name));
                Require(Hash(bytes) == file.Value.GetString(), "Source hash mismatch: " + file.Name);
                sourceFiles.Add(file.Name, JsonDocument.Parse(bytes));
            }
            var sources = sourceFiles["fishing-sources.json"].RootElement;
            var patches = sourceFiles["item-patch.json"].RootElement;
            var patchNames = sourceFiles["patch-names.json"].RootElement;
            var names = sourceFiles["items.json"].RootElement;
            var parameters = sourceFiles["fish-parameter.json"].RootElement;
            var root = JsonNode.Parse(baselineText)!.AsObject();
            Require(root["gameBuild"]!.GetValue<string>() == build, "Baseline KR build mismatch");
            var original = root["fish"]!.Deserialize<FishingRule[]>(Json)!;
            var rules = original.ToDictionary(r => r.ItemId);
            using var game = new GameData(Path.Combine(args[0], "sqpack"), new LuminaOptions { DefaultExcelLanguage = Language.Korean });
            var items = game.GetExcelSheet<Item>()!;
            var fish = game.GetExcelSheet<FishParameter>()!.Where(r => r.RowId > 0 && r.IsInLog && r.OceanStars == 0 && r.Item.Is<Item>())
                .OrderBy(r => r.RowId).ToArray();
            var byItem = fish.GroupBy(r => r.Item.RowId).ToDictionary(g => g.Key, g => g.Single());
            var spots = game.GetExcelSheet<FishingSpot>()!.Where(s => s.RowId > 0 && s.TerritoryType.RowId > 0 && s.PlaceNameMain.RowId == 0)
                .ToDictionary(s => s.RowId);
            var notes = game.GetExcelSheet<FishingNoteInfo>()!.Where(n => byItem.ContainsKey(n.Item.RowId))
                .GroupBy(n => n.Item.RowId).ToDictionary(g => g.Key, g => g.Single());
            var changes = new List<Change>();
            var reasons = new Dictionary<uint, string>();
            foreach (var row in fish)
            {
                var id = row.Item.RowId;
                var name = items.GetRow(id).Name.ExtractText();
                var candidates = Candidates(id).OrderBy(c => U(c, "spot") == row.FishingSpot.RowId ? 0 : 1)
                    .ThenBy(c => U(c, "spot")).ThenBy(c => U(c, "bait")).ToArray();
                if (rules.TryGetValue(id, out var rule))
                {
                    var changed = new List<string>();
                    var atSpot = candidates.Where(c => rule.FishingSpotId == 0 || U(c, "spot") == rule.FishingSpotId).ToArray();
                    if (rule.Hookset.Length == 0)
                    {
                        var values = atSpot.Select(Hook).Distinct().ToArray();
                        if (values.Length == 1 && values[0].Length > 0)
                        { rule = rule with { Hookset = values[0] }; changed.Add("hookset"); }
                    }
                    if (rule.Tug.Length == 0)
                    {
                        var values = atSpot.Select(Tug).Distinct().ToArray();
                        if (values.Length == 1 && values[0].Length > 0)
                        { rule = rule with { Tug = values[0] }; changed.Add("tug"); }
                    }
                    if (rule.FishingSpotId == 0)
                    {
                        var matches = candidates.Where(c => SameConditions(rule, c) &&
                            rule.BaitSteps.SelectMany(s => s).All(b => !byItem.ContainsKey(b) || AtSpot(byItem[b], U(c, "spot")))).ToArray();
                        if (matches.Length > 0)
                        { rule = rule with { FishingSpotId = U(matches[0], "spot"), ConditionsKnown = true }; changed.Add("fishingSpotId/conditionsKnown"); }
                        else reasons[id] = "NoCompatibleSpotConditions";
                    }
                    rules[id] = rule;
                    if (changed.Count > 0) changes.Add(new(row.RowId, id, name, changed.ToArray(), "Teamcraft"));
                    continue;
                }

                FishingRule? added = null;
                foreach (var candidate in candidates)
                {
                    var chain = Chain(U(candidate, "bait"), U(candidate, "spot"), new HashSet<uint> { id });
                    if (chain is null) continue;
                    var note = notes[id];
                    var window = Window(candidate);
                    var known = window.Known &&
                        (note.TimeRestriction != 1 || candidate.TryGetProperty("spawn", out _)) &&
                        (note.WeatherRestriction != 1 || Ids(candidate, "weathers").Length + Ids(candidate, "weathersFrom").Length > 0);
                    var predators = Predators(candidate);
                    if (predators.Any(p => !ValidItem(p.ItemId) || p.Count <= 0)) continue;
                    if (parameters.TryGetProperty(id.ToString(CultureInfo.InvariantCulture), out var parameter))
                        Require(U(parameter, "id") == row.RowId && U(parameter, "itemId") == id, "KR/Teamcraft FishParameter mismatch");
                    added = NewRule(row, name) with
                    {
                        FishingSpotId = U(candidate, "spot"), BaitSteps = chain.Select(b => new[] { b }).ToArray(), BaitPath = chain,
                        Hookset = Hook(candidate), Tug = Tug(candidate), StartHour = window.Start, EndHour = window.End,
                        WeatherIds = Ids(candidate, "weathers"), PreviousWeatherIds = Ids(candidate, "weathersFrom"),
                        ConditionsKnown = known, Predators = predators,
                        SnaggingRequired = candidate.TryGetProperty("snagging", out var snag) && snag.GetBoolean(),
                        Lure = U(candidate, "aLure") > 0 ? "Ambitious" : U(candidate, "mLure") > 0 ? "Modest" : ""
                    };
                    break;
                }
                if (added is null)
                {
                    // Reviewed single-catch facts are partial; absence of restrictions is not inferred.
                    var fact = manifest.GetProperty("reviewedFacts").EnumerateArray().FirstOrDefault(f => U(f, "itemId") == id);
                    if (fact.ValueKind == JsonValueKind.Object && ValidItem(U(fact, "baitItemId")) && AtSpot(row, row.FishingSpot.RowId))
                    {
                        var bait = U(fact, "baitItemId");
                        added = NewRule(row, name) with { FishingSpotId = row.FishingSpot.RowId,
                            BaitPath = [bait], BaitSteps = [[bait]], Tug = fact.GetProperty("tug").GetString()!, ConditionsKnown = false };
                        reasons[id] = "ReviewedCatchOnly_NoCompleteConditionsOrHookset";
                    }
                }
                if (added is not null)
                {
                    rules.Add(id, added);
                    changes.Add(new(row.RowId, id, name, ["newRule"], reasons.ContainsKey(id) ? "ReviewedObservation+KR" : "Teamcraft+KR"));
                }
                else reasons[id] = candidates.Length == 0 ? "NoCompatibleStructuredSource" : "NoValidatedBaitChain";
            }

            var gaps = new List<Gap>();
            var optional = new List<Gap>();
            foreach (var row in fish)
            {
                var id = row.Item.RowId;
                var fields = new List<string>();
                var why = new List<string>();
                if (!rules.TryGetValue(id, out var rule))
                { fields.Add("상세 자료 전체"); why.Add(reasons.GetValueOrDefault(id, "NoCompatibleStructuredSource")); }
                else
                {
                    Require(rule.FishParameterId == row.RowId && rule.BaitSteps.Length > 0 &&
                        rule.BaitSteps.All(s => s.Length > 0) && rule.BaitSteps.SelectMany(s => s).All(ValidItem) &&
                        (rule.FishingSpotId == 0 ? !rule.ConditionsKnown : AtSpot(row, rule.FishingSpotId)) &&
                        rule.StartHour >= 0 && rule.StartHour < 24 && rule.EndHour >= 0 && rule.EndHour <= 24 &&
                        rule.StartHour != rule.EndHour && rule.WeatherIds.Concat(rule.PreviousWeatherIds).All(w =>
                            game.GetExcelSheet<Weather>()!.GetRowOrDefault(w) is { Icon: > 0 }), "Invalid imported rule");
                    if (rule.Hookset.Length == 0) { fields.Add("낚아채기"); why.Add("SourceHooksetNotEstablished"); }
                    if (rule.Tug.Length == 0) { fields.Add("입질 강도"); why.Add("SourceTugNotEstablished"); }
                    if (rule.FishingSpotId == 0) { fields.Add("예측 기준 낚시터"); why.Add("NoCompatibleSpotConditions"); }
                    if (!rule.ConditionsKnown) { fields.Add("시간·날씨 조건 완전성"); why.Add(reasons.GetValueOrDefault(id, "ConditionsNotEstablished")); }
                    if (rule.Predators.Length > 0 && rule.IntuitionSeconds is null)
                    { fields.Add("직감 지속 시간"); why.Add("SourcesOmitIntuitionDuration"); }
                    if (rule.FishEyesSupported is null)
                        optional.Add(new(row.RowId, id, items.GetRow(id).Name.ExtractText(), ["물고기의 눈 지원 여부(UI 미표시)"], ["NotProvidedBySupplement"]));
                }
                if (fields.Count > 0) gaps.Add(new(row.RowId, id, items.GetRow(id).Name.ExtractText(), fields.ToArray(), why.Distinct().ToArray()));
            }
            root["dataVersion"] = manifest.GetProperty("dataVersion").GetString();
            root["fish"] = JsonSerializer.SerializeToNode(rules.Values.OrderBy(r => r.FishParameterId).ToArray(), Json);
            root["supplementalSources"] = JsonSerializer.SerializeToNode(new[] { new { name = "Teamcraft",
                repository = manifest.GetProperty("repository").GetString(), revision = manifest.GetProperty("revision").GetString(),
                license = manifest.GetProperty("license").GetString(), files = manifest.GetProperty("files") } }, Json);
            root["reviewedObservations"] = JsonNode.Parse(manifest.GetProperty("reviewedFacts").GetRawText());
            File.WriteAllText(args[4], root.ToJsonString(Json) + "\n", new UTF8Encoding(false));
            Directory.CreateDirectory(args[5]);
            var oceans = game.GetExcelSheet<FishParameter>()!.Where(r => r.RowId > 0 && r.IsInLog && r.OceanStars > 0 && r.Item.Is<Item>())
                .OrderBy(r => r.RowId).Select(r => new Gap(r.RowId, r.Item.RowId, items.GetRow(r.Item.RowId).Name.ExtractText(),
                    ["먼바다 어종별 미끼·특수 조건"], ["OceanDetailsIntegrationOutsideThisRegularImport"])).ToArray();
            var spears = root["spearfishing"]!.Deserialize<SpearfishingRule[]>(Json)!.Where(r => !r.ConditionsKnown)
                .OrderBy(r => r.SpearfishingItemId).Select(r => new Gap(r.SpearfishingItemId, r.ItemId, items.GetRow(r.ItemId).Name.ExtractText(),
                    ["작살 크기·출현 조건"], ["PrimarySourceDataMissing_SpearModeNotIntegrated"])).ToArray();
            File.WriteAllText(Path.Combine(args[5], "audit.json"), JsonSerializer.Serialize(new { build,
                before = original.Length, after = rules.Count, regular = fish.Length, changes, gaps, optional,
                ocean = oceans.Length, spearUnknown = spears.Length }, Json) + "\n");
            WriteCsv(Path.Combine(args[5], "remaining.csv"), gaps);
            WriteCsv(Path.Combine(args[5], "optional-metadata.csv"), optional);
            WriteCsv(Path.Combine(args[5], "ocean-pending.csv"), oceans);
            WriteCsv(Path.Combine(args[5], "spearfishing-pending.csv"), spears, "SpearfishingItem");
            var report = new StringBuilder("# 일반 낚시 데이터 보완 후 미확인 목록\n\n");
            report.AppendLine($"KR 빌드: `{build}` / 데이터: `{manifest.GetProperty("dataVersion").GetString()}`\n");
            report.AppendLine($"일반 낚시 {fish.Length}종 중 상세 {original.Length}→{rules.Count}종. 변경 {changes.Count}종, 핵심 안내 필드 미확인 {gaps.Count}종.\n");
            report.AppendLine("| FishParameter | Item | 어종 | 남은 항목 | 남긴 이유 |\n|---:|---:|---|---|---|");
            foreach (var gap in gaps) report.AppendLine($"| {gap.FishParameterId} | {gap.ItemId} | {gap.Name} | {string.Join(" / ", gap.Fields)} | {string.Join(" / ", gap.Reasons.Select(ReasonText))} |");
            report.AppendLine($"\n선택적 메타데이터: 물고기의 눈 지원 여부 미기재 {optional.Count}종은 별도 CSV에 모두 기록했다. 해당 정보는 사용자 결정으로 현재 UI에 표시하지 않는다. 원본의 bool 값은 보존하고 새 자료에서 확인되지 않은 값은 null로 구분한다.");
            report.AppendLine($"\n먼바다 {oceans.Length}종과 기존 작살 미확인 {spears.Length}건도 별도 CSV에 ID·이름·이유를 모두 기록했다. 이번 일반 낚시 보완 범위 밖의 표시/연동 작업이며 수집 상태·입질 초 수 통계와 구분한다. 기존 시간·날씨 예측 공식과 이미 알려진 조건은 변경하지 않았다.");
            report.AppendLine("\n## 전체 목록 파일\n\n- [일반 낚시 잔여 항목](remaining.csv): 위 표 전체와 안정적인 이유 코드.\n- [선택적 메타데이터](optional-metadata.csv): 추가 정적 자료에 물고기의 눈 지원 여부가 없어 null로 남김.\n- [먼바다](ocean-pending.csv): 기본 해역·항로 정보는 제공 중이며 어종별 미끼·특수 조건의 별도 연동은 후속. 원본 자료가 전혀 없다는 뜻은 아님.\n- [작살낚시](spearfishing-pending.csv): 기존 원본의 크기·조건 미확인과 전용 모드 연동이 후속. 첫 열은 FishParameter가 아닌 SpearfishingItem ID.\n\nCSV는 Excel에서 읽을 수 있는 UTF-8 BOM이며 이름은 조사한 KR 클라이언트 기준이다.\n");
            File.WriteAllText(Path.Combine(args[5], "remaining.md"), report.ToString().TrimEnd() + "\n", new UTF8Encoding(false));
            Console.WriteLine($"Regular={fish.Length}; Before={original.Length}; After={rules.Count}; Changed={changes.Count}; Gaps={gaps.Count}; OptionalMetadata={optional.Count}");

            bool ValidItem(uint id) => id > 0 && items.GetRowOrDefault(id) is { } item && item.Icon > 0 && !string.IsNullOrWhiteSpace(item.Name.ExtractText());
            bool AtSpot(FishParameter row, uint spot) => spots.TryGetValue(spot, out var s) &&
                (row.FishingSpot.RowId == spot || s.Item.Any(i => i.RowId == row.Item.RowId));
            JsonElement[] Candidates(uint id) => !sources.TryGetProperty(id.ToString(CultureInfo.InvariantCulture), out var list) ? [] :
                list.EnumerateArray().Where(c => U(c, "spot") > 0 && U(c, "bait") > 0 && byItem.TryGetValue(id, out var r) &&
                    AtSpot(r, U(c, "spot")) && ValidItem(U(c, "bait")) && !c.TryGetProperty("oceanFishingTime", out _)).ToArray();
            uint[]? Chain(uint bait, uint spot, HashSet<uint> seen)
            {
                if (!ValidItem(bait) || seen.Count > 10 || !seen.Add(bait)) return null;
                if (!byItem.TryGetValue(bait, out var child)) return [bait];
                if (!AtSpot(child, spot)) return null;
                foreach (var next in Candidates(bait).Where(c => U(c, "spot") == spot).OrderBy(c => U(c, "bait")))
                {
                    var prefix = Chain(U(next, "bait"), spot, new HashSet<uint>(seen));
                    if (prefix is not null) return [.. prefix, bait];
                }
                return null;
            }
            FishingRule NewRule(FishParameter row, string name)
            {
                var patch = 0d;
                var version = "";
                if (patches.TryGetProperty(row.Item.RowId.ToString(), out var index) &&
                    patchNames.TryGetProperty(index.GetRawText(), out var info))
                {
                    version = info.GetProperty("version").GetString()!;
                    double.TryParse(version.TrimEnd('a', 'b', 'c'), CultureInfo.InvariantCulture, out patch);
                }
                var english = names.TryGetProperty(row.Item.RowId.ToString(), out var n) && n.TryGetProperty("en", out var en) ? en.GetString() : null;
                return new() { ItemId = row.Item.RowId, FishParameterId = row.RowId, SourceName = english ?? name,
                    StartHour = 0, EndHour = 24, FolkloreRequired = row.GatheringSubCategory.RowId > 0,
                    FishEyesSupported = null, BigFish = items.GetRow(row.Item.RowId).Description.ExtractText().Contains("터주", StringComparison.Ordinal),
                    Patch = patch, PatchVersion = version };
            }
        }
        finally { foreach (var doc in sourceFiles.Values) doc.Dispose(); }
    }

    private static uint U(JsonElement row, string key) => row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetUInt32(out var number) ? number : 0;
    private static uint[] Ids(JsonElement row, string key) => row.TryGetProperty(key, out var value) ? value.EnumerateArray().Select(v => v.GetUInt32()).ToArray() : [];
    private static string Hook(JsonElement row) => U(row, "hookset") switch { 1 => "Powerful", 2 => "Precision", _ => "" };
    private static string Tug(JsonElement row) => !row.TryGetProperty("tug", out var value) || value.ValueKind != JsonValueKind.Number ? "" :
        U(row, "tug") switch { 0 => "medium", 1 => "heavy", 2 => "light", _ => "" };
    private static FishRequirement[] Predators(JsonElement row) => row.TryGetProperty("predators", out var values)
        ? values.EnumerateArray().Select(v => new FishRequirement(U(v, "id"), (int)U(v, "amount"))).ToArray() : [];
    private static (double Start, double End, bool Known) Window(JsonElement row)
    {
        if (!row.TryGetProperty("spawn", out var spawn)) return (0, 24, !row.TryGetProperty("duration", out _));
        if (!row.TryGetProperty("duration", out var duration)) return (0, 24, false);
        var start = spawn.GetDouble(); var hours = duration.GetDouble(); var end = start + hours;
        if (!double.IsFinite(start) || !double.IsFinite(hours) || start < 0 || start >= 24 || hours <= 0 || hours > 24) return (0, 24, false);
        if (hours == 24) return (0, 24, true);
        return (start, end <= 24 ? end : end % 24, true);
    }
    private static bool SameConditions(FishingRule rule, JsonElement row)
    {
        var window = Window(row);
        return window.Known && rule.StartHour == window.Start && rule.EndHour == window.End &&
            rule.WeatherIds.Order().SequenceEqual(Ids(row, "weathers").Order()) &&
            rule.PreviousWeatherIds.Order().SequenceEqual(Ids(row, "weathersFrom").Order()) &&
            rule.Predators.OrderBy(p => p.ItemId).SequenceEqual(Predators(row).OrderBy(p => p.ItemId));
    }
    private static void WriteCsv(string path, IEnumerable<Gap> gaps, string idColumn = "FishParameter")
    {
        static string Cell(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        var lines = new[] { idColumn + ",Item,Name,Fields,Reasons" }.Concat(gaps.Select(g =>
            $"{g.FishParameterId},{g.ItemId},{Cell(g.Name)},{Cell(string.Join(";", g.Fields))},{Cell(string.Join(";", g.Reasons))}"));
        File.WriteAllLines(path, lines, new UTF8Encoding(true));
    }
    private static string Hash(byte[] value) => Convert.ToHexString(SHA256.HashData(value));
    private static string ReasonText(string code) => code switch
    {
        "SourcesOmitIntuitionDuration" => "채택한 두 원본에 직감 지속 시간이 없음. 출현 시간 길이로 대체하지 않음",
        "SourceHooksetNotEstablished" => "공개 어획 기록에 낚아채기 종류가 기재되지 않음",
        "ReviewedCatchOnly_NoCompleteConditionsOrHookset" => "개별 어획으로 미끼·입질 강도만 확인. 시간·날씨 제한 전체는 입증되지 않음",
        _ => code
    };
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    private sealed record Change(uint FishParameterId, uint ItemId, string Name, string[] Fields, string Source);
    private sealed record Gap(uint FishParameterId, uint ItemId, string Name, string[] Fields, string[] Reasons);
}
